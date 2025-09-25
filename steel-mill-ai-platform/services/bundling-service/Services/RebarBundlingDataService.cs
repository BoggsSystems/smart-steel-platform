using Microsoft.Azure.Cosmos;
using BundlingService.Models;
using System.Collections.Concurrent;

namespace BundlingService.Services;

public interface IRebarBundlingDataService
{
    Task<RebarBundlingStatus?> GetLineStatusAsync(string lineId);
    Task<IEnumerable<RebarBundlingTelemetry>> GetLineTelemetryAsync(string lineId, DateTime? startTime, DateTime? endTime, RebarGrade? grade = null, RebarSize? size = null);
    Task UpdateBundlingConfigurationAsync(string lineId, BundlingConfiguration config);
    Task<RebarBundle> CreateBundleFromBarsAsync(List<RebarBar> bars, string customerOrderId, string bundlingLineId);
    Task<RebarBundle?> GetBundleAsync(string bundleId);
    Task<IEnumerable<RebarBundle>> GetBundlesAsync(RebarGrade? grade = null, RebarSize? size = null, BundleStatus? status = null, string? customerOrderId = null, int limit = 100);
    Task<QualityVerificationResult> VerifyBundleQualityAsync(string bundleId, string inspectorId, Dictionary<string, bool> qualityChecks);
    Task<ServiceResult> CompleteBundleTyingAsync(string bundleId, string operatorId, TyingDetails tyingDetails);
    Task<MillTestCertificate?> GenerateMillTestCertificateAsync(string bundleId);
    Task<ServiceResult> MarkBundleShippedAsync(string bundleId, string destination, string instructions, string operatorId);
    Task<BundlingPerformanceMetrics> GetPerformanceMetricsAsync(string lineId, DateTime? from = null, DateTime? to = null);
    Task<RebarBundlingAlert> CreateBundlingAlertAsync(RebarBundlingAlert alert);
    Task<IEnumerable<RebarBundlingAlert>> GetLineAlertsAsync(string lineId, bool includeResolved = false);
    Task<ServiceResult> ResolveAlertAsync(string alertId, string resolutionAction, string operatorId);
    Task ProcessTelemetryAsync(RebarBundlingTelemetry telemetry);
}

