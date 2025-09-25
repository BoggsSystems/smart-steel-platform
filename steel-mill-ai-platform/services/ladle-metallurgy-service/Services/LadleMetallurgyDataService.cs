using LadleMetallurgyService.Models;
using Microsoft.Azure.Cosmos;
using System.Collections.Concurrent;

namespace LadleMetallurgyService.Services;

public class LadleMetallurgyDataService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _ladleContainer;
    private readonly Container _telemetryContainer;
    private readonly Container _alertsContainer;
    private readonly Container _performanceContainer;
    private readonly Container _recipesContainer;
    private readonly ILogger<LadleMetallurgyDataService> _logger;
    
    // In-memory cache for real-time operations
    private readonly ConcurrentDictionary<string, LadleStatus> _ladleStatusCache = new();

    public LadleMetallurgyDataService(
        CosmosClient cosmosClient,
        ILogger<LadleMetallurgyDataService> logger)
    {
        _cosmosClient = cosmosClient;
        _logger = logger;
        
        // Initialize containers
        var database = _cosmosClient.GetDatabase("SteelMillRebarDB");
        _ladleContainer = database.GetContainer("LadleStatus");
        _telemetryContainer = database.GetContainer("LadleMetallurgyTelemetry");
        _alertsContainer = database.GetContainer("LadleMetallurgyAlerts");
        _performanceContainer = database.GetContainer("LadleMetallurgyPerformance");
        _recipesContainer = database.GetContainer("TreatmentRecipes");
    }

    public async Task<IEnumerable<LadleStatus>> GetAllLadlesAsync()
    {
        try
        {
            if (_ladleStatusCache.Any())
            {
                return _ladleStatusCache.Values.ToList();
            }

            // Fallback to database
            var query = "SELECT * FROM c ORDER BY c.LastUpdated DESC";
            var results = new List<LadleStatus>();
            
            using var feedIterator = _ladleContainer.GetItemQueryIterator<LadleStatus>(query);
            while (feedIterator.HasMoreResults)
            {
                var response = await feedIterator.ReadNextAsync();
                results.AddRange(response);
            }

            // Update cache
            foreach (var ladle in results)
            {
                _ladleStatusCache.TryAdd(ladle.LadleId, ladle);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all ladles");
            throw;
        }
    }

    public async Task<LadleStatus?> GetLadleStatusAsync(string ladleId)
    {
        try
        {
            // Check cache first
            if (_ladleStatusCache.TryGetValue(ladleId, out var cachedStatus))
            {
                return cachedStatus;
            }

            // Query database
            var response = await _ladleContainer.ReadItemAsync<LadleStatus>(ladleId, new PartitionKey(ladleId));
            var status = response.Resource;
            
            // Update cache
            _ladleStatusCache.TryAdd(ladleId, status);
            
            return status;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ladle status for {LadleId}", ladleId);
            throw;
        }
    }

    public async Task ProcessTelemetryAsync(LadleMetallurgyTelemetry telemetry)
    {
        try
        {
            // Store telemetry
            await _telemetryContainer.CreateItemAsync(telemetry, new PartitionKey(telemetry.LadleId));

            // Update ladle status
            await UpdateLadleStatusFromTelemetry(telemetry);

            // Check for alerts
            await CheckForAlertsAsync(telemetry);

            _logger.LogDebug("Processed telemetry for ladle {LadleId}", telemetry.LadleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing telemetry for ladle {LadleId}", telemetry.LadleId);
            throw;
        }
    }

    private async Task UpdateLadleStatusFromTelemetry(LadleMetallurgyTelemetry telemetry)
    {
        var status = await GetLadleStatusAsync(telemetry.LadleId) ?? new LadleStatus { LadleId = telemetry.LadleId };
        
        // Update chemistry values
        status.CurrentCarbon = telemetry.Carbon;
        status.CurrentManganese = telemetry.Manganese;
        status.CurrentPhosphorus = telemetry.Phosphorus;
        status.CurrentSulfur = telemetry.Sulfur;
        status.CurrentSilicon = telemetry.Silicon;
        status.CurrentNitrogen = telemetry.Nitrogen;
        status.CurrentOxygen = telemetry.Oxygen;
        
        // Update process parameters
        status.SteelTemperature = telemetry.SteelTemperature;
        status.SteelWeight = telemetry.SteelWeight;
        status.ArgonFlowRate = telemetry.ArgonFlowRate;
        status.VacuumPressure = telemetry.VacuumLevel;
        status.StirringPower = telemetry.StirringIntensity;
        status.TargetGrade = telemetry.TargetGrade;
        
        // Calculate quality metrics
        var target = TargetChemistry.GetTargetForGrade(telemetry.TargetGrade);
        status.ChemistryAccuracy = CalculateChemistryAccuracy(status, target);
        status.TreatmentEfficiency = CalculateTreatmentEfficiency(status, telemetry);
        
        status.LastUpdated = DateTime.UtcNow;

        // Update database and cache
        await _ladleContainer.UpsertItemAsync(status, new PartitionKey(status.LadleId));
        _ladleStatusCache.AddOrUpdate(status.LadleId, status, (key, oldValue) => status);
    }

    private double CalculateChemistryAccuracy(LadleStatus status, TargetChemistry target)
    {
        var carbonAccuracy = 1.0 - Math.Min(1.0, Math.Abs(status.CurrentCarbon - target.TargetCarbon) / target.CarbonTolerance);
        var manganeseAccuracy = 1.0 - Math.Min(1.0, Math.Abs(status.CurrentManganese - target.TargetManganese) / target.ManganeseTolerance);
        var phosphorusAccuracy = status.CurrentPhosphorus <= target.TargetPhosphorus ? 1.0 : 0.8;
        var sulfurAccuracy = status.CurrentSulfur <= target.TargetSulfur ? 1.0 : 0.8;
        
        return (carbonAccuracy + manganeseAccuracy + phosphorusAccuracy + sulfurAccuracy) / 4.0 * 100;
    }

    private double CalculateTreatmentEfficiency(LadleStatus status, LadleMetallurgyTelemetry telemetry)
    {
        // Efficiency based on process parameters and chemistry improvement
        double efficiency = 80.0; // Base efficiency
        
        // Adjust for chemistry accuracy
        if (status.ChemistryAccuracy > 95) efficiency += 15;
        else if (status.ChemistryAccuracy > 90) efficiency += 10;
        else if (status.ChemistryAccuracy > 85) efficiency += 5;
        
        // Adjust for process parameters
        if (telemetry.VacuumLevel > 1.0 && telemetry.ArgonFlowRate > 100) efficiency += 5;
        if (telemetry.Oxygen < 50) efficiency += 5; // Good deoxidation
        if (telemetry.Sulfur < status.TargetGrade == RebarGrade.Grade60 ? 0.04 : 0.035) efficiency += 5;
        
        return Math.Min(100, efficiency);
    }

    private async Task CheckForAlertsAsync(LadleMetallurgyTelemetry telemetry)
    {
        var alerts = new List<LadleMetallurgyAlert>();
        var target = TargetChemistry.GetTargetForGrade(telemetry.TargetGrade);

        // Chemistry alerts
        if (Math.Abs(telemetry.Carbon - target.TargetCarbon) > target.CarbonTolerance * 1.5)
        {
            alerts.Add(CreateAlert(telemetry.LadleId, telemetry.HeatId, telemetry.TargetGrade,
                LadleMetallurgyAlert.AlertTypes.ChemistryOutOfSpec,
                $"Carbon content {telemetry.Carbon:F3}% deviates significantly from target {target.TargetCarbon:F3}%",
                "High", telemetry.SteelTemperature, telemetry.Carbon));
        }

        if (telemetry.Sulfur > target.TargetSulfur * 1.2)
        {
            alerts.Add(CreateAlert(telemetry.LadleId, telemetry.HeatId, telemetry.TargetGrade,
                LadleMetallurgyAlert.AlertTypes.SulfurRemovalInefficient,
                $"Sulfur level {telemetry.Sulfur:F4}% exceeds acceptable range",
                "High", null, null, telemetry.Sulfur));
        }

        // Process alerts
        if (telemetry.VacuumLevel < 0.5)
        {
            alerts.Add(CreateAlert(telemetry.LadleId, telemetry.HeatId, telemetry.TargetGrade,
                LadleMetallurgyAlert.AlertTypes.VacuumLevelLow,
                $"Vacuum level {telemetry.VacuumLevel:F1} mbar is too low",
                "Medium"));
        }

        if (telemetry.Oxygen > 100)
        {
            alerts.Add(CreateAlert(telemetry.LadleId, telemetry.HeatId, telemetry.TargetGrade,
                LadleMetallurgyAlert.AlertTypes.OxygenLevelHigh,
                $"Oxygen content {telemetry.Oxygen} ppm is excessive",
                "Medium", null, null, null, null, telemetry.Oxygen));
        }

        // Store alerts
        foreach (var alert in alerts)
        {
            await _alertsContainer.CreateItemAsync(alert, new PartitionKey(alert.LadleId));
            _logger.LogWarning("Alert created for ladle {LadleId}: {AlertType}", 
                telemetry.LadleId, alert.AlertType);
        }
    }

    private LadleMetallurgyAlert CreateAlert(string ladleId, string heatId, RebarGrade grade,
        string alertType, string description, string severity,
        double? temperature = null, double? carbon = null, double? sulfur = null,
        double? vacuum = null, double? oxygen = null)
    {
        return new LadleMetallurgyAlert
        {
            LadleId = ladleId,
            HeatId = heatId,
            AffectedGrade = grade,
            AlertType = alertType,
            Description = description,
            Severity = severity,
            TemperatureReading = temperature,
            CarbonContentReading = carbon,
            SulfurLevel = sulfur
        };
    }

    public async Task<ServiceResult> StartTreatmentAsync(string ladleId, TreatmentRecipe recipe)
    {
        try
        {
            var status = await GetLadleStatusAsync(ladleId);
            if (status == null)
            {
                return new ServiceResult { Success = false, Message = "Ladle not found" };
            }

            if (status.CurrentState != "Ready" && status.CurrentState != "Idle")
            {
                return new ServiceResult 
                { 
                    Success = false, 
                    Message = $"Ladle is in {status.CurrentState} state, cannot start treatment" 
                };
            }

            status.CurrentState = "Treating";
            status.TreatmentStation = $"LF-{ladleId.Substring(0, 2)}";
            status.LastUpdated = DateTime.UtcNow;

            await _ladleContainer.UpsertItemAsync(status, new PartitionKey(status.LadleId));
            _ladleStatusCache.AddOrUpdate(status.LadleId, status, (key, oldValue) => status);

            return new ServiceResult { Success = true, Message = "Treatment started successfully" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting treatment for ladle {LadleId}", ladleId);
            return new ServiceResult { Success = false, Message = "Internal error starting treatment" };
        }
    }

    public async Task<ServiceResult> CompleteTreatmentAsync(string ladleId)
    {
        try
        {
            var status = await GetLadleStatusAsync(ladleId);
            if (status == null)
            {
                return new ServiceResult { Success = false, Message = "Ladle not found" };
            }

            // Check chemistry compliance
            if (!status.IsChemistryOnTarget())
            {
                return new ServiceResult 
                { 
                    Success = false, 
                    Message = "Chemistry is not within specification, cannot complete treatment" 
                };
            }

            status.CurrentState = "Ready";
            status.IsHomogenized = true;
            status.LastUpdated = DateTime.UtcNow;

            await _ladleContainer.UpsertItemAsync(status, new PartitionKey(status.LadleId));
            _ladleStatusCache.AddOrUpdate(status.LadleId, status, (key, oldValue) => status);

            // Create performance metrics
            await CreatePerformanceMetricsAsync(status);

            return new ServiceResult { Success = true, Message = "Treatment completed successfully" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing treatment for ladle {LadleId}", ladleId);
            return new ServiceResult { Success = false, Message = "Internal error completing treatment" };
        }
    }

    private async Task CreatePerformanceMetricsAsync(LadleStatus status)
    {
        var metrics = new LadleMetallurgyPerformanceMetrics
        {
            LadleId = status.LadleId,
            HeatId = status.HeatId,
            Grade = status.TargetGrade,
            ChemistryAccuracy = status.ChemistryAccuracy,
            TreatmentTime = 45.0, // Would be calculated from actual start/end times
            HomogenizationIndex = 0.95,
            MetQualityTargets = status.IsChemistryOnTarget(),
            ProcessedAt = DateTime.UtcNow
        };

        await _performanceContainer.CreateItemAsync(metrics, new PartitionKey(metrics.LadleId));
    }

    public async Task<IEnumerable<LadleMetallurgyPerformanceMetrics>> GetPerformanceMetricsAsync(
        DateTime? from = null, DateTime? to = null, RebarGrade? grade = null)
    {
        try
        {
            var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
            var toDate = to ?? DateTime.UtcNow;

            var query = $"SELECT * FROM c WHERE c.ProcessedAt >= '{fromDate:yyyy-MM-ddTHH:mm:ssZ}' AND c.ProcessedAt <= '{toDate:yyyy-MM-ddTHH:mm:ssZ}'";
            
            if (grade.HasValue)
            {
                query += $" AND c.Grade = {(int)grade.Value}";
            }

            query += " ORDER BY c.ProcessedAt DESC";

            var results = new List<LadleMetallurgyPerformanceMetrics>();
            using var feedIterator = _performanceContainer.GetItemQueryIterator<LadleMetallurgyPerformanceMetrics>(query);
            
            while (feedIterator.HasMoreResults)
            {
                var response = await feedIterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance metrics");
            throw;
        }
    }

    public async Task<IEnumerable<LadleMetallurgyAlert>> GetAlertsAsync(bool includeResolved = false)
    {
        try
        {
            var query = includeResolved ? 
                "SELECT * FROM c ORDER BY c.CreatedAt DESC" :
                "SELECT * FROM c WHERE c.IsResolved = false ORDER BY c.CreatedAt DESC";

            var results = new List<LadleMetallurgyAlert>();
            using var feedIterator = _alertsContainer.GetItemQueryIterator<LadleMetallurgyAlert>(query);
            
            while (feedIterator.HasMoreResults)
            {
                var response = await feedIterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alerts");
            throw;
        }
    }

    public async Task<ServiceResult> ResolveAlertAsync(string alertId, string resolutionAction)
    {
        try
        {
            var alert = await _alertsContainer.ReadItemAsync<LadleMetallurgyAlert>(alertId, new PartitionKey(alertId));
            var alertResource = alert.Resource;

            alertResource.IsResolved = true;
            alertResource.ResolvedAt = DateTime.UtcNow;
            alertResource.ResolutionAction = resolutionAction;

            await _alertsContainer.UpsertItemAsync(alertResource, new PartitionKey(alertId));

            return new ServiceResult { Success = true, Message = "Alert resolved successfully" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving alert {AlertId}", alertId);
            return new ServiceResult { Success = false, Message = "Error resolving alert" };
        }
    }

    public async Task<IEnumerable<TreatmentRecipe>> GetTreatmentRecipesAsync(RebarGrade? grade = null)
    {
        try
        {
            var query = grade.HasValue ? 
                $"SELECT * FROM c WHERE c.TargetGrade = {(int)grade.Value}" :
                "SELECT * FROM c";

            var results = new List<TreatmentRecipe>();
            using var feedIterator = _recipesContainer.GetItemQueryIterator<TreatmentRecipe>(query);
            
            while (feedIterator.HasMoreResults)
            {
                var response = await feedIterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving treatment recipes");
            throw;
        }
    }

    public async Task<TreatmentRecipe> CreateTreatmentRecipeAsync(TreatmentRecipe recipe)
    {
        try
        {
            recipe.Id = Guid.NewGuid().ToString();
            recipe.CreatedAt = DateTime.UtcNow;

            await _recipesContainer.CreateItemAsync(recipe, new PartitionKey(recipe.Id));
            return recipe;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating treatment recipe");
            throw;
        }
    }
}