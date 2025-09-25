using Microsoft.Azure.Cosmos;
using RollingMillService.Models;
using System.Collections.Concurrent;

namespace RollingMillService.Services;

public interface IRebarRollingDataService
{
    Task<RebarRollingStatus?> GetMillStatusAsync(string millId);
    Task<IEnumerable<RebarRollingTelemetry>> GetMillTelemetryAsync(string millId, DateTime? startTime, DateTime? endTime, RollingStand? stand = null, RebarGrade? grade = null);
    Task UpdateRollingConfigurationAsync(string millId, RebarRollingConfiguration config);
    Task<RebarRollingPerformanceMetrics> GetPerformanceMetricsAsync(string millId, DateTime? from = null, DateTime? to = null);
    Task<RebarRollingAlert> CreateRollingAlertAsync(RebarRollingAlert alert);
    Task<IEnumerable<RebarRollingAlert>> GetMillAlertsAsync(string millId, bool includeResolved = false);
    Task<ServiceResult> ResolveAlertAsync(string alertId, string resolutionAction, string operatorId);
    Task ProcessTelemetryAsync(RebarRollingTelemetry telemetry);
    Task<RibbingSpecification> GetRibbingSpecificationAsync(RebarSize size, RibbingPattern pattern);
    Task<ServiceResult> ValidateRibbingQualityAsync(string millId, string heatNumber, Dictionary<string, double> measurements);
}

