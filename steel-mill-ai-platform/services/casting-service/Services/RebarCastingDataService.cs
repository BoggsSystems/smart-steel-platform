using Microsoft.Azure.Cosmos;
using CastingService.Models;
using System.Collections.Concurrent;

namespace CastingService.Services;

public interface IRebarCastingDataService
{
    Task<RebarCastingStatus?> GetCasterStatusAsync(string casterId);
    Task<IEnumerable<RebarCastingTelemetry>> GetCasterTelemetryAsync(
        string casterId,
        DateTime? startTime,
        DateTime? endTime,
        RebarGrade? grade = null
    );
    Task UpdateMoldConfigurationAsync(string casterId, RebarMoldConfiguration config);
    Task<RebarCastingPerformanceMetrics?> GetPerformanceMetricsAsync(string casterId, RebarGrade? grade = null);
    Task CreateCastingAlertAsync(RebarCastingAlert alert);
    Task SaveTelemetryAsync(RebarCastingTelemetry telemetry);
    Task<IEnumerable<BilletQualityInspection>> GetBilletInspectionsAsync(string casterId, RebarGrade? grade = null);
    Task SaveBilletInspectionAsync(BilletQualityInspection inspection);
    Task<RebarProductionSchedule?> GetProductionScheduleAsync(string casterId);
    Task UpdateProductionScheduleAsync(RebarProductionSchedule schedule);
    Task<IEnumerable<RebarCastingAlert>> GetActiveAlertsAsync(string? casterId = null);
    Task ResolveAlertAsync(string alertId, string resolutionNotes);
}

