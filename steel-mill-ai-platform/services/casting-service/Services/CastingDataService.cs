using Microsoft.Azure.Cosmos;
using CastingService.Models;

namespace CastingService.Services;

public interface ICastingDataService
{
    Task<CastingStatus?> GetCasterStatusAsync(string casterId);
    Task<IEnumerable<CastingTelemetry>> GetCasterTelemetryAsync(
        string casterId,
        DateTime? startTime,
        DateTime? endTime
    );
    Task UpdateMoldConfigurationAsync(string casterId, MoldConfiguration config);
    Task<CastingPerformanceMetrics?> GetPerformanceMetricsAsync(string casterId);
    Task CreateCastingAlertAsync(CastingAlert alert);
    Task SaveTelemetryAsync(CastingTelemetry telemetry);
}

public class CastingDataService : ICastingDataService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _telemetryContainer;
    private readonly Container _statusContainer;
    private readonly Container _alertsContainer;
    private readonly ILogger<CastingDataService> _logger;

    public CastingDataService(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<CastingDataService> logger
    )
    {
        _cosmosClient = cosmosClient;
        _logger = logger;

        var databaseName = configuration["CosmosDb:DatabaseName"];
        var telemetryContainerName = configuration["CosmosDb:TelemetryContainer"];
        var statusContainerName = configuration["CosmosDb:StatusContainer"];
        var alertsContainerName = configuration["CosmosDb:AlertsContainer"];

        _telemetryContainer = _cosmosClient.GetContainer(databaseName, telemetryContainerName);
        _statusContainer = _cosmosClient.GetContainer(databaseName, statusContainerName);
        _alertsContainer = _cosmosClient.GetContainer(databaseName, alertsContainerName);
    }

    public async Task<CastingStatus?> GetCasterStatusAsync(string casterId)
    {
        try
        {
            var response = await _statusContainer.ReadItemAsync<CastingStatus>(
                casterId,
                new PartitionKey(casterId)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Caster status not found for {CasterId}", casterId);
            return null;
        }
    }

    public async Task<IEnumerable<CastingTelemetry>> GetCasterTelemetryAsync(
        string casterId,
        DateTime? startTime,
        DateTime? endTime
    )
    {
        var queryText = "SELECT * FROM c WHERE c.CasterId = @casterId";

        if (startTime.HasValue)
            queryText += " AND c.Timestamp >= @startTime";
        if (endTime.HasValue)
            queryText += " AND c.Timestamp <= @endTime";

        queryText += " ORDER BY c.Timestamp DESC";

        var queryDefinition = new QueryDefinition(queryText).WithParameter("@casterId", casterId);

        if (startTime.HasValue)
            queryDefinition.WithParameter("@startTime", startTime.Value);
        if (endTime.HasValue)
            queryDefinition.WithParameter("@endTime", endTime.Value);

        var results = new List<CastingTelemetry>();

        using var iterator = _telemetryContainer.GetItemQueryIterator<CastingTelemetry>(
            queryDefinition
        );
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public async Task UpdateMoldConfigurationAsync(string casterId, MoldConfiguration config)
    {
        var status = await GetCasterStatusAsync(casterId) ?? new CastingStatus { CasterId = casterId };

        status.LastUpdated = DateTime.UtcNow;

        await _statusContainer.UpsertItemAsync(status, new PartitionKey(casterId));
        _logger.LogInformation("Updated mold configuration for caster {CasterId}", casterId);
    }

    public async Task<CastingPerformanceMetrics?> GetPerformanceMetricsAsync(string casterId)
    {
        var telemetryData = await GetCasterTelemetryAsync(
            casterId,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow
        );

        if (!telemetryData.Any())
            return null;

        var metrics = new CastingPerformanceMetrics
        {
            CasterId = casterId,
            CastingEfficiency = CalculateCastingEfficiency(telemetryData),
            QualityIndex = CalculateQualityIndex(telemetryData),
            TemperatureStability = CalculateTemperatureStability(telemetryData),
            CoolingEfficiency = CalculateCoolingEfficiency(telemetryData),
            VibrationLevel = telemetryData.Average(t => t.VibrationLevel),
            ThroughputRate = telemetryData.Average(t => t.CastingSpeed),
            CalculatedAt = DateTime.UtcNow
        };

        return metrics;
    }

    public async Task CreateCastingAlertAsync(CastingAlert alert)
    {
        await _alertsContainer.CreateItemAsync(alert, new PartitionKey(alert.CasterId));
        _logger.LogWarning(
            "Created casting alert {AlertId} for caster {CasterId}: {AlertType}",
            alert.Id,
            alert.CasterId,
            alert.AlertType
        );
    }

    public async Task SaveTelemetryAsync(CastingTelemetry telemetry)
    {
        await _telemetryContainer.CreateItemAsync(telemetry, new PartitionKey(telemetry.CasterId));
    }

    private double CalculateCastingEfficiency(IEnumerable<CastingTelemetry> telemetry)
    {
        var avgSpeed = telemetry.Average(t => t.CastingSpeed);
        var avgFlow = telemetry.Average(t => t.SteelFlow);
        var targetFlow = 100.0; // Target flow rate
        
        return Math.Min(100, (avgFlow / targetFlow) * 100);
    }

    private double CalculateQualityIndex(IEnumerable<CastingTelemetry> telemetry)
    {
        var tempVariance = telemetry.Select(t => t.TundishTemperature).ToArray().Variance();
        var vibrationAvg = telemetry.Average(t => t.VibrationLevel);
        var flowStability = 1.0 - (telemetry.Select(t => t.SteelFlow).ToArray().Variance() / 100);
        
        var temperatureStability = Math.Max(0, 1.0 - (tempVariance / 10000));
        var vibrationScore = Math.Max(0, 1.0 - (vibrationAvg / 10));
        
        return Math.Min(100, (temperatureStability + vibrationScore + flowStability) * 33.33);
    }

    private double CalculateTemperatureStability(IEnumerable<CastingTelemetry> telemetry)
    {
        var tempVariance = telemetry.Select(t => t.TundishTemperature).ToArray().Variance();
        return Math.Max(0, Math.Min(100, 100 - (tempVariance / 100)));
    }

    private double CalculateCoolingEfficiency(IEnumerable<CastingTelemetry> telemetry)
    {
        var avgCoolingFlow = telemetry.Average(t => t.MoldCoolingWaterFlow);
        var avgMoldTemp = telemetry.Average(t => t.MoldTemperature);
        var targetTemp = 1200.0; // Target mold temperature
        
        var tempEfficiency = Math.Max(0, 1.0 - Math.Abs(avgMoldTemp - targetTemp) / targetTemp);
        return Math.Min(100, tempEfficiency * 100);
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