public class RebarRollingDataService : IRebarRollingDataService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _statusContainer;
    private readonly Container _telemetryContainer;
    private readonly Container _configContainer;
    private readonly Container _metricsContainer;
    private readonly Container _alertsContainer;
    private readonly ILogger<RebarRollingDataService> _logger;
    
    // In-memory cache for real-time operations
    private readonly ConcurrentDictionary<string, RebarRollingStatus> _statusCache = new();
    private readonly ConcurrentDictionary<string, RebarRollingConfiguration> _configCache = new();

    public RebarRollingDataService(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<RebarRollingDataService> logger
    )
    {
        _cosmosClient = cosmosClient;
        _logger = logger;

        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "SteelMillRebarDB";
        
        _statusContainer = _cosmosClient.GetContainer(databaseName, "RebarRollingStatus");
        _telemetryContainer = _cosmosClient.GetContainer(databaseName, "RebarRollingTelemetry");
        _configContainer = _cosmosClient.GetContainer(databaseName, "RebarRollingConfiguration");
        _metricsContainer = _cosmosClient.GetContainer(databaseName, "RollingPerformanceMetrics");
        _alertsContainer = _cosmosClient.GetContainer(databaseName, "RebarRollingAlerts");
    }

    public async Task<RebarRollingStatus?> GetMillStatusAsync(string millId)
    {
        try
        {
            // Check cache first
            if (_statusCache.TryGetValue(millId, out var cachedStatus))
            {
                return cachedStatus;
            }

            var response = await _statusContainer.ReadItemAsync<RebarRollingStatus>(
                millId, new PartitionKey(millId)
            );
            
            var status = response.Resource;
            _statusCache.TryAdd(millId, status);
            return status;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Rolling mill status not found for {MillId}", millId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving mill status for {MillId}", millId);
            throw;
        }
    }

    public async Task<IEnumerable<RebarRollingTelemetry>> GetMillTelemetryAsync(
        string millId, DateTime? startTime, DateTime? endTime, RollingStand? stand = null, RebarGrade? grade = null)
    {
        try
        {
            var queryText = "SELECT * FROM c WHERE c.MillId = @millId";

            if (startTime.HasValue)
                queryText += " AND c.Timestamp >= @startTime";
            if (endTime.HasValue)
                queryText += " AND c.Timestamp <= @endTime";
            if (stand.HasValue)
                queryText += " AND c.StandPosition = @stand";
            if (grade.HasValue)
                queryText += " AND c.Grade = @grade";

            queryText += " ORDER BY c.Timestamp DESC";

            var queryDefinition = new QueryDefinition(queryText).WithParameter("@millId", millId);

            if (startTime.HasValue)
                queryDefinition.WithParameter("@startTime", startTime.Value);
            if (endTime.HasValue)
                queryDefinition.WithParameter("@endTime", endTime.Value);
            if (stand.HasValue)
                queryDefinition.WithParameter("@stand", (int)stand.Value);
            if (grade.HasValue)
                queryDefinition.WithParameter("@grade", (int)grade.Value);

            var results = new List<RebarRollingTelemetry>();
            using var iterator = _telemetryContainer.GetItemQueryIterator<RebarRollingTelemetry>(queryDefinition);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving telemetry for mill {MillId}", millId);
            throw;
        }
    }

    public async Task UpdateRollingConfigurationAsync(string millId, RebarRollingConfiguration config)
    {
        try
        {
            config.MillId = millId;
            await _configContainer.UpsertItemAsync(config, new PartitionKey(millId));
            _configCache.AddOrUpdate(millId, config, (key, oldValue) => config);

            // Update mill status with new configuration
            var status = await GetMillStatusAsync(millId) ?? 
                        new RebarRollingStatus { MillId = millId };

            status.CurrentGrade = config.TargetGrade;
            status.CurrentSize = config.TargetSize;
            status.CurrentPattern = config.RibbingPattern;
            status.LastUpdated = DateTime.UtcNow;

            await _statusContainer.UpsertItemAsync(status, new PartitionKey(millId));
            _statusCache.AddOrUpdate(millId, status, (key, oldValue) => status);

            _logger.LogInformation("Updated rolling configuration for mill {MillId} - Grade: {Grade}, Size: {Size}, Pattern: {Pattern}", 
                millId, config.TargetGrade, config.TargetSize, config.RibbingPattern);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration for mill {MillId}", millId);
            throw;
        }
    }

    public async Task<RebarRollingPerformanceMetrics> GetPerformanceMetricsAsync(string millId, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var fromDate = from ?? DateTime.UtcNow.AddHours(-24);
            var toDate = to ?? DateTime.UtcNow;

            var telemetryData = await GetMillTelemetryAsync(millId, fromDate, toDate);

            var metrics = new RebarRollingPerformanceMetrics
            {
                MillId = millId,
                CalculatedAt = DateTime.UtcNow,
                CalculationPeriod = toDate - fromDate
            };

            if (telemetryData.Any())
            {
                // Production metrics
                var telemetryList = telemetryData.ToList();
                metrics.ThroughputRate = CalculateThroughputRate(telemetryList);
                metrics.BarsRolled = CalculateBarsRolled(telemetryList);
                metrics.TotalTonnage = metrics.BarsRolled * metrics.AverageBarWeight / 1000.0;

                // Quality metrics
                metrics.OverallQualityScore = CalculateQualityScore(telemetryList);
                metrics.DimensionalAccuracy = CalculateDimensionalAccuracy(telemetryList);
                metrics.RibbingQuality = CalculateRibbingQuality(telemetryList);
                metrics.SurfaceQuality = CalculateSurfaceQuality(telemetryList);

                // Energy metrics
                metrics.TotalEnergyConsumption = CalculateEnergyConsumption(telemetryList);
                metrics.EnergyPerTon = metrics.TotalTonnage > 0 ? metrics.TotalEnergyConsumption / metrics.TotalTonnage : 0;

                // Equipment metrics
                metrics.EquipmentUtilization = CalculateEquipmentUtilization(telemetryList);
                metrics.AvgVibrationLevel = CalculateAverageVibration(telemetryList);

                // Process stability
                metrics.ProcessStability = CalculateProcessStability(telemetryList);
                
                // Grade distribution
                foreach (var gradeGroup in telemetryList.GroupBy(t => t.Grade))
                {
                    var gradeData = gradeGroup.ToList();
                    metrics.TonnageByGrade[gradeGroup.Key] = gradeData.Count * metrics.AverageBarWeight / 1000.0;
                }
            }

            await _metricsContainer.CreateItemAsync(metrics, new PartitionKey(millId));
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating performance metrics for mill {MillId}", millId);
            throw;
        }
    }

    public async Task<RebarRollingAlert> CreateRollingAlertAsync(RebarRollingAlert alert)
    {
        try
        {
            alert.CreatedAt = DateTime.UtcNow;
            await _alertsContainer.CreateItemAsync(alert, new PartitionKey(alert.MillId));
            
            _logger.LogWarning("Rolling alert created for mill {MillId}: {AlertType} - {Description}",
                alert.MillId, alert.AlertType, alert.Description);
            
            return alert;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating rolling alert");
            throw;
        }
    }

    public async Task<IEnumerable<RebarRollingAlert>> GetMillAlertsAsync(string millId, bool includeResolved = false)
    {
        try
        {
            var queryText = includeResolved ? 
                "SELECT * FROM c WHERE c.MillId = @millId ORDER BY c.CreatedAt DESC" :
                "SELECT * FROM c WHERE c.MillId = @millId AND c.IsResolved = false ORDER BY c.CreatedAt DESC";

            var queryDefinition = new QueryDefinition(queryText).WithParameter("@millId", millId);

            var results = new List<RebarRollingAlert>();
            using var iterator = _alertsContainer.GetItemQueryIterator<RebarRollingAlert>(queryDefinition);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alerts for mill {MillId}", millId);
            throw;
        }
    }

    public async Task<ServiceResult> ResolveAlertAsync(string alertId, string resolutionAction, string operatorId)
    {
        try
        {
            // Find alert by scanning
            var queryText = "SELECT * FROM c WHERE c.Id = @alertId";
            var queryDefinition = new QueryDefinition(queryText).WithParameter("@alertId", alertId);
            
            using var iterator = _alertsContainer.GetItemQueryIterator<RebarRollingAlert>(queryDefinition);
            
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var alert = response.FirstOrDefault();
                
                if (alert != null)
                {
                    alert.IsResolved = true;
                    alert.ResolvedAt = DateTime.UtcNow;
                    alert.ResolutionAction = resolutionAction;
                    alert.OperatorId = operatorId;
                    
                    await _alertsContainer.UpsertItemAsync(alert, new PartitionKey(alert.MillId));
                    
                    return new ServiceResult { Success = true, Message = "Alert resolved successfully" };
                }
            }
            
            return new ServiceResult { Success = false, Message = "Alert not found" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving alert {AlertId}", alertId);
            throw;
        }
    }

    public async Task ProcessTelemetryAsync(RebarRollingTelemetry telemetry)
    {
        try
        {
            await _telemetryContainer.CreateItemAsync(telemetry, new PartitionKey(telemetry.MillId));
            
            // Update mill status from telemetry
            await UpdateMillStatusFromTelemetry(telemetry);
            
            // Check for alerts based on telemetry
            await CheckTelemetryForAlerts(telemetry);
            
            _logger.LogDebug("Processed telemetry for mill {MillId}, stand {Stand}", 
                telemetry.MillId, telemetry.StandPosition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing telemetry for mill {MillId}", telemetry.MillId);
            throw;
        }
    }

    public async Task<RibbingSpecification> GetRibbingSpecificationAsync(RebarSize size, RibbingPattern pattern)
    {
        return await Task.FromResult(RibbingSpecification.GetStandardSpec(size, pattern));
    }

    public async Task<ServiceResult> ValidateRibbingQualityAsync(string millId, string heatNumber, Dictionary<string, double> measurements)
    {
        try
        {
            var issues = new List<string>();
            
            // Get current configuration to determine specifications
            var config = _configCache.GetValueOrDefault(millId);
            if (config == null)
            {
                return new ServiceResult { Success = false, Message = "Mill configuration not found" };
            }

            var ribSpec = await GetRibbingSpecificationAsync(config.TargetSize, config.RibbingPattern);

            // Validate rib height
            if (measurements.TryGetValue("RibHeight", out var ribHeight))
            {
                var deviation = Math.Abs(ribHeight - ribSpec.NominalRibHeight);
                if (deviation > ribSpec.RibHeightTolerance)
                {
                    issues.Add($"Rib height deviation: {deviation:F2}mm (tolerance: ±{ribSpec.RibHeightTolerance:F2}mm)");
                }
            }

            // Validate rib spacing
            if (measurements.TryGetValue("RibSpacing", out var ribSpacing))
            {
                var deviation = Math.Abs(ribSpacing - ribSpec.RibSpacing);
                if (deviation > ribSpec.RibSpacing * 0.1) // 10% tolerance
                {
                    issues.Add($"Rib spacing deviation: {deviation:F2}mm");
                }
            }

            // Validate dimensional accuracy
            if (measurements.TryGetValue("Diameter", out var diameter))
            {
                var targetDiameter = (double)config.TargetSize;
                var deviation = Math.Abs(diameter - targetDiameter);
                if (deviation > config.DiameterTolerance)
                {
                    issues.Add($"Diameter deviation: {deviation:F2}mm (tolerance: ±{config.DiameterTolerance:F2}mm)");
                }
            }

            var qualityPassed = !issues.Any();
            
            if (!qualityPassed)
            {
                // Create quality alert
                await CreateRollingAlertAsync(new RebarRollingAlert
                {
                    MillId = millId,
                    CurrentHeatNumber = heatNumber,
                    AffectedGrade = config.TargetGrade,
                    AffectedSize = config.TargetSize,
                    AlertType = RebarRollingAlert.AlertTypes.RibbingQualityIssue,
                    Severity = "High",
                    Description = $"Ribbing quality issues detected: {string.Join(", ", issues)}"
                });
            }

            return new ServiceResult 
            { 
                Success = true, 
                Message = qualityPassed ? "Ribbing quality validated successfully" : $"Quality issues found: {string.Join(", ", issues)}",
                Data = new { QualityPassed = qualityPassed, Issues = issues }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating ribbing quality for mill {MillId}", millId);
            throw;
        }
    }

    private async Task UpdateMillStatusFromTelemetry(RebarRollingTelemetry telemetry)
    {
        var status = await GetMillStatusAsync(telemetry.MillId) ?? 
                    new RebarRollingStatus { MillId = telemetry.MillId };

        status.CurrentGrade = telemetry.Grade;
        status.CurrentSize = telemetry.Size;
        status.CurrentPattern = telemetry.Pattern;
        status.CurrentHeatNumber = telemetry.CurrentHeatNumber;
        status.BilletTemperature = telemetry.BilletTemperature;
        status.RollingSpeed = telemetry.RollingSpeed;
        status.PowerConsumption = telemetry.PowerConsumption;
        status.LastUpdated = DateTime.UtcNow;

        // Update stand-specific status
        var standStatus = status.RollingStands.FirstOrDefault(s => s.StandType == telemetry.StandPosition);
        if (standStatus == null)
        {
            standStatus = new RollingStandStatus { StandType = telemetry.StandPosition };
            status.RollingStands.Add(standStatus);
        }

        standStatus.MotorTorque = telemetry.MotorTorque;
        standStatus.MotorCurrent = telemetry.MotorCurrent;
        standStatus.RollGap = telemetry.RollGap;
        standStatus.RollPressure = telemetry.RollPressure;
        standStatus.VibrationLevel = Math.Sqrt(telemetry.VibrationX * telemetry.VibrationX + 
                                              telemetry.VibrationY * telemetry.VibrationY + 
                                              telemetry.VibrationZ * telemetry.VibrationZ);

        await _statusContainer.UpsertItemAsync(status, new PartitionKey(status.MillId));
        _statusCache.AddOrUpdate(status.MillId, status, (key, oldValue) => status);
    }

    private async Task CheckTelemetryForAlerts(RebarRollingTelemetry telemetry)
    {
        var alerts = new List<RebarRollingAlert>();

        // Vibration alert
        var totalVibration = Math.Sqrt(telemetry.VibrationX * telemetry.VibrationX + 
                                      telemetry.VibrationY * telemetry.VibrationY + 
                                      telemetry.VibrationZ * telemetry.VibrationZ);
        if (totalVibration > 15.0) // 15 mm/s RMS threshold
        {
            alerts.Add(new RebarRollingAlert
            {
                MillId = telemetry.MillId,
                AffectedStand = telemetry.StandPosition,
                CurrentHeatNumber = telemetry.CurrentHeatNumber,
                AffectedGrade = telemetry.Grade,
                AffectedSize = telemetry.Size,
                AlertType = RebarRollingAlert.AlertTypes.HighVibration,
                Severity = totalVibration > 25.0 ? "Critical" : "High",
                Description = $"High vibration detected: {totalVibration:F1} mm/s RMS",
                VibrationReading = totalVibration
            });
        }

        // Temperature alert
        if (telemetry.BilletTemperature > 1200 || telemetry.BilletTemperature < 900)
        {
            alerts.Add(new RebarRollingAlert
            {
                MillId = telemetry.MillId,
                AffectedStand = telemetry.StandPosition,
                AlertType = RebarRollingAlert.AlertTypes.OverTemperature,
                Severity = telemetry.BilletTemperature > 1250 ? "Critical" : "High",
                Description = $"Billet temperature out of range: {telemetry.BilletTemperature:F1}°C",
                TemperatureReading = telemetry.BilletTemperature
            });
        }

        // Dimensional deviation alert
        if (telemetry.DimensionalTolerance > 1.0) // 1mm tolerance
        {
            alerts.Add(new RebarRollingAlert
            {
                MillId = telemetry.MillId,
                AffectedStand = telemetry.StandPosition,
                AlertType = RebarRollingAlert.AlertTypes.DimensionalDeviation,
                Severity = "Medium",
                Description = $"Dimensional deviation: {telemetry.DimensionalTolerance:F2}mm",
                DimensionReading = telemetry.DimensionalTolerance
            });
        }

        // Ribbing quality alert (for ribbing stands)
        if (telemetry.StandPosition == RollingStand.RibbingStand && telemetry.RibConsistency < 90.0)
        {
            alerts.Add(new RebarRollingAlert
            {
                MillId = telemetry.MillId,
                AffectedStand = telemetry.StandPosition,
                AlertType = RebarRollingAlert.AlertTypes.RibbingQualityIssue,
                Severity = "High",
                Description = $"Poor rib consistency: {telemetry.RibConsistency:F1}%",
                QualityScore = telemetry.RibConsistency
            });
        }

        // Save all alerts
        foreach (var alert in alerts)
        {
            await CreateRollingAlertAsync(alert);
        }
    }

    // Performance calculation methods
    private double CalculateThroughputRate(List<RebarRollingTelemetry> telemetry)
    {
        if (!telemetry.Any()) return 0;
        return telemetry.Where(t => t.StandPosition == RollingStand.FinishingStand)
                       .Average(t => t.RollingSpeed * 0.1); // Approximate tons/hour
    }

    private int CalculateBarsRolled(List<RebarRollingTelemetry> telemetry)
    {
        return telemetry.Where(t => t.StandPosition == RollingStand.FinishingStand).Count();
    }

    private double CalculateQualityScore(List<RebarRollingTelemetry> telemetry)
    {
        if (!telemetry.Any()) return 0;
        
        var dimensionalScore = 100 - telemetry.Average(t => Math.Min(t.DimensionalTolerance * 10, 50));
        var ribbingScore = telemetry.Where(t => t.StandPosition == RollingStand.RibbingStand)
                                  .DefaultIfEmpty()
                                  .Average(t => t?.RibConsistency ?? 95);
        var surfaceScore = 100 - telemetry.Average(t => Math.Min(t.SurfaceDefects * 5, 30));
        
        return (dimensionalScore + ribbingScore + surfaceScore) / 3.0;
    }

    private double CalculateDimensionalAccuracy(List<RebarRollingTelemetry> telemetry)
    {
        if (!telemetry.Any()) return 0;
        return telemetry.Count(t => t.DimensionalTolerance <= 0.5) * 100.0 / telemetry.Count;
    }

    private double CalculateRibbingQuality(List<RebarRollingTelemetry> telemetry)
    {
        var ribbingData = telemetry.Where(t => t.StandPosition == RollingStand.RibbingStand).ToList();
        return ribbingData.Any() ? ribbingData.Average(t => t.RibConsistency) : 100.0;
    }

    private double CalculateSurfaceQuality(List<RebarRollingTelemetry> telemetry)
    {
        if (!telemetry.Any()) return 0;
        return 100.0 - telemetry.Average(t => Math.Min(t.SurfaceDefects * 5, 50));
    }

    private double CalculateEnergyConsumption(List<RebarRollingTelemetry> telemetry)
    {
        return telemetry.Sum(t => t.PowerConsumption * 0.001); // Convert to kWh (assuming 1-minute intervals)
    }

    private double CalculateEquipmentUtilization(List<RebarRollingTelemetry> telemetry)
    {
        if (!telemetry.Any()) return 0;
        var operatingTime = telemetry.Count(t => t.RollingSpeed > 0.5);
        return operatingTime * 100.0 / telemetry.Count;
    }

    private double CalculateAverageVibration(List<RebarRollingTelemetry> telemetry)
    {
        if (!telemetry.Any()) return 0;
        return telemetry.Average(t => Math.Sqrt(t.VibrationX * t.VibrationX + 
                                               t.VibrationY * t.VibrationY + 
                                               t.VibrationZ * t.VibrationZ));
    }

    private double CalculateProcessStability(List<RebarRollingTelemetry> telemetry)
    {
        if (telemetry.Count < 2) return 100;
        
        var speedVariance = telemetry.Select(t => t.RollingSpeed).ToArray().Variance();
        var forceVariance = telemetry.Select(t => t.RollForce).ToArray().Variance();
        var tempVariance = telemetry.Select(t => t.BilletTemperature).ToArray().Variance();
        
        // Lower variance = higher stability
        var avgVariance = (speedVariance / 100 + forceVariance / 10000 + tempVariance / 10000) / 3;
        return Math.Max(0, 100 - avgVariance * 10);
    }
}

public class ServiceResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}

public static class ArrayExtensions
{
    public static double Variance(this double[] values)
    {
        if (values.Length <= 1) return 0;
        var mean = values.Average();
        return values.Select(x => Math.Pow(x - mean, 2)).Average();
    }
}