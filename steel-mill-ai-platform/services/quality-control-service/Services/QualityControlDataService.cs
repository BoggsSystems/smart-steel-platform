using Microsoft.Azure.Cosmos;
using QualityControlService.Models;
using System.Collections.Concurrent;

namespace QualityControlService.Services;

public interface IQualityControlDataService
{
    Task<QualityTestSample?> GetSampleAsync(string sampleId);
    Task<IEnumerable<QualityTestSample>> GetSamplesAsync(string? batchId = null, string? heatNumber = null, QualityStatus? status = null, int limit = 100);
    Task<QualityTestSample> CreateSampleAsync(QualityTestSample sample);
    Task UpdateSampleStatusAsync(string sampleId, QualityStatus status, string? notes = null);
    
    Task<QualityTestResult> AddTestResultAsync(string sampleId, QualityTestResult testResult);
    Task<IEnumerable<QualityTestResult>> GetSampleTestResultsAsync(string sampleId);
    Task<ServiceResult> ValidateASTMComplianceAsync(string sampleId);
    Task<ServiceResult> ValidateCustomerSpecificationAsync(string sampleId, Dictionary<string, object> customerRequirements);
    
    Task<QualityControlStation?> GetStationStatusAsync(string stationId);
    Task<IEnumerable<QualityControlStation>> GetAllStationsAsync();
    Task UpdateStationWorkloadAsync(string stationId, int currentWorkload);
    Task<string> AssignSampleToStationAsync(string sampleId, QualityTestType testType);
    
    Task<QualityPerformanceMetrics> GetPerformanceMetricsAsync(string stationId, DateTime? from = null, DateTime? to = null);
    Task<QualityAlert> CreateQualityAlertAsync(QualityAlert alert);
    Task<IEnumerable<QualityAlert>> GetStationAlertsAsync(string stationId, bool includeResolved = false);
    Task<ServiceResult> ResolveAlertAsync(string alertId, string resolutionAction, string inspectorId);
    
    Task<ASTMSpecification> GetASTMSpecificationAsync(ASTMStandard standard, RebarGrade grade, RebarSize size);
    Task<ServiceResult> ProcessTestResultAsync(QualityTestResult testResult);
    Task<ServiceResult> GenerateQualityCertificateAsync(string sampleId);
}