public class RebarCastingDataService : IRebarCastingDataService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _telemetryContainer;
    private readonly Container _statusContainer;
    private readonly Container _alertsContainer;
    private readonly Container _performanceContainer;
    private readonly Container _inspectionContainer;
    private readonly Container _scheduleContainer;
    private readonly ILogger<RebarCastingDataService> _logger;
    
    // In-memory cache for real-time operations
    private readonly ConcurrentDictionary<string, RebarCastingStatus> _statusCache = new();

    public RebarCastingDataService(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<RebarCastingDataService> logger
    )
    {
        _cosmosClient = cosmosClient;
        _logger = logger;

        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "SteelMillRebarDB";
        
        _telemetryContainer = _cosmosClient.GetContainer(databaseName, "RebarCastingTelemetry");
        _statusContainer = _cosmosClient.GetContainer(databaseName, "RebarCastingStatus");
        _alertsContainer = _cosmosClient.GetContainer(databaseName, "RebarCastingAlerts");
        _performanceContainer = _cosmosClient.GetContainer(databaseName, "RebarCastingPerformance");
        _inspectionContainer = _cosmosClient.GetContainer(databaseName, "BilletQualityInspections");
        _scheduleContainer = _cosmosClient.GetContainer(databaseName, "RebarProductionSchedules");
    }

    public async Task<RebarCastingStatus?> GetCasterStatusAsync(string casterId)
    {
        try
        {
            // Check cache first
            if (_statusCache.TryGetValue(casterId, out var cachedStatus))
            {
                return cachedStatus;
            }

            var response = await _statusContainer.ReadItemAsync<RebarCastingStatus>(
                casterId,
                new PartitionKey(casterId)
            );
            
            var status = response.Resource;
            _statusCache.TryAdd(casterId, status);
            return status;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Caster status not found for {CasterId}", casterId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving caster status for {CasterId}", casterId);
            throw;
        }
    }

    public async Task<IEnumerable<RebarCastingTelemetry>> GetCasterTelemetryAsync(
        string casterId,
        DateTime? startTime,
        DateTime? endTime,
        RebarGrade? grade = null
    )
    {
        try
        {
            var queryText = "SELECT * FROM c WHERE c.CasterId = @casterId";

            if (startTime.HasValue)
                queryText += " AND c.Timestamp >= @startTime";
            if (endTime.HasValue)
                queryText += " AND c.Timestamp <= @endTime";
            if (grade.HasValue)
                queryText += " AND c.CurrentGrade = @grade";

            queryText += " ORDER BY c.Timestamp DESC";

            var queryDefinition = new QueryDefinition(queryText).WithParameter("@casterId", casterId);

            if (startTime.HasValue)
                queryDefinition.WithParameter("@startTime", startTime.Value);
            if (endTime.HasValue)
                queryDefinition.WithParameter("@endTime", endTime.Value);
            if (grade.HasValue)
                queryDefinition.WithParameter("@grade", (int)grade.Value);

            var results = new List<RebarCastingTelemetry>();

            using var iterator = _telemetryContainer.GetItemQueryIterator<RebarCastingTelemetry>(
                queryDefinition
            );
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving telemetry for caster {CasterId}", casterId);
            throw;
        }
    }

    public async Task UpdateMoldConfigurationAsync(string casterId, RebarMoldConfiguration config)
    {
        try
        {
            var status = await GetCasterStatusAsync(casterId) ?? new RebarCastingStatus { CasterId = casterId };

            status.TargetGrade = config.TargetGrade;
            status.BilletSize = config.BilletSize;
            status.LastUpdated = DateTime.UtcNow;

            await _statusContainer.UpsertItemAsync(status, new PartitionKey(casterId));
            _statusCache.AddOrUpdate(casterId, status, (key, oldValue) => status);
            
            _logger.LogInformation("Updated mold configuration for caster {CasterId} - Grade: {Grade}, Size: {Size}", 
                casterId, config.TargetGrade, config.BilletSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating mold configuration for caster {CasterId}", casterId);
            throw;
        }
    }

    public async Task<RebarCastingPerformanceMetrics?> GetPerformanceMetricsAsync(string casterId, RebarGrade? grade = null)
    {
        try
        {
            var telemetryData = await GetCasterTelemetryAsync(
                casterId,
                DateTime.UtcNow.AddHours(-1),
                DateTime.UtcNow,
                grade
            );

            if (!telemetryData.Any())
                return null;

            var status = await GetCasterStatusAsync(casterId);
            var currentGrade = grade ?? status?.TargetGrade ?? RebarGrade.Grade60;
            var currentSize = status?.BilletSize ?? BilletSize.Size120x120;

            var metrics = new RebarCastingPerformanceMetrics
            {
                CasterId = casterId,
                Grade = currentGrade,
                BilletSize = currentSize,
                CastingEfficiency = CalculateRebarCastingEfficiency(telemetryData),
                QualityIndex = CalculateRebarQualityIndex(telemetryData),
                TemperatureStability = CalculateTemperatureStability(telemetryData),
                CoolingEfficiency = CalculateCoolingEfficiency(telemetryData),
                VibrationLevel = telemetryData.Average(t => t.VibrationLevel),
                ThroughputRate = telemetryData.Average(t => t.CastingSpeed),
                ChemicalComplianceRate = CalculateChemicalComplianceRate(telemetryData, currentGrade),
                DimensionalAccuracy = CalculateDimensionalAccuracy(telemetryData, currentSize),
                SurfaceQualityScore = CalculateSurfaceQualityScore(telemetryData),
                BilletStraightnessIndex = CalculateBilletStraightnessIndex(telemetryData),
                YieldRatio = CalculateYieldRatio(telemetryData),
                TotalBilletsProduced = status?.BilletsProduced ?? 0,
                ProductionRate = telemetryData.Count() > 0 ? telemetryData.Count() : 0,
                CalculatedAt = DateTime.UtcNow
            };

            // Store metrics for historical analysis
            await _performanceContainer.CreateItemAsync(metrics, new PartitionKey(casterId));

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating performance metrics for caster {CasterId}", casterId);
            throw;
        }
    }

    public async Task CreateCastingAlertAsync(RebarCastingAlert alert)
    {
        try
        {
            alert.CreatedAt = DateTime.UtcNow;
            await _alertsContainer.CreateItemAsync(alert, new PartitionKey(alert.CasterId));
            
            _logger.LogWarning(
                "Created rebar casting alert {AlertId} for caster {CasterId}: {AlertType} - Grade: {Grade}",
                alert.Id,
                alert.CasterId,
                alert.AlertType,
                alert.AffectedGrade
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alert for caster {CasterId}", alert.CasterId);
            throw;
        }
    }

    public async Task SaveTelemetryAsync(RebarCastingTelemetry telemetry)
    {
        try
        {
            telemetry.Timestamp = DateTime.UtcNow;
            await _telemetryContainer.CreateItemAsync(telemetry, new PartitionKey(telemetry.CasterId));
            
            // Update caster status from telemetry
            await UpdateCasterStatusFromTelemetry(telemetry);
            
            // Check for alerts
            await CheckForRebarAlertsAsync(telemetry);
            
            _logger.LogDebug("Saved telemetry for caster {CasterId} - Grade: {Grade}", 
                telemetry.CasterId, telemetry.CurrentGrade);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving telemetry for caster {CasterId}", telemetry.CasterId);
            throw;
        }
    }

    private async Task UpdateCasterStatusFromTelemetry(RebarCastingTelemetry telemetry)
    {
        var status = await GetCasterStatusAsync(telemetry.CasterId) ?? 
                    new RebarCastingStatus { CasterId = telemetry.CasterId };
        
        status.TargetGrade = telemetry.CurrentGrade;
        status.BilletSize = telemetry.CurrentBilletSize;
        status.TundishTemperature = telemetry.TundishTemperature;
        status.MoldTemperature = telemetry.MoldTemperature;
        status.CastingSpeed = telemetry.CastingSpeed;
        status.LiquidLevel = telemetry.LiquidLevel;
        status.SteelFlow = telemetry.SteelFlow;
        status.CarbonContent = telemetry.CarbonContent;
        status.ManganeseContent = telemetry.ManganeseContent;
        status.PhosphorusContent = telemetry.PhosphorusContent;
        status.SulfurContent = telemetry.SulfurContent;
        status.BilletWidth = telemetry.BilletWidth;
        status.BilletLength = telemetry.BilletThickness;
        status.SurfaceQualityIndex = 100 - (telemetry.SurfaceDefectCount * 10);
        status.LastUpdated = DateTime.UtcNow;
        
        await _statusContainer.UpsertItemAsync(status, new PartitionKey(status.CasterId));
        _statusCache.AddOrUpdate(status.CasterId, status, (key, oldValue) => status);
    }
    
    private async Task CheckForRebarAlertsAsync(RebarCastingTelemetry telemetry)
    {
        var alerts = new List<RebarCastingAlert>();
        
        // Chemical composition alerts
        var spec = RebarSpecifications.GradeSpecs[telemetry.CurrentGrade];
        
        if (telemetry.CarbonContent > spec.MaxCarbon * 1.1)
        {
            alerts.Add(new RebarCastingAlert
            {
                CasterId = telemetry.CasterId,
                AffectedGrade = telemetry.CurrentGrade,
                AffectedBilletSize = telemetry.CurrentBilletSize,
                AlertType = RebarCastingAlert.AlertTypes.ChemicalCompositionOutOfSpec,
                Severity = "High",
                Description = $"Carbon content {telemetry.CarbonContent:F3}% exceeds maximum {spec.MaxCarbon:F3}%",
                CarbonContentReading = telemetry.CarbonContent
            });
        }
        
        // Billet dimension alerts
        var targetSize = (double)telemetry.CurrentBilletSize;
        if (Math.Abs(telemetry.BilletWidth - targetSize) > 2.0)
        {
            alerts.Add(new RebarCastingAlert
            {
                CasterId = telemetry.CasterId,
                AffectedGrade = telemetry.CurrentGrade,
                AffectedBilletSize = telemetry.CurrentBilletSize,
                AlertType = RebarCastingAlert.AlertTypes.BilletSizeDeviation,
                Severity = "Medium",
                Description = $"Billet width {telemetry.BilletWidth:F1}mm deviates from target {targetSize:F1}mm",
                BilletWidthDeviation = Math.Abs(telemetry.BilletWidth - targetSize)
            });
        }
        
        // Surface defect alerts
        if (telemetry.SurfaceDefectCount > 5)
        {
            alerts.Add(new RebarCastingAlert
            {
                CasterId = telemetry.CasterId,
                AffectedGrade = telemetry.CurrentGrade,
                AffectedBilletSize = telemetry.CurrentBilletSize,
                AlertType = RebarCastingAlert.AlertTypes.SurfaceDefectThresholdExceeded,
                Severity = "High",
                Description = $"Surface defect count {telemetry.SurfaceDefectCount} exceeds threshold",
                SurfaceDefectCount = telemetry.SurfaceDefectCount
            });
        }
        
        // Crack detection alerts
        if (telemetry.CrackDetectionScore > 0.7)
        {
            alerts.Add(new RebarCastingAlert
            {
                CasterId = telemetry.CasterId,
                AffectedGrade = telemetry.CurrentGrade,
                AffectedBilletSize = telemetry.CurrentBilletSize,
                AlertType = RebarCastingAlert.AlertTypes.CrackDetected,
                Severity = "Critical",
                Description = $"Potential crack detected (confidence: {telemetry.CrackDetectionScore:P1})",
                CrackDetectionScore = telemetry.CrackDetectionScore
            });
        }
        
        // Save all alerts
        foreach (var alert in alerts)
        {
            await CreateCastingAlertAsync(alert);
        }
    }
    
    public async Task<IEnumerable<BilletQualityInspection>> GetBilletInspectionsAsync(string casterId, RebarGrade? grade = null)
    {
        try
        {
            var queryText = "SELECT * FROM c WHERE c.CasterId = @casterId";
            if (grade.HasValue)
                queryText += " AND c.Grade = @grade";
            queryText += " ORDER BY c.InspectedAt DESC";
            
            var queryDefinition = new QueryDefinition(queryText).WithParameter("@casterId", casterId);
            if (grade.HasValue)
                queryDefinition.WithParameter("@grade", (int)grade.Value);
            
            var results = new List<BilletQualityInspection>();
            using var iterator = _inspectionContainer.GetItemQueryIterator<BilletQualityInspection>(queryDefinition);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving billet inspections for caster {CasterId}", casterId);
            throw;
        }
    }
    
    public async Task SaveBilletInspectionAsync(BilletQualityInspection inspection)
    {
        try
        {
            inspection.InspectedAt = DateTime.UtcNow;
            await _inspectionContainer.CreateItemAsync(inspection, new PartitionKey(inspection.CasterId));
            
            _logger.LogInformation("Saved billet inspection {InspectionId} for billet {BilletId} - Grade: {Grade}",
                inspection.Id, inspection.BilletId, inspection.OverallGrade);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving billet inspection for billet {BilletId}", inspection.BilletId);
            throw;
        }
    }
    
    public async Task<RebarProductionSchedule?> GetProductionScheduleAsync(string casterId)
    {
        try
        {
            var queryText = "SELECT * FROM c WHERE c.CasterId = @casterId AND c.ProductionStatus IN ('Scheduled', 'InProgress') ORDER BY c.ScheduledStart ASC";
            var queryDefinition = new QueryDefinition(queryText).WithParameter("@casterId", casterId);
            
            using var iterator = _scheduleContainer.GetItemQueryIterator<RebarProductionSchedule>(queryDefinition);
            
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                return response.FirstOrDefault();
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving production schedule for caster {CasterId}", casterId);
            throw;
        }
    }
    
    public async Task UpdateProductionScheduleAsync(RebarProductionSchedule schedule)
    {
        try
        {
            await _scheduleContainer.UpsertItemAsync(schedule, new PartitionKey(schedule.CasterId));
            
            _logger.LogInformation("Updated production schedule {ScheduleId} for caster {CasterId}",
                schedule.Id, schedule.CasterId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating production schedule for caster {CasterId}", schedule.CasterId);
            throw;
        }
    }
    
    public async Task<IEnumerable<RebarCastingAlert>> GetActiveAlertsAsync(string? casterId = null)
    {
        try
        {
            var queryText = "SELECT * FROM c WHERE c.IsResolved = false";
            if (!string.IsNullOrEmpty(casterId))
                queryText += " AND c.CasterId = @casterId";
            queryText += " ORDER BY c.CreatedAt DESC";
            
            var queryDefinition = new QueryDefinition(queryText);
            if (!string.IsNullOrEmpty(casterId))
                queryDefinition.WithParameter("@casterId", casterId);
            
            var results = new List<RebarCastingAlert>();
            using var iterator = _alertsContainer.GetItemQueryIterator<RebarCastingAlert>(queryDefinition);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active alerts");
            throw;
        }
    }
    
    public async Task ResolveAlertAsync(string alertId, string resolutionNotes)
    {
        try
        {
            // Find the alert by scanning (since we don't have the partition key)
            var queryText = "SELECT * FROM c WHERE c.Id = @alertId";
            var queryDefinition = new QueryDefinition(queryText).WithParameter("@alertId", alertId);
            
            using var iterator = _alertsContainer.GetItemQueryIterator<RebarCastingAlert>(queryDefinition);
            
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var alert = response.FirstOrDefault();
                
                if (alert != null)
                {
                    alert.IsResolved = true;
                    alert.ResolvedAt = DateTime.UtcNow;
                    alert.ResolutionNotes = resolutionNotes;
                    
                    await _alertsContainer.UpsertItemAsync(alert, new PartitionKey(alert.CasterId));
                    
                    _logger.LogInformation("Resolved alert {AlertId} for caster {CasterId}",
                        alertId, alert.CasterId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving alert {AlertId}", alertId);
            throw;
        }
    }
    
    // Calculation methods for rebar-specific metrics
    private double CalculateRebarCastingEfficiency(IEnumerable<RebarCastingTelemetry> telemetry)
    {
        var avgSpeed = telemetry.Average(t => t.CastingSpeed);
        var avgFlow = telemetry.Average(t => t.SteelFlow);
        var targetFlow = 120.0; // Target flow rate for rebar casting
        
        var speedEfficiency = Math.Min(100, (avgSpeed / 1.5) * 100);
        var flowEfficiency = Math.Min(100, (avgFlow / targetFlow) * 100);
        
        return (speedEfficiency + flowEfficiency) / 2;
    }
    
    private double CalculateRebarQualityIndex(IEnumerable<RebarCastingTelemetry> telemetry)
    {
        var tempStability = CalculateTemperatureStability(telemetry);
        var vibrationScore = 100 - (telemetry.Average(t => t.VibrationLevel) * 10);
        var surfaceQuality = 100 - (telemetry.Average(t => t.SurfaceDefectCount) * 5);
        var straightness = telemetry.Average(t => 100 - (t.BilletStraightness * 100));
        
        return (tempStability + vibrationScore + surfaceQuality + straightness) / 4;
    }
    
    private double CalculateChemicalComplianceRate(IEnumerable<RebarCastingTelemetry> telemetry, RebarGrade grade)
    {
        var spec = RebarSpecifications.GradeSpecs[grade];
        var compliantCount = 0;
        var totalCount = 0;
        
        foreach (var t in telemetry)
        {
            totalCount++;
            if (t.CarbonContent <= spec.MaxCarbon &&
                t.ManganeseContent <= spec.MaxManganese &&
                t.PhosphorusContent <= spec.MaxPhosphorus &&
                t.SulfurContent <= spec.MaxSulfur)
            {
                compliantCount++;
            }
        }
        
        return totalCount > 0 ? (double)compliantCount / totalCount * 100 : 100;
    }
    
    private double CalculateDimensionalAccuracy(IEnumerable<RebarCastingTelemetry> telemetry, BilletSize targetSize)
    {
        var target = (double)targetSize;
        var deviations = telemetry.Select(t => Math.Abs(t.BilletWidth - target)).ToArray();
        var avgDeviation = deviations.Length > 0 ? deviations.Average() : 0;
        
        return Math.Max(0, 100 - (avgDeviation * 10));
    }
    
    private double CalculateSurfaceQualityScore(IEnumerable<RebarCastingTelemetry> telemetry)
    {
        var avgDefects = telemetry.Average(t => t.SurfaceDefectCount);
        return Math.Max(0, 100 - (avgDefects * 8));
    }
    
    private double CalculateBilletStraightnessIndex(IEnumerable<RebarCastingTelemetry> telemetry)
    {
        var avgStraightness = telemetry.Average(t => t.BilletStraightness);
        return Math.Max(0, 100 - (avgStraightness * 50));
    }
    
    private double CalculateYieldRatio(IEnumerable<RebarCastingTelemetry> telemetry)
    {
        // Simplified yield calculation based on defect rates
        var defectRate = telemetry.Average(t => t.SurfaceDefectCount) / 100.0;
        var crackRate = telemetry.Average(t => t.CrackDetectionScore);
        
        return Math.Max(0, Math.Min(100, 100 - (defectRate * 10) - (crackRate * 20)));
    }
    
    private double CalculateTemperatureStability(IEnumerable<RebarCastingTelemetry> telemetry)
    {
        var tempVariance = telemetry.Select(t => t.TundishTemperature).ToArray().Variance();
        return Math.Max(0, Math.Min(100, 100 - (tempVariance / 100)));
    }
    
    private double CalculateCoolingEfficiency(IEnumerable<RebarCastingTelemetry> telemetry)
    {
        var avgCoolingFlow = telemetry.Average(t => t.MoldCoolingWaterFlow);
        var avgMoldTemp = telemetry.Average(t => t.MoldTemperature);
        var targetTemp = 1200.0;
        
        var tempEfficiency = Math.Max(0, 1.0 - Math.Abs(avgMoldTemp - targetTemp) / targetTemp);
        var flowEfficiency = Math.Min(1.0, avgCoolingFlow / 300.0); // Target 300 L/min
        
        return Math.Min(100, (tempEfficiency + flowEfficiency) / 2 * 100);
    }
}

public static class ArrayExtensions
{
    public static double Variance(this double[] values)
    {
        if (values.Length == 0)
            return 0;

        var mean = values.Average();
        return values.Select(x => Math.Pow(x - mean, 2)).Average();
    }
}