public class RebarBundlingDataService : IRebarBundlingDataService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _statusContainer;
    private readonly Container _telemetryContainer;
    private readonly Container _bundleContainer;
    private readonly Container _alertContainer;
    private readonly Container _performanceContainer;
    private readonly Container _certificateContainer;
    private readonly ILogger<RebarBundlingDataService> _logger;
    
    // In-memory cache for real-time operations
    private readonly ConcurrentDictionary<string, RebarBundlingStatus> _statusCache = new();
    private readonly ConcurrentDictionary<string, BundlingConfiguration> _configCache = new();

    public RebarBundlingDataService(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<RebarBundlingDataService> logger
    )
    {
        _cosmosClient = cosmosClient;
        _logger = logger;

        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "SteelMillRebarDB";
        
        _statusContainer = _cosmosClient.GetContainer(databaseName, "RebarBundlingStatus");
        _telemetryContainer = _cosmosClient.GetContainer(databaseName, "RebarBundlingTelemetry");
        _bundleContainer = _cosmosClient.GetContainer(databaseName, "RebarBundles");
        _alertContainer = _cosmosClient.GetContainer(databaseName, "RebarBundlingAlerts");
        _performanceContainer = _cosmosClient.GetContainer(databaseName, "BundlingPerformanceMetrics");
        _certificateContainer = _cosmosClient.GetContainer(databaseName, "MillTestCertificates");
    }

    public async Task<RebarBundlingStatus?> GetLineStatusAsync(string lineId)
    {
        try
        {
            // Check cache first
            if (_statusCache.TryGetValue(lineId, out var cachedStatus))
            {
                return cachedStatus;
            }

            var response = await _statusContainer.ReadItemAsync<RebarBundlingStatus>(
                lineId, new PartitionKey(lineId)
            );
            
            var status = response.Resource;
            _statusCache.TryAdd(lineId, status);
            return status;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Bundling line status not found for {LineId}", lineId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving line status for {LineId}", lineId);
            throw;
        }
    }

    public async Task<IEnumerable<RebarBundlingTelemetry>> GetLineTelemetryAsync(
        string lineId, DateTime? startTime, DateTime? endTime, RebarGrade? grade = null, RebarSize? size = null)
    {
        try
        {
            var queryText = "SELECT * FROM c WHERE c.BundlingLineId = @lineId";

            if (startTime.HasValue)
                queryText += " AND c.Timestamp >= @startTime";
            if (endTime.HasValue)
                queryText += " AND c.Timestamp <= @endTime";
            if (grade.HasValue)
                queryText += " AND c.Grade = @grade";
            if (size.HasValue)
                queryText += " AND c.Size = @size";

            queryText += " ORDER BY c.Timestamp DESC";

            var queryDefinition = new QueryDefinition(queryText).WithParameter("@lineId", lineId);

            if (startTime.HasValue)
                queryDefinition.WithParameter("@startTime", startTime.Value);
            if (endTime.HasValue)
                queryDefinition.WithParameter("@endTime", endTime.Value);
            if (grade.HasValue)
                queryDefinition.WithParameter("@grade", (int)grade.Value);
            if (size.HasValue)
                queryDefinition.WithParameter("@size", (int)size.Value);

            var results = new List<RebarBundlingTelemetry>();
            using var iterator = _telemetryContainer.GetItemQueryIterator<RebarBundlingTelemetry>(queryDefinition);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving telemetry for line {LineId}", lineId);
            throw;
        }
    }

    public async Task UpdateBundlingConfigurationAsync(string lineId, BundlingConfiguration config)
    {
        try
        {
            var status = await GetLineStatusAsync(lineId) ?? 
                        new RebarBundlingStatus { BundlingLineId = lineId };

            status.CurrentGrade = config.TargetGrade;
            status.CurrentSize = config.TargetSize;
            status.CurrentLength = config.TargetLength;
            status.LastUpdated = DateTime.UtcNow;

            await _statusContainer.UpsertItemAsync(status, new PartitionKey(lineId));
            _statusCache.AddOrUpdate(lineId, status, (key, oldValue) => status);
            _configCache.AddOrUpdate(lineId, config, (key, oldValue) => config);

            _logger.LogInformation("Updated bundling configuration for line {LineId}", lineId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration for line {LineId}", lineId);
            throw;
        }
    }

    public async Task<RebarBundle> CreateBundleFromBarsAsync(List<RebarBar> bars, string customerOrderId, string bundlingLineId)
    {
        try
        {
            if (!bars.Any())
                throw new ArgumentException("Bars list cannot be empty");

            // Validate all bars have same grade, size, and length
            var firstBar = bars.First();
            if (!bars.All(b => b.Grade == firstBar.Grade && b.Size == firstBar.Size && b.Length == firstBar.Length))
            {
                throw new ArgumentException("All bars in a bundle must have the same grade, size, and length");
            }

            // Get bundle specification
            var spec = RebarBundle.GetBundleSpec(firstBar.Grade, firstBar.Size, firstBar.Length);
            
            if (bars.Count > spec.MaxBarsPerBundle)
            {
                throw new ArgumentException($"Bundle exceeds maximum bar count ({spec.MaxBarsPerBundle}) for this specification");
            }

            var bundle = new RebarBundle
            {
                CustomerOrderId = customerOrderId,
                BundlingLineId = bundlingLineId,
                Grade = firstBar.Grade,
                Size = firstBar.Size,
                Length = firstBar.Length,
                BarCount = bars.Count,
                Bars = bars,
                HeatNumbers = bars.Select(b => b.HeatNumber).ToHashSet(),
                TotalWeight = bars.Sum(b => b.Weight),
                CalculatedWeight = bars.Count * spec.WeightPerBar,
                Status = BundleStatus.Creating,
                CreatedAt = DateTime.UtcNow
            };

            // Calculate quality score
            bundle.QualityScore = CalculateBundleQualityScore(bundle);
            bundle.PassedQualityCheck = bundle.IsQualityCompliant();

            await _bundleContainer.CreateItemAsync(bundle, new PartitionKey(bundle.BundleId));

            _logger.LogInformation("Created bundle {BundleId} with {BarCount} bars", 
                bundle.BundleId, bundle.BarCount);

            return bundle;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bundle from bars");
            throw;
        }
    }

    public async Task<RebarBundle?> GetBundleAsync(string bundleId)
    {
        try
        {
            var response = await _bundleContainer.ReadItemAsync<RebarBundle>(bundleId, new PartitionKey(bundleId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bundle {BundleId}", bundleId);
            throw;
        }
    }

    public async Task<IEnumerable<RebarBundle>> GetBundlesAsync(
        RebarGrade? grade = null, RebarSize? size = null, BundleStatus? status = null, 
        string? customerOrderId = null, int limit = 100)
    {
        try
        {
            var queryText = "SELECT * FROM c WHERE 1=1";

            if (grade.HasValue)
                queryText += " AND c.Grade = @grade";
            if (size.HasValue)
                queryText += " AND c.Size = @size";
            if (status.HasValue)
                queryText += " AND c.Status = @status";
            if (!string.IsNullOrEmpty(customerOrderId))
                queryText += " AND c.CustomerOrderId = @customerOrderId";

            queryText += " ORDER BY c.CreatedAt DESC";

            var queryDefinition = new QueryDefinition(queryText);

            if (grade.HasValue)
                queryDefinition.WithParameter("@grade", (int)grade.Value);
            if (size.HasValue)
                queryDefinition.WithParameter("@size", (int)size.Value);
            if (status.HasValue)
                queryDefinition.WithParameter("@status", (int)status.Value);
            if (!string.IsNullOrEmpty(customerOrderId))
                queryDefinition.WithParameter("@customerOrderId", customerOrderId);

            var results = new List<RebarBundle>();
            using var iterator = _bundleContainer.GetItemQueryIterator<RebarBundle>(
                queryDefinition, requestOptions: new QueryRequestOptions { MaxItemCount = limit });
            
            while (iterator.HasMoreResults && results.Count < limit)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.Take(limit - results.Count));
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bundles");
            throw;
        }
    }

    public async Task<QualityVerificationResult> VerifyBundleQualityAsync(
        string bundleId, string inspectorId, Dictionary<string, bool> qualityChecks)
    {
        try
        {
            var bundle = await GetBundleAsync(bundleId);
            if (bundle == null)
            {
                return new QualityVerificationResult 
                { 
                    Success = false, 
                    Message = "Bundle not found" 
                };
            }

            var qualityIssues = new List<string>();

            // Weight verification
            if (!bundle.IsWithinWeightTolerance())
            {
                qualityIssues.Add($"Weight deviation: {Math.Abs(bundle.TotalWeight - bundle.CalculatedWeight):F1}kg");
            }

            // Bar quality verification
            var failedBars = bundle.Bars.Where(b => !b.PassedTensileTest || !b.PassedBendTest || b.HasSurfaceDefects).ToList();
            if (failedBars.Any())
            {
                qualityIssues.Add($"{failedBars.Count} bars failed quality tests");
            }

            // Quality score calculation
            var qualityScore = CalculateBundleQualityScore(bundle);
            var qualityPassed = qualityScore >= 85.0 && !qualityIssues.Any();

            // Update bundle
            bundle.QualityScore = qualityScore;
            bundle.PassedQualityCheck = qualityPassed;
            bundle.QualityInspectorId = inspectorId;
            bundle.QualityCheckedAt = DateTime.UtcNow;
            bundle.QualityIssues = qualityIssues;
            bundle.Status = qualityPassed ? BundleStatus.QualityCheck : BundleStatus.Rejected;

            await _bundleContainer.UpsertItemAsync(bundle, new PartitionKey(bundleId));

            return new QualityVerificationResult
            {
                Success = true,
                QualityPassed = qualityPassed,
                QualityScore = qualityScore,
                QualityIssues = qualityIssues
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying quality for bundle {BundleId}", bundleId);
            throw;
        }
    }

    public async Task<ServiceResult> CompleteBundleTyingAsync(string bundleId, string operatorId, TyingDetails tyingDetails)
    {
        try
        {
            var bundle = await GetBundleAsync(bundleId);
            if (bundle == null)
            {
                return new ServiceResult { Success = false, Message = "Bundle not found" };
            }

            if (bundle.Status != BundleStatus.QualityCheck)
            {
                return new ServiceResult 
                { 
                    Success = false, 
                    Message = $"Bundle is in {bundle.Status} status, cannot complete tying" 
                };
            }

            bundle.TyingPoints = tyingDetails.TyingPoints;
            bundle.TyingMaterial = tyingDetails.TyingMaterial;
            bundle.Status = BundleStatus.Tying;
            bundle.OperatorId = operatorId;

            if (tyingDetails.QualityApproved)
            {
                bundle.Status = BundleStatus.Tagging;
            }

            await _bundleContainer.UpsertItemAsync(bundle, new PartitionKey(bundleId));

            return new ServiceResult { Success = true, Message = "Bundle tying completed successfully" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing tying for bundle {BundleId}", bundleId);
            throw;
        }
    }

    public async Task<MillTestCertificate?> GenerateMillTestCertificateAsync(string bundleId)
    {
        try
        {
            var bundle = await GetBundleAsync(bundleId);
            if (bundle == null || !bundle.PassedQualityCheck)
            {
                return null;
            }

            var certificate = new MillTestCertificate
            {
                BundleId = bundleId,
                Grade = bundle.Grade,
                Size = bundle.Size,
                Length = bundle.Length,
                BarCount = bundle.BarCount,
                TotalWeight = bundle.TotalWeight,
                HeatNumbers = bundle.HeatNumbers.ToList(),
                ASTMCompliance = true,
                ASTMStandard = "A615",
                SteelMillName = "Steel Mill Rebar Platform",
                ProductionFacility = "Automated Rebar Production Facility"
            };

            certificate.CertificateNumber = certificate.GenerateCertificateNumber();

            // Get representative chemical and mechanical properties from first heat
            var firstHeat = bundle.HeatNumbers.First();
            certificate.CarbonContent = GetHeatChemistry(firstHeat, "Carbon");
            certificate.ManganeseContent = GetHeatChemistry(firstHeat, "Manganese");
            certificate.PhosphorusContent = GetHeatChemistry(firstHeat, "Phosphorus");
            certificate.SulfurContent = GetHeatChemistry(firstHeat, "Sulfur");

            // Set mechanical properties based on grade
            (certificate.YieldStrength, certificate.TensileStrength) = bundle.Grade switch
            {
                RebarGrade.Grade40 => (275, 420),
                RebarGrade.Grade60 => (420, 620),
                RebarGrade.Grade75 => (520, 690),
                RebarGrade.Grade80 => (550, 720),
                _ => (420, 620)
            };

            certificate.Elongation = 9.0; // Minimum elongation for rebar
            certificate.BendTestResult = 180.0; // Full bend without cracking

            await _certificateContainer.CreateItemAsync(certificate, new PartitionKey(certificate.BundleId));

            // Update bundle with certificate
            bundle.MillTestCertificate = certificate.CertificateNumber;
            bundle.Status = BundleStatus.Ready;
            await _bundleContainer.UpsertItemAsync(bundle, new PartitionKey(bundleId));

            return certificate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating MTC for bundle {BundleId}", bundleId);
            throw;
        }
    }

    public async Task<ServiceResult> MarkBundleShippedAsync(string bundleId, string destination, string instructions, string operatorId)
    {
        try
        {
            var bundle = await GetBundleAsync(bundleId);
            if (bundle == null)
            {
                return new ServiceResult { Success = false, Message = "Bundle not found" };
            }

            if (bundle.Status != BundleStatus.Ready)
            {
                return new ServiceResult 
                { 
                    Success = false, 
                    Message = $"Bundle is not ready for shipping (Status: {bundle.Status})" 
                };
            }

            bundle.Status = BundleStatus.Shipped;
            bundle.ShippingDestination = destination;
            bundle.ShippingInstructions = instructions;
            bundle.CompletedAt = DateTime.UtcNow;
            bundle.OperatorId = operatorId;

            await _bundleContainer.UpsertItemAsync(bundle, new PartitionKey(bundleId));

            return new ServiceResult { Success = true, Message = "Bundle marked as shipped successfully" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking bundle {BundleId} as shipped", bundleId);
            throw;
        }
    }

    public async Task<BundlingPerformanceMetrics> GetPerformanceMetricsAsync(string lineId, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var fromDate = from ?? DateTime.UtcNow.AddDays(-1);
            var toDate = to ?? DateTime.UtcNow;

            // Get bundles created in the time period
            var queryText = "SELECT * FROM c WHERE c.BundlingLineId = @lineId AND c.CreatedAt >= @from AND c.CreatedAt <= @to";
            var queryDefinition = new QueryDefinition(queryText)
                .WithParameter("@lineId", lineId)
                .WithParameter("@from", fromDate)
                .WithParameter("@to", toDate);

            var bundles = new List<RebarBundle>();
            using var iterator = _bundleContainer.GetItemQueryIterator<RebarBundle>(queryDefinition);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                bundles.AddRange(response);
            }

            var metrics = new BundlingPerformanceMetrics
            {
                BundlingLineId = lineId,
                CalculatedAt = DateTime.UtcNow,
                CalculationPeriod = toDate - fromDate,
                TotalBundlesCompleted = bundles.Count(b => b.Status == BundleStatus.Shipped || b.Status == BundleStatus.Ready),
                TotalBarsProcessed = bundles.Sum(b => b.BarCount),
                TotalTonnage = bundles.Sum(b => b.TotalWeight) / 1000.0, // Convert kg to tonnes
                QualityPassRate = bundles.Any() ? bundles.Count(b => b.PassedQualityCheck) * 100.0 / bundles.Count : 100.0,
                LineUtilization = 85.0, // Would be calculated from actual telemetry
                OverallEfficiency = bundles.Any() ? bundles.Average(b => b.QualityScore) : 0.0
            };

            // Calculate grade and size distributions
            foreach (var bundle in bundles)
            {
                metrics.BundlesByGrade[bundle.Grade] = metrics.BundlesByGrade.GetValueOrDefault(bundle.Grade, 0) + 1;
                metrics.BundlesBySize[bundle.Size] = metrics.BundlesBySize.GetValueOrDefault(bundle.Size, 0) + 1;
                metrics.BundlesByLength[bundle.Length] = metrics.BundlesByLength.GetValueOrDefault(bundle.Length, 0) + 1;
            }

            // Calculate defect metrics
            metrics.WeightDeviationDefects = bundles.Count(b => !b.IsWithinWeightTolerance());
            metrics.DefectRate = bundles.Any() ? 
                (metrics.WeightDeviationDefects + bundles.Count(b => b.QualityIssues.Any())) * 1000.0 / bundles.Count : 0.0;

            if (metrics.CalculationPeriod.TotalHours > 0)
            {
                metrics.AverageBundlingRate = metrics.TotalBundlesCompleted / metrics.CalculationPeriod.TotalHours;
            }

            await _performanceContainer.CreateItemAsync(metrics, new PartitionKey(lineId));

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating performance metrics for line {LineId}", lineId);
            throw;
        }
    }

    public async Task<RebarBundlingAlert> CreateBundlingAlertAsync(RebarBundlingAlert alert)
    {
        try
        {
            alert.CreatedAt = DateTime.UtcNow;
            await _alertContainer.CreateItemAsync(alert, new PartitionKey(alert.BundlingLineId));
            return alert;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bundling alert");
            throw;
        }
    }

    public async Task<IEnumerable<RebarBundlingAlert>> GetLineAlertsAsync(string lineId, bool includeResolved = false)
    {
        try
        {
            var queryText = includeResolved ? 
                "SELECT * FROM c WHERE c.BundlingLineId = @lineId ORDER BY c.CreatedAt DESC" :
                "SELECT * FROM c WHERE c.BundlingLineId = @lineId AND c.IsResolved = false ORDER BY c.CreatedAt DESC";

            var queryDefinition = new QueryDefinition(queryText).WithParameter("@lineId", lineId);

            var results = new List<RebarBundlingAlert>();
            using var iterator = _alertContainer.GetItemQueryIterator<RebarBundlingAlert>(queryDefinition);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alerts for line {LineId}", lineId);
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
            
            using var iterator = _alertContainer.GetItemQueryIterator<RebarBundlingAlert>(queryDefinition);
            
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
                    
                    await _alertContainer.UpsertItemAsync(alert, new PartitionKey(alert.BundlingLineId));
                    
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

    public async Task ProcessTelemetryAsync(RebarBundlingTelemetry telemetry)
    {
        try
        {
            await _telemetryContainer.CreateItemAsync(telemetry, new PartitionKey(telemetry.BundlingLineId));
            
            // Update line status from telemetry
            await UpdateLineStatusFromTelemetry(telemetry);
            
            // Check for alerts based on telemetry
            await CheckTelemetryForAlerts(telemetry);
            
            _logger.LogDebug("Processed telemetry for line {LineId}", telemetry.BundlingLineId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing telemetry for line {LineId}", telemetry.BundlingLineId);
            throw;
        }
    }

    private async Task UpdateLineStatusFromTelemetry(RebarBundlingTelemetry telemetry)
    {
        var status = await GetLineStatusAsync(telemetry.BundlingLineId) ?? 
                    new RebarBundlingStatus { BundlingLineId = telemetry.BundlingLineId };

        status.CurrentGrade = telemetry.Grade;
        status.CurrentSize = telemetry.Size;
        status.CurrentLength = telemetry.Length;
        status.BundlingSpeed = telemetry.ThroughputRate / 8.0; // Convert bars/hour to bundles/hour (assuming 8 bars avg)
        status.CraneOperational = telemetry.CraneLoadCapacity < 95.0;
        status.TyingMachineOperational = telemetry.TyingMachineSpeed > 0;
        status.WeighingSystemOperational = telemetry.WeighingAccuracy > 95.0;
        status.OverallQualityScore = 100 - telemetry.VisualDefectsCount * 5;
        status.LastUpdated = DateTime.UtcNow;

        await _statusContainer.UpsertItemAsync(status, new PartitionKey(status.BundlingLineId));
        _statusCache.AddOrUpdate(status.BundlingLineId, status, (key, oldValue) => status);
    }

    private async Task CheckTelemetryForAlerts(RebarBundlingTelemetry telemetry)
    {
        var alerts = new List<RebarBundlingAlert>();

        // Weight tolerance alert
        if (Math.Abs(telemetry.WeightDeviation) > 5.0)
        {
            alerts.Add(new RebarBundlingAlert
            {
                BundlingLineId = telemetry.BundlingLineId,
                BundleId = telemetry.CurrentBundleId,
                AffectedGrade = telemetry.Grade,
                AffectedSize = telemetry.Size,
                AlertType = RebarBundlingAlert.AlertTypes.WeightOutOfTolerance,
                Severity = "High",
                Description = $"Weight deviation {telemetry.WeightDeviation:F1}% exceeds tolerance",
                WeightDeviation = telemetry.WeightDeviation
            });
        }

        // Equipment failure alerts
        if (telemetry.CraneLoadCapacity > 95.0)
        {
            alerts.Add(new RebarBundlingAlert
            {
                BundlingLineId = telemetry.BundlingLineId,
                AlertType = RebarBundlingAlert.AlertTypes.CraneOverload,
                Severity = "Critical",
                Description = $"Crane load capacity at {telemetry.CraneLoadCapacity:F1}%",
                CraneOperational = false
            });
        }

        if (telemetry.TyingMachineSpeed < 1.0)
        {
            alerts.Add(new RebarBundlingAlert
            {
                BundlingLineId = telemetry.BundlingLineId,
                AlertType = RebarBundlingAlert.AlertTypes.TyingSystemFailure,
                Severity = "High",
                Description = "Tying machine speed below operational threshold",
                TyingMachineOperational = false
            });
        }

        // Quality alerts
        if (telemetry.VisualDefectsCount > 3)
        {
            alerts.Add(new RebarBundlingAlert
            {
                BundlingLineId = telemetry.BundlingLineId,
                BundleId = telemetry.CurrentBundleId,
                AlertType = RebarBundlingAlert.AlertTypes.ExcessiveDefects,
                Severity = "Medium",
                Description = $"Visual defects count: {telemetry.VisualDefectsCount}",
                QualityScore = 100 - (telemetry.VisualDefectsCount * 10)
            });
        }

        // Save all alerts
        foreach (var alert in alerts)
        {
            await CreateBundlingAlertAsync(alert);
        }
    }

    private double CalculateBundleQualityScore(RebarBundle bundle)
    {
        var baseScore = 100.0;

        // Deduct points for failed quality tests
        var failedBars = bundle.Bars.Count(b => !b.PassedTensileTest || !b.PassedBendTest || b.HasSurfaceDefects);
        baseScore -= (failedBars * 20.0); // 20 points per failed bar

        // Deduct for weight deviation
        if (!bundle.IsWithinWeightTolerance())
        {
            var deviation = Math.Abs(bundle.TotalWeight - bundle.CalculatedWeight) / bundle.CalculatedWeight * 100;
            baseScore -= deviation * 5; // 5 points per % deviation
        }

        // Deduct for surface quality issues
        var avgSurfaceQuality = bundle.Bars.Average(b => b.SurfaceQuality);
        if (avgSurfaceQuality < 80)
        {
            baseScore -= (80 - avgSurfaceQuality);
        }

        // Deduct for straightness issues
        var avgStraightness = bundle.Bars.Average(b => b.Straightness);
        if (avgStraightness > 3.0) // More than 3mm deviation
        {
            baseScore -= (avgStraightness - 3.0) * 5;
        }

        return Math.Max(0, baseScore);
    }

    private double GetHeatChemistry(string heatNumber, string element)
    {
        // In a real implementation, this would query the ladle metallurgy service
        // For now, return typical values for rebar chemistry
        return element switch
        {
            "Carbon" => 0.25,
            "Manganese" => 1.35,
            "Phosphorus" => 0.030,
            "Sulfur" => 0.035,
            _ => 0.0
        };
    }
}