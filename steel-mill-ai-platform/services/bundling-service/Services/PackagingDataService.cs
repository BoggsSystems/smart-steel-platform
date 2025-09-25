using Microsoft.Azure.Cosmos;
using PackagingService.Models;

namespace PackagingService.Services;

public interface IPackagingDataService
{
    Task<PackagingStatus?> GetLineStatusAsync(string lineId);
    Task<IEnumerable<PackagingTelemetry>> GetLineTelemetryAsync(
        string lineId,
        DateTime? startTime,
        DateTime? endTime
    );
    Task UpdatePackagingConfigurationAsync(string lineId, PackagingConfiguration config);
    Task<QualityControlMetrics?> GetQualityControlMetricsAsync(string lineId);
    Task CreatePackagingAlertAsync(PackagingAlert alert);
    Task SaveTelemetryAsync(PackagingTelemetry telemetry);
}

public class PackagingDataService : IPackagingDataService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _telemetryContainer;
    private readonly Container _statusContainer;
    private readonly Container _alertsContainer;
    private readonly ILogger<PackagingDataService> _logger;

    public PackagingDataService(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<PackagingDataService> logger
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

    public async Task<PackagingStatus?> GetLineStatusAsync(string lineId)
    {
        try
        {
            var response = await _statusContainer.ReadItemAsync<PackagingStatus>(
                lineId,
                new PartitionKey(lineId)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Packaging line status not found for {LineId}", lineId);
            return null;
        }
    }

    public async Task<IEnumerable<PackagingTelemetry>> GetLineTelemetryAsync(
        string lineId,
        DateTime? startTime,
        DateTime? endTime
    )
    {
        var queryText = "SELECT * FROM c WHERE c.LineId = @lineId";

        if (startTime.HasValue)
            queryText += " AND c.Timestamp >= @startTime";
        if (endTime.HasValue)
            queryText += " AND c.Timestamp <= @endTime";

        queryText += " ORDER BY c.Timestamp DESC";

        var queryDefinition = new QueryDefinition(queryText).WithParameter("@lineId", lineId);

        if (startTime.HasValue)
            queryDefinition.WithParameter("@startTime", startTime.Value);
        if (endTime.HasValue)
            queryDefinition.WithParameter("@endTime", endTime.Value);

        var results = new List<PackagingTelemetry>();

        using var iterator = _telemetryContainer.GetItemQueryIterator<PackagingTelemetry>(
            queryDefinition
        );
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public async Task UpdatePackagingConfigurationAsync(string lineId, PackagingConfiguration config)
    {
        var status = await GetLineStatusAsync(lineId) ?? new PackagingStatus { LineId = lineId };

        status.LastUpdated = DateTime.UtcNow;

        await _statusContainer.UpsertItemAsync(status, new PartitionKey(lineId));
        _logger.LogInformation("Updated packaging configuration for line {LineId}", lineId);
    }

    public async Task<QualityControlMetrics?> GetQualityControlMetricsAsync(string lineId)
    {
        var telemetryData = await GetLineTelemetryAsync(
            lineId,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow
        );

        if (!telemetryData.Any())
            return null;

        var metrics = new QualityControlMetrics
        {
            LineId = lineId,
            OverallQualityScore = CalculateOverallQualityScore(telemetryData),
            DefectRate = CalculateDefectRate(telemetryData),
            LabelingAccuracy = telemetryData.Average(t => t.BarcodeReadRate),
            WeightVariance = telemetryData.Select(t => t.WeightAccuracy).ToArray().Variance(),
            ThroughputEfficiency = CalculateThroughputEfficiency(telemetryData),
            BarcodeAccuracy = telemetryData.Average(t => t.BarcodeReadRate),
            CalculatedAt = DateTime.UtcNow
        };

        return metrics;
    }

    public async Task CreatePackagingAlertAsync(PackagingAlert alert)
    {
        await _alertsContainer.CreateItemAsync(alert, new PartitionKey(alert.LineId));
        _logger.LogWarning(
            "Created packaging alert {AlertId} for line {LineId}: {AlertType}",
            alert.Id,
            alert.LineId,
            alert.AlertType
        );
    }

    public async Task SaveTelemetryAsync(PackagingTelemetry telemetry)
    {
        await _telemetryContainer.CreateItemAsync(telemetry, new PartitionKey(telemetry.LineId));
    }

    private double CalculateOverallQualityScore(IEnumerable<PackagingTelemetry> telemetry)
    {
        var qualityPassedCount = telemetry.Count(t => t.QualityCheckPassed);
        var totalCount = telemetry.Count();
        var passRate = (double)qualityPassedCount / totalCount;
        
        var avgBarcode = telemetry.Average(t => t.BarcodeReadRate);
        var avgWeight = telemetry.Average(t => t.WeightAccuracy);
        var avgEfficiency = telemetry.Average(t => t.PackagingEfficiency);
        
        return Math.Min(100, (passRate * 40) + (avgBarcode * 0.2) + (avgWeight * 0.2) + (avgEfficiency * 0.2));
    }

    private double CalculateDefectRate(IEnumerable<PackagingTelemetry> telemetry)
    {
        var qualityPassedCount = telemetry.Count(t => t.QualityCheckPassed);
        var totalCount = telemetry.Count();
        return ((double)(totalCount - qualityPassedCount) / totalCount) * 100;
    }

    private double CalculateThroughputEfficiency(IEnumerable<PackagingTelemetry> telemetry)
    {
        var avgUnitsPerMinute = telemetry.Average(t => t.UnitsPerMinute);
        var targetUnitsPerMinute = 50.0; // Target throughput
        
        return Math.Min(100, (avgUnitsPerMinute / targetUnitsPerMinute) * 100);
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