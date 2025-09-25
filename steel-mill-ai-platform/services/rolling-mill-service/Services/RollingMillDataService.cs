using Microsoft.Azure.Cosmos;
using RollingMillService.Models;

namespace RollingMillService.Services;

public interface IRollingMillDataService
{
    Task<RollingMillStatus?> GetMillStatusAsync(string millId);
    Task<IEnumerable<RollingMillTelemetry>> GetMillTelemetryAsync(
        string millId,
        DateTime? startTime,
        DateTime? endTime
    );
    Task UpdateConfigurationAsync(string millId, RollingConfiguration config);
    Task<PerformanceMetrics?> GetPerformanceMetricsAsync(string millId);
    Task CreateMaintenanceAlertAsync(MaintenanceAlert alert);
    Task SaveTelemetryAsync(RollingMillTelemetry telemetry);
}

public class RollingMillDataService : IRollingMillDataService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _telemetryContainer;
    private readonly Container _statusContainer;
    private readonly Container _alertsContainer;
    private readonly ILogger<RollingMillDataService> _logger;

    public RollingMillDataService(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<RollingMillDataService> logger
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

    public async Task<RollingMillStatus?> GetMillStatusAsync(string millId)
    {
        try
        {
            var response = await _statusContainer.ReadItemAsync<RollingMillStatus>(
                millId,
                new PartitionKey(millId)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Mill status not found for {MillId}", millId);
            return null;
        }
    }

    public async Task<IEnumerable<RollingMillTelemetry>> GetMillTelemetryAsync(
        string millId,
        DateTime? startTime,
        DateTime? endTime
    )
    {
        var queryText = "SELECT * FROM c WHERE c.MillId = @millId";

        if (startTime.HasValue)
            queryText += " AND c.Timestamp >= @startTime";
        if (endTime.HasValue)
            queryText += " AND c.Timestamp <= @endTime";

        queryText += " ORDER BY c.Timestamp DESC";

        var queryDefinition = new QueryDefinition(queryText).WithParameter("@millId", millId);

        if (startTime.HasValue)
            queryDefinition.WithParameter("@startTime", startTime.Value);
        if (endTime.HasValue)
            queryDefinition.WithParameter("@endTime", endTime.Value);

        var results = new List<RollingMillTelemetry>();

        using var iterator = _telemetryContainer.GetItemQueryIterator<RollingMillTelemetry>(
            queryDefinition
        );
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public async Task UpdateConfigurationAsync(string millId, RollingConfiguration config)
    {
        var status = await GetMillStatusAsync(millId) ?? new RollingMillStatus { MillId = millId };

        status.LastUpdated = DateTime.UtcNow;

        await _statusContainer.UpsertItemAsync(status, new PartitionKey(millId));
        _logger.LogInformation("Updated configuration for mill {MillId}", millId);
    }

    public async Task<PerformanceMetrics?> GetPerformanceMetricsAsync(string millId)
    {
        var telemetryData = await GetMillTelemetryAsync(
            millId,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow
        );

        if (!telemetryData.Any())
            return null;

        var metrics = new PerformanceMetrics
        {
            MillId = millId,
            Throughput = telemetryData.Average(t => t.Speed),
            QualityScore = CalculateQualityScore(telemetryData),
            Efficiency = CalculateEfficiency(telemetryData),
            AvgVibration = telemetryData.Average(t =>
                Math.Sqrt(t.VibrationX * t.VibrationX + t.VibrationY * t.VibrationY + t.VibrationZ * t.VibrationZ)
            ),
            EnergyConsumption = telemetryData.Average(t => t.MotorCurrent * 400), // Approximation
            CalculatedAt = DateTime.UtcNow
        };

        return metrics;
    }

    public async Task CreateMaintenanceAlertAsync(MaintenanceAlert alert)
    {
        await _alertsContainer.CreateItemAsync(alert, new PartitionKey(alert.MillId));
        _logger.LogWarning(
            "Created maintenance alert {AlertId} for mill {MillId}: {AlertType}",
            alert.Id,
            alert.MillId,
            alert.AlertType
        );
    }

    public async Task SaveTelemetryAsync(RollingMillTelemetry telemetry)
    {
        await _telemetryContainer.CreateItemAsync(telemetry, new PartitionKey(telemetry.MillId));
    }

    private double CalculateQualityScore(IEnumerable<RollingMillTelemetry> telemetry)
    {
        var vibrationVariance = telemetry.Select(t => t.VibrationX + t.VibrationY + t.VibrationZ)
            .ToArray()
            .Variance();

        var pressureStability = 1.0 - (telemetry.Select(t => t.RollPressure).ToArray().Variance() / 1000);
        var temperatureStability = 1.0 - (telemetry.Select(t => t.Temperature).ToArray().Variance() / 100);

        return Math.Max(0, Math.Min(100, (pressureStability + temperatureStability) * 50));
    }

    private double CalculateEfficiency(IEnumerable<RollingMillTelemetry> telemetry)
    {
        var avgTorque = telemetry.Average(t => t.MotorTorque);
        var avgSpeed = telemetry.Average(t => t.Speed);

        return Math.Min(100, (avgSpeed * 1000) / Math.Max(1, avgTorque));
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