public class QualityControlDataService : IQualityControlDataService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _samplesContainer;
    private readonly Container _testResultsContainer;
    private readonly Container _stationsContainer;
    private readonly Container _metricsContainer;
    private readonly Container _alertsContainer;
    private readonly Container _certificatesContainer;
    private readonly ILogger<QualityControlDataService> _logger;
    
    // In-memory cache for real-time operations
    private readonly ConcurrentDictionary<string, QualityControlStation> _stationsCache = new();
    private readonly ConcurrentDictionary<string, ASTMSpecification> _specificationsCache = new();

    public QualityControlDataService(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<QualityControlDataService> logger
    )
    {
        _cosmosClient = cosmosClient;
        _logger = logger;

        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "SteelMillRebarDB";
        
        _samplesContainer = _cosmosClient.GetContainer(databaseName, "QualityTestSamples");
        _testResultsContainer = _cosmosClient.GetContainer(databaseName, "QualityTestResults");
        _stationsContainer = _cosmosClient.GetContainer(databaseName, "QualityControlStations");
        _metricsContainer = _cosmosClient.GetContainer(databaseName, "QualityPerformanceMetrics");
        _alertsContainer = _cosmosClient.GetContainer(databaseName, "QualityAlerts");
        _certificatesContainer = _cosmosClient.GetContainer(databaseName, "QualityCertificates");
    }

    public async Task<QualityTestSample?> GetSampleAsync(string sampleId)
    {
        try
        {
            var response = await _samplesContainer.ReadItemAsync<QualityTestSample>(
                sampleId, new PartitionKey(sampleId)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Quality test sample not found: {SampleId}", sampleId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sample {SampleId}", sampleId);
            throw;
        }
    }

    public async Task<IEnumerable<QualityTestSample>> GetSamplesAsync(
        string? batchId = null, string? heatNumber = null, QualityStatus? status = null, int limit = 100)
    {
        try
        {
            var queryText = "SELECT * FROM c WHERE 1=1";

            if (!string.IsNullOrEmpty(batchId))
                queryText += " AND c.BatchId = @batchId";
            if (!string.IsNullOrEmpty(heatNumber))
                queryText += " AND c.HeatNumber = @heatNumber";
            if (status.HasValue)
                queryText += " AND c.Status = @status";

            queryText += " ORDER BY c.SampleTaken DESC";

            var queryDefinition = new QueryDefinition(queryText);

            if (!string.IsNullOrEmpty(batchId))
                queryDefinition.WithParameter("@batchId", batchId);
            if (!string.IsNullOrEmpty(heatNumber))
                queryDefinition.WithParameter("@heatNumber", heatNumber);
            if (status.HasValue)
                queryDefinition.WithParameter("@status", (int)status.Value);

            var results = new List<QualityTestSample>();
            using var iterator = _samplesContainer.GetItemQueryIterator<QualityTestSample>(
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
            _logger.LogError(ex, "Error retrieving quality samples");
            throw;
        }
    }

    public async Task<QualityTestSample> CreateSampleAsync(QualityTestSample sample)
    {
        try
        {
            // Set initial status and validate required fields
            sample.Status = QualityStatus.InQueue;
            sample.CreatedAt = DateTime.UtcNow;
            sample.SampleTaken = DateTime.UtcNow;

            await _samplesContainer.CreateItemAsync(sample, new PartitionKey(sample.SampleId));

            // Assign to appropriate testing station
            await AssignSampleToStationForInitialTesting(sample);

            _logger.LogInformation("Created quality test sample {SampleId} for heat {HeatNumber}", 
                sample.SampleId, sample.HeatNumber);

            return sample;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating quality test sample");
            throw;
        }
    }

    public async Task UpdateSampleStatusAsync(string sampleId, QualityStatus status, string? notes = null)
    {
        try
        {
            var sample = await GetSampleAsync(sampleId);
            if (sample == null)
            {
                throw new InvalidOperationException($"Sample {sampleId} not found");
            }

            sample.Status = status;
            
            switch (status)
            {
                case QualityStatus.Testing:
                    sample.TestingStarted = DateTime.UtcNow;
                    break;
                case QualityStatus.Completed:
                    sample.TestingCompleted = DateTime.UtcNow;
                    break;
                case QualityStatus.Approved:
                case QualityStatus.Rejected:
                    sample.TestingCompleted ??= DateTime.UtcNow;
                    break;
            }

            if (!string.IsNullOrEmpty(notes))
            {
                sample.QualityNotes.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm}: {notes}");
            }

            await _samplesContainer.UpsertItemAsync(sample, new PartitionKey(sample.SampleId));

            _logger.LogInformation("Updated sample {SampleId} status to {Status}", sampleId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sample status for {SampleId}", sampleId);
            throw;
        }
    }

    public async Task<QualityTestResult> AddTestResultAsync(string sampleId, QualityTestResult testResult)
    {
        try
        {
            var sample = await GetSampleAsync(sampleId);
            if (sample == null)
            {
                throw new InvalidOperationException($"Sample {sampleId} not found");
            }

            // Process test result and determine pass/fail
            await ProcessTestResultAsync(testResult);

            // Store test result
            await _testResultsContainer.CreateItemAsync(testResult, new PartitionKey(sampleId));

            // Update sample with test result
            sample.TestResults.Add(testResult);
            await UpdateSampleWithTestResult(sample, testResult);

            _logger.LogInformation("Added {TestType} result for sample {SampleId}: {Result}", 
                testResult.TestType, sampleId, testResult.Result);

            return testResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding test result for sample {SampleId}", sampleId);
            throw;
        }
    }

    public async Task<IEnumerable<QualityTestResult>> GetSampleTestResultsAsync(string sampleId)
    {
        try
        {
            var queryText = "SELECT * FROM c WHERE c.SampleId = @sampleId ORDER BY c.TestDate DESC";
            var queryDefinition = new QueryDefinition(queryText).WithParameter("@sampleId", sampleId);

            var results = new List<QualityTestResult>();
            using var iterator = _testResultsContainer.GetItemQueryIterator<QualityTestResult>(queryDefinition);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving test results for sample {SampleId}", sampleId);
            throw;
        }
    }

    public async Task<ServiceResult> ValidateASTMComplianceAsync(string sampleId)
    {
        try
        {
            var sample = await GetSampleAsync(sampleId);
            if (sample == null)
            {
                return new ServiceResult { Success = false, Message = "Sample not found" };
            }

            var specification = await GetASTMSpecificationAsync(sample.Standard, sample.Grade, sample.Size);
            var complianceIssues = new List<string>();

            // Check mechanical properties
            var tensileResult = sample.TestResults.OfType<TensileTestResult>().FirstOrDefault();
            if (tensileResult != null)
            {
                if (tensileResult.YieldStrength < specification.MinYieldStrength)
                {
                    complianceIssues.Add($"Yield strength {tensileResult.YieldStrength:F0} MPa below minimum {specification.MinYieldStrength:F0} MPa");
                }
                
                if (tensileResult.TensileStrength < specification.MinTensileStrength)
                {
                    complianceIssues.Add($"Tensile strength {tensileResult.TensileStrength:F0} MPa below minimum {specification.MinTensileStrength:F0} MPa");
                }
                
                if (tensileResult.ElongationAtBreak < specification.MinElongation)
                {
                    complianceIssues.Add($"Elongation {tensileResult.ElongationAtBreak:F1}% below minimum {specification.MinElongation:F1}%");
                }
            }

            // Check chemical composition
            var chemicalResult = sample.TestResults.OfType<ChemicalAnalysisResult>().FirstOrDefault();
            if (chemicalResult != null)
            {
                if (chemicalResult.CarbonContent > specification.MaxCarbon)
                {
                    complianceIssues.Add($"Carbon content {chemicalResult.CarbonContent:F3}% exceeds maximum {specification.MaxCarbon:F3}%");
                }
                
                if (chemicalResult.PhosphorusContent > specification.MaxPhosphorus)
                {
                    complianceIssues.Add($"Phosphorus content {chemicalResult.PhosphorusContent:F3}% exceeds maximum {specification.MaxPhosphorus:F3}%");
                }
                
                if (chemicalResult.SulfurContent > specification.MaxSulfur)
                {
                    complianceIssues.Add($"Sulfur content {chemicalResult.SulfurContent:F3}% exceeds maximum {specification.MaxSulfur:F3}%");
                }
            }

            // Check dimensional requirements
            var dimensionalResult = sample.TestResults.OfType<DimensionalTestResult>().FirstOrDefault();
            if (dimensionalResult != null)
            {
                var diameterDeviation = Math.Abs(dimensionalResult.ActualDiameter - specification.NominalDiameter);
                if (diameterDeviation > specification.DiameterTolerance)
                {
                    complianceIssues.Add($"Diameter deviation {diameterDeviation:F2}mm exceeds tolerance ±{specification.DiameterTolerance:F2}mm");
                }
                
                if (dimensionalResult.RibHeight < specification.MinRibHeight)
                {
                    complianceIssues.Add($"Rib height {dimensionalResult.RibHeight:F2}mm below minimum {specification.MinRibHeight:F2}mm");
                }
            }

            // Check bend test
            var bendResult = sample.TestResults.OfType<BendTestResult>().FirstOrDefault();
            if (bendResult != null && !bendResult.CompletedWithoutCracking)
            {
                complianceIssues.Add("Failed 180° bend test - cracking detected");
            }

            var isCompliant = !complianceIssues.Any();
            
            // Update sample compliance status
            sample.ASTMCompliant = isCompliant;
            sample.ComplianceNotes = string.Join("; ", complianceIssues);
            
            if (isCompliant)
            {
                sample.CertificationDate = DateTime.UtcNow;
                sample.CertificationNumber = GenerateCertificationNumber(sample);
            }

            await _samplesContainer.UpsertItemAsync(sample, new PartitionKey(sample.SampleId));

            return new ServiceResult 
            { 
                Success = true, 
                Message = isCompliant ? "ASTM compliant" : $"Non-compliant: {string.Join(", ", complianceIssues)}",
                Data = new { IsCompliant = isCompliant, Issues = complianceIssues }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating ASTM compliance for sample {SampleId}", sampleId);
            throw;
        }
    }

    public async Task<ServiceResult> ValidateCustomerSpecificationAsync(string sampleId, Dictionary<string, object> customerRequirements)
    {
        try
        {
            var sample = await GetSampleAsync(sampleId);
            if (sample == null)
            {
                return new ServiceResult { Success = false, Message = "Sample not found" };
            }

            var complianceIssues = new List<string>();

            // Process customer requirements (this would be customized based on actual customer specs)
            foreach (var requirement in customerRequirements)
            {
                var result = ValidateCustomerRequirement(sample, requirement.Key, requirement.Value);
                if (!result.isCompliant)
                {
                    complianceIssues.Add(result.issue);
                }
            }

            var isCompliant = !complianceIssues.Any();
            sample.CustomerSpecCompliant = isCompliant;

            await _samplesContainer.UpsertItemAsync(sample, new PartitionKey(sample.SampleId));

            return new ServiceResult 
            { 
                Success = true, 
                Message = isCompliant ? "Customer specification compliant" : $"Non-compliant: {string.Join(", ", complianceIssues)}",
                Data = new { IsCompliant = isCompliant, Issues = complianceIssues }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating customer specification for sample {SampleId}", sampleId);
            throw;
        }
    }

    public async Task<QualityControlStation?> GetStationStatusAsync(string stationId)
    {
        try
        {
            // Check cache first
            if (_stationsCache.TryGetValue(stationId, out var cachedStation))
            {
                return cachedStation;
            }

            var response = await _stationsContainer.ReadItemAsync<QualityControlStation>(
                stationId, new PartitionKey(stationId)
            );
            
            var station = response.Resource;
            _stationsCache.TryAdd(stationId, station);
            return station;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Quality control station not found: {StationId}", stationId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving station status for {StationId}", stationId);
            throw;
        }
    }

    public async Task<IEnumerable<QualityControlStation>> GetAllStationsAsync()
    {
        try
        {
            var queryText = "SELECT * FROM c ORDER BY c.StationName";
            var queryDefinition = new QueryDefinition(queryText);

            var results = new List<QualityControlStation>();
            using var iterator = _stationsContainer.GetItemQueryIterator<QualityControlStation>(queryDefinition);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            // Update cache
            foreach (var station in results)
            {
                _stationsCache.AddOrUpdate(station.StationId, station, (key, oldValue) => station);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all quality control stations");
            throw;
        }
    }

    public async Task UpdateStationWorkloadAsync(string stationId, int currentWorkload)
    {
        try
        {
            var station = await GetStationStatusAsync(stationId);
            if (station != null)
            {
                station.CurrentWorkload = currentWorkload;
                station.Utilization = station.MaxCapacity > 0 ? (double)currentWorkload / station.MaxCapacity * 100 : 0;
                station.LastUpdated = DateTime.UtcNow;

                await _stationsContainer.UpsertItemAsync(station, new PartitionKey(stationId));
                _stationsCache.AddOrUpdate(stationId, station, (key, oldValue) => station);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workload for station {StationId}", stationId);
            throw;
        }
    }

    public async Task<string> AssignSampleToStationAsync(string sampleId, QualityTestType testType)
    {
        try
        {
            var stations = await GetAllStationsAsync();
            var availableStations = stations.Where(s => 
                s.OperationalStatus == "Active" && 
                s.AvailableTests.Contains(testType) &&
                s.CurrentWorkload < s.MaxCapacity
            ).OrderBy(s => s.Utilization).ToList();

            if (!availableStations.Any())
            {
                throw new InvalidOperationException($"No available stations for test type {testType}");
            }

            var selectedStation = availableStations.First();
            selectedStation.QueuedSamples.Add(sampleId);
            await UpdateStationWorkloadAsync(selectedStation.StationId, selectedStation.CurrentWorkload + 1);

            return selectedStation.StationId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning sample {SampleId} to station for test {TestType}", sampleId, testType);
            throw;
        }
    }

    public async Task<QualityPerformanceMetrics> GetPerformanceMetricsAsync(string stationId, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
            var toDate = to ?? DateTime.UtcNow;

            // Get samples tested in the time period
            var samples = await GetSamplesForMetricsCalculation(stationId, fromDate, toDate);

            var metrics = new QualityPerformanceMetrics
            {
                StationId = stationId,
                CalculatedAt = DateTime.UtcNow,
                CalculationPeriod = toDate - fromDate,
                TotalSamplesTested = samples.Count,
                TotalTestsPerformed = samples.SelectMany(s => s.TestResults).Count()
            };

            if (samples.Any())
            {
                // Calculate pass rates
                metrics.OverallPassRate = samples.Count(s => s.PassedAllTests) * 100.0 / samples.Count;
                metrics.ASTMComplianceRate = samples.Count(s => s.ASTMCompliant) * 100.0 / samples.Count;
                metrics.CustomerSpecComplianceRate = samples.Count(s => s.CustomerSpecCompliant) * 100.0 / samples.Count;

                // Calculate test-specific pass rates
                foreach (var testType in Enum.GetValues<QualityTestType>())
                {
                    var testResults = samples.SelectMany(s => s.TestResults)
                                           .Where(r => r.TestType == testType).ToList();
                    if (testResults.Any())
                    {
                        metrics.PassRateByTest[testType] = testResults.Count(r => r.Result == TestResult.Pass) * 100.0 / testResults.Count;
                    }
                }

                // Calculate throughput
                var totalHours = metrics.CalculationPeriod.TotalHours;
                if (totalHours > 0)
                {
                    metrics.TestingThroughputRate = metrics.TotalTestsPerformed / totalHours;
                    metrics.SampleProcessingRate = metrics.TotalSamplesTested / totalHours;
                }

                // Calculate quality trends and defect rates
                metrics.DefectRateTrend = CalculateDefectRateTrend(samples);
            }

            await _metricsContainer.CreateItemAsync(metrics, new PartitionKey(stationId));
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating performance metrics for station {StationId}", stationId);
            throw;
        }
    }

    public async Task<QualityAlert> CreateQualityAlertAsync(QualityAlert alert)
    {
        try
        {
            alert.CreatedAt = DateTime.UtcNow;
            await _alertsContainer.CreateItemAsync(alert, new PartitionKey(alert.StationId));
            
            _logger.LogWarning("Quality alert created for station {StationId}: {AlertType} - {Description}",
                alert.StationId, alert.AlertType, alert.Description);
            
            return alert;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating quality alert");
            throw;
        }
    }

    public async Task<IEnumerable<QualityAlert>> GetStationAlertsAsync(string stationId, bool includeResolved = false)
    {
        try
        {
            var queryText = includeResolved ? 
                "SELECT * FROM c WHERE c.StationId = @stationId ORDER BY c.CreatedAt DESC" :
                "SELECT * FROM c WHERE c.StationId = @stationId AND c.IsResolved = false ORDER BY c.CreatedAt DESC";

            var queryDefinition = new QueryDefinition(queryText).WithParameter("@stationId", stationId);

            var results = new List<QualityAlert>();
            using var iterator = _alertsContainer.GetItemQueryIterator<QualityAlert>(queryDefinition);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alerts for station {StationId}", stationId);
            throw;
        }
    }

    public async Task<ServiceResult> ResolveAlertAsync(string alertId, string resolutionAction, string inspectorId)
    {
        try
        {
            // Find alert by scanning
            var queryText = "SELECT * FROM c WHERE c.Id = @alertId";
            var queryDefinition = new QueryDefinition(queryText).WithParameter("@alertId", alertId);
            
            using var iterator = _alertsContainer.GetItemQueryIterator<QualityAlert>(queryDefinition);
            
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var alert = response.FirstOrDefault();
                
                if (alert != null)
                {
                    alert.IsResolved = true;
                    alert.ResolvedAt = DateTime.UtcNow;
                    alert.ResolutionAction = resolutionAction;
                    alert.QualityInspector = inspectorId;
                    
                    await _alertsContainer.UpsertItemAsync(alert, new PartitionKey(alert.StationId));
                    
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

    public async Task<ASTMSpecification> GetASTMSpecificationAsync(ASTMStandard standard, RebarGrade grade, RebarSize size)
    {
        var cacheKey = $"{standard}_{grade}_{size}";
        
        if (_specificationsCache.TryGetValue(cacheKey, out var cachedSpec))
        {
            return cachedSpec;
        }

        var specification = ASTMSpecification.GetSpecification(standard, grade, size);
        _specificationsCache.TryAdd(cacheKey, specification);
        
        return await Task.FromResult(specification);
    }

    public async Task<ServiceResult> ProcessTestResultAsync(QualityTestResult testResult)
    {
        try
        {
            // Determine pass/fail based on test type and measured values
            testResult.PassedAllCriteria = DetermineTestResultStatus(testResult);
            testResult.Result = testResult.PassedAllCriteria ? TestResult.Pass : TestResult.Fail;

            // Check for alerts based on test results
            await CheckForQualityAlerts(testResult);

            return new ServiceResult { Success = true, Message = "Test result processed successfully" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing test result {TestId}", testResult.TestId);
            throw;
        }
    }

    public async Task<ServiceResult> GenerateQualityCertificateAsync(string sampleId)
    {
        try
        {
            var sample = await GetSampleAsync(sampleId);
            if (sample == null)
            {
                return new ServiceResult { Success = false, Message = "Sample not found" };
            }

            if (!sample.ASTMCompliant || !sample.CustomerSpecCompliant)
            {
                return new ServiceResult { Success = false, Message = "Sample does not meet compliance requirements" };
            }

            var certificate = new QualityCertificate
            {
                SampleId = sampleId,
                BatchId = sample.BatchId,
                HeatNumber = sample.HeatNumber,
                Grade = sample.Grade,
                Size = sample.Size,
                Standard = sample.Standard,
                CertificationDate = DateTime.UtcNow,
                CertificationNumber = sample.CertificationNumber ?? GenerateCertificationNumber(sample),
                QualityInspector = sample.QualityInspector ?? "System Generated",
                TestResults = sample.TestResults
            };

            await _certificatesContainer.CreateItemAsync(certificate, new PartitionKey(sampleId));

            return new ServiceResult 
            { 
                Success = true, 
                Message = "Quality certificate generated successfully",
                Data = certificate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating quality certificate for sample {SampleId}", sampleId);
            throw;
        }
    }

    // Private helper methods
    private async Task AssignSampleToStationForInitialTesting(QualityTestSample sample)
    {
        // Assign to appropriate stations based on required tests
        var requiredTests = DetermineRequiredTests(sample.Grade, sample.Size, sample.Standard);
        
        foreach (var testType in requiredTests)
        {
            try
            {
                var stationId = await AssignSampleToStationAsync(sample.SampleId, testType);
                _logger.LogDebug("Assigned sample {SampleId} to station {StationId} for {TestType}", 
                    sample.SampleId, stationId, testType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not assign sample {SampleId} for test {TestType}", 
                    sample.SampleId, testType);
            }
        }
    }

    private List<QualityTestType> DetermineRequiredTests(RebarGrade grade, RebarSize size, ASTMStandard standard)
    {
        var tests = new List<QualityTestType>
        {
            QualityTestType.TensileTest,
            QualityTestType.BendTest,
            QualityTestType.DimensionalTest,
            QualityTestType.ChemicalAnalysis,
            QualityTestType.SurfaceInspection
        };

        // Add additional tests based on grade/standard
        if (grade >= RebarGrade.Grade75)
        {
            tests.Add(QualityTestType.HardnessTest);
        }

        if (standard == ASTMStandard.A706)
        {
            tests.Add(QualityTestType.UltrasonicTest);
        }

        return tests;
    }

    private async Task UpdateSampleWithTestResult(QualityTestSample sample, QualityTestResult testResult)
    {
        // Update overall sample status based on test results
        var allRequiredTests = DetermineRequiredTests(sample.Grade, sample.Size, sample.Standard);
        var completedTests = sample.TestResults.Select(r => r.TestType).ToHashSet();
        
        if (allRequiredTests.All(t => completedTests.Contains(t)))
        {
            sample.Status = QualityStatus.Completed;
            sample.PassedAllTests = sample.TestResults.All(r => r.Result == TestResult.Pass);
            sample.OverallQualityScore = CalculateOverallQualityScore(sample.TestResults);
            
            if (!sample.PassedAllTests)
            {
                sample.FailureReasons.AddRange(
                    sample.TestResults.Where(r => r.Result == TestResult.Fail)
                                    .SelectMany(r => r.FailedCriteria)
                );
            }
        }

        await _samplesContainer.UpsertItemAsync(sample, new PartitionKey(sample.SampleId));
    }

    private bool DetermineTestResultStatus(QualityTestResult testResult)
    {
        var failedCriteria = new List<string>();

        foreach (var criterion in testResult.AcceptanceCriteria)
        {
            if (!EvaluateAcceptanceCriterion(testResult, criterion.Key, criterion.Value))
            {
                failedCriteria.Add(criterion.Key);
            }
        }

        testResult.FailedCriteria = failedCriteria;
        return !failedCriteria.Any();
    }

    private bool EvaluateAcceptanceCriterion(QualityTestResult testResult, string criterion, string requirement)
    {
        // This would implement specific logic for evaluating different acceptance criteria
        // For now, simplified implementation
        if (testResult.MeasuredValues.TryGetValue(criterion, out var measuredValue))
        {
            if (testResult.RequiredMinValues.TryGetValue(criterion, out var minValue) && measuredValue < minValue)
                return false;
            if (testResult.RequiredMaxValues.TryGetValue(criterion, out var maxValue) && measuredValue > maxValue)
                return false;
        }
        return true;
    }

    private double CalculateOverallQualityScore(List<QualityTestResult> testResults)
    {
        if (!testResults.Any()) return 0;

        var passCount = testResults.Count(r => r.Result == TestResult.Pass);
        return (double)passCount / testResults.Count * 100;
    }

    private async Task CheckForQualityAlerts(QualityTestResult testResult)
    {
        var alerts = new List<QualityAlert>();

        if (testResult.Result == TestResult.Fail)
        {
            alerts.Add(new QualityAlert
            {
                StationId = testResult.TestEquipmentId,
                AlertType = QualityAlert.AlertTypes.TestFailure,
                Severity = "High",
                Description = $"{testResult.TestType} test failed: {string.Join(", ", testResult.FailedCriteria)}",
                TestType = testResult.TestType,
                TestEquipmentId = testResult.TestEquipmentId
            });
        }

        foreach (var alert in alerts)
        {
            await CreateQualityAlertAsync(alert);
        }
    }

    private async Task<List<QualityTestSample>> GetSamplesForMetricsCalculation(string stationId, DateTime from, DateTime to)
    {
        // This would query samples that were processed at the specific station
        // For now, simplified to get all samples in time range
        var samples = await GetSamplesAsync(status: QualityStatus.Completed, limit: 1000);
        return samples.Where(s => s.TestingCompleted >= from && s.TestingCompleted <= to).ToList();
    }

    private double CalculateDefectRateTrend(List<QualityTestSample> samples)
    {
        // Calculate trend in defect rate over the period
        // Simplified implementation
        var totalDefects = samples.Sum(s => s.FailureReasons.Count);
        return samples.Any() ? totalDefects * 1000.0 / samples.Count : 0; // defects per 1000 samples
    }

    private (bool isCompliant, string issue) ValidateCustomerRequirement(QualityTestSample sample, string requirement, object value)
    {
        // This would implement customer-specific validation logic
        // For now, simplified implementation
        return (true, string.Empty);
    }

    private string GenerateCertificationNumber(QualityTestSample sample)
    {
        return $"QC-{DateTime.UtcNow:yyyyMMdd}-{sample.Grade}{sample.Size}-{sample.SampleId.Substring(0, 8).ToUpper()}";
    }
}

public class ServiceResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}

public class QualityCertificate
{
    public string CertificateId { get; set; } = Guid.NewGuid().ToString();
    public string SampleId { get; set; } = string.Empty;
    public string BatchId { get; set; } = string.Empty;
    public string HeatNumber { get; set; } = string.Empty;
    public RebarGrade Grade { get; set; }
    public RebarSize Size { get; set; }
    public ASTMStandard Standard { get; set; }
    public string CertificationNumber { get; set; } = string.Empty;
    public DateTime CertificationDate { get; set; }
    public string QualityInspector { get; set; } = string.Empty;
    public List<QualityTestResult> TestResults { get; set; } = new();
}