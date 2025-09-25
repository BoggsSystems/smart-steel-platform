using Microsoft.Azure.Cosmos;
using ShippingService.Models;

namespace ShippingService.Services;

public interface IShippingDataService
{
    Task<ShippingStatus?> GetShipmentStatusAsync(string shipmentId);
    Task<IEnumerable<ShippingTelemetry>> GetShipmentTelemetryAsync(
        string shipmentId,
        DateTime? startTime,
        DateTime? endTime
    );
    Task UpdateDeliveryScheduleAsync(string shipmentId, DeliverySchedule schedule);
    Task<LogisticsMetrics?> GetLogisticsMetricsAsync(string shipmentId);
    Task CreateShippingAlertAsync(ShippingAlert alert);
    Task SaveTelemetryAsync(ShippingTelemetry telemetry);
}

public class ShippingDataService : IShippingDataService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _telemetryContainer;
    private readonly Container _statusContainer;
    private readonly Container _alertsContainer;
    private readonly Container _schedulesContainer;
    private readonly ILogger<ShippingDataService> _logger;

    public ShippingDataService(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<ShippingDataService> logger
    )
    {
        _cosmosClient = cosmosClient;
        _logger = logger;

        var databaseName = configuration["CosmosDb:DatabaseName"];
        var telemetryContainerName = configuration["CosmosDb:TelemetryContainer"];
        var statusContainerName = configuration["CosmosDb:StatusContainer"];
        var alertsContainerName = configuration["CosmosDb:AlertsContainer"];
        var schedulesContainerName = configuration["CosmosDb:SchedulesContainer"];

        _telemetryContainer = _cosmosClient.GetContainer(databaseName, telemetryContainerName);
        _statusContainer = _cosmosClient.GetContainer(databaseName, statusContainerName);
        _alertsContainer = _cosmosClient.GetContainer(databaseName, alertsContainerName);
        _schedulesContainer = _cosmosClient.GetContainer(databaseName, schedulesContainerName);
    }

    public async Task<ShippingStatus?> GetShipmentStatusAsync(string shipmentId)
    {
        try
        {
            var response = await _statusContainer.ReadItemAsync<ShippingStatus>(
                shipmentId,
                new PartitionKey(shipmentId)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Shipment status not found for {ShipmentId}", shipmentId);
            return null;
        }
    }

    public async Task<IEnumerable<ShippingTelemetry>> GetShipmentTelemetryAsync(
        string shipmentId,
        DateTime? startTime,
        DateTime? endTime
    )
    {
        var queryText = "SELECT * FROM c WHERE c.ShipmentId = @shipmentId";

        if (startTime.HasValue)
            queryText += " AND c.Timestamp >= @startTime";
        if (endTime.HasValue)
            queryText += " AND c.Timestamp <= @endTime";

        queryText += " ORDER BY c.Timestamp DESC";

        var queryDefinition = new QueryDefinition(queryText).WithParameter("@shipmentId", shipmentId);

        if (startTime.HasValue)
            queryDefinition.WithParameter("@startTime", startTime.Value);
        if (endTime.HasValue)
            queryDefinition.WithParameter("@endTime", endTime.Value);

        var results = new List<ShippingTelemetry>();

        using var iterator = _telemetryContainer.GetItemQueryIterator<ShippingTelemetry>(
            queryDefinition
        );
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public async Task UpdateDeliveryScheduleAsync(string shipmentId, DeliverySchedule schedule)
    {
        schedule.ShipmentId = shipmentId;
        await _schedulesContainer.UpsertItemAsync(schedule, new PartitionKey(shipmentId));
        
        var status = await GetShipmentStatusAsync(shipmentId) ?? new ShippingStatus { ShipmentId = shipmentId };
        status.LastUpdated = DateTime.UtcNow;
        status.EstimatedDelivery = schedule.ScheduledDeliveryTime;

        await _statusContainer.UpsertItemAsync(status, new PartitionKey(shipmentId));
        _logger.LogInformation("Updated delivery schedule for shipment {ShipmentId}", shipmentId);
    }

    public async Task<LogisticsMetrics?> GetLogisticsMetricsAsync(string shipmentId)
    {
        var telemetryData = await GetShipmentTelemetryAsync(
            shipmentId,
            DateTime.UtcNow.AddHours(-24),
            DateTime.UtcNow
        );

        if (!telemetryData.Any())
            return null;

        var metrics = new LogisticsMetrics
        {
            ShipmentId = shipmentId,
            DeliveryEfficiency = CalculateDeliveryEfficiency(telemetryData),
            OnTimeDeliveryRate = CalculateOnTimeDeliveryRate(shipmentId),
            FuelEfficiency = CalculateFuelEfficiency(telemetryData),
            AverageSpeed = telemetryData.Average(t => t.Speed),
            SecurityComplianceScore = CalculateSecurityCompliance(telemetryData),
            CustomerSatisfactionScore = CalculateCustomerSatisfaction(shipmentId),
            CalculatedAt = DateTime.UtcNow
        };

        return metrics;
    }

    public async Task CreateShippingAlertAsync(ShippingAlert alert)
    {
        await _alertsContainer.CreateItemAsync(alert, new PartitionKey(alert.ShipmentId));
        _logger.LogWarning(
            "Created shipping alert {AlertId} for shipment {ShipmentId}: {AlertType}",
            alert.Id,
            alert.ShipmentId,
            alert.AlertType
        );
    }

    public async Task SaveTelemetryAsync(ShippingTelemetry telemetry)
    {
        await _telemetryContainer.CreateItemAsync(telemetry, new PartitionKey(telemetry.ShipmentId));
    }

    private double CalculateDeliveryEfficiency(IEnumerable<ShippingTelemetry> telemetry)
    {
        var avgSpeed = telemetry.Average(t => t.Speed);
        var targetSpeed = 80.0; // Target speed in km/h
        var speedEfficiency = Math.Min(1.0, avgSpeed / targetSpeed);
        
        var fuelEfficiency = telemetry.Average(t => t.FuelLevel);
        var fuelScore = Math.Max(0, fuelEfficiency / 100.0);
        
        return Math.Min(100, (speedEfficiency * 60) + (fuelScore * 40));
    }

    private double CalculateOnTimeDeliveryRate(string shipmentId)
    {
        // Simplified calculation - would normally query historical delivery data
        return 95.0; // 95% on-time delivery rate
    }

    private double CalculateFuelEfficiency(IEnumerable<ShippingTelemetry> telemetry)
    {
        var initialFuel = telemetry.OrderBy(t => t.Timestamp).FirstOrDefault()?.FuelLevel ?? 100;
        var finalFuel = telemetry.OrderByDescending(t => t.Timestamp).FirstOrDefault()?.FuelLevel ?? 90;
        var fuelConsumed = initialFuel - finalFuel;
        var distance = telemetry.Sum(t => t.Speed * (1.0/60.0)); // Approximate distance
        
        if (distance > 0 && fuelConsumed > 0)
        {
            return Math.Min(100, (distance / fuelConsumed) * 10); // Efficiency score
        }
        
        return 85.0; // Default efficiency
    }

    private double CalculateSecurityCompliance(IEnumerable<ShippingTelemetry> telemetry)
    {
        var sealIntactCount = telemetry.Count(t => t.SecuritySealIntact);
        var totalCount = telemetry.Count();
        return ((double)sealIntactCount / totalCount) * 100;
    }

    private double CalculateCustomerSatisfaction(string shipmentId)
    {
        // Simplified calculation - would normally query customer feedback data
        return 4.2; // Out of 5.0
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