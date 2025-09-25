using Microsoft.Azure.Cosmos;
using FurnaceService.Models;

namespace FurnaceService.Services;

public interface IFurnaceDataService
{
    Task<FurnaceStatus?> GetFurnaceStatusAsync(string furnaceId);
    Task<IEnumerable<FurnaceTelemetry>> GetFurnaceTelemetryAsync(string furnaceId, DateTime? startTime, DateTime? endTime);
    Task SetTemperatureSetpointAsync(string furnaceId, TemperatureSetpoint setpoint);
    Task SaveTelemetryAsync(FurnaceTelemetry telemetry);
}

public class FurnaceDataService : IFurnaceDataService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _telemetryContainer;
    private readonly Container _statusContainer;
    private readonly ILogger<FurnaceDataService> _logger;

    public FurnaceDataService(CosmosClient cosmosClient, IConfiguration configuration, ILogger<FurnaceDataService> logger)
    {
        _cosmosClient = cosmosClient;
        _logger = logger;
        
        var databaseName = configuration["CosmosDb:DatabaseName"];
        var telemetryContainerName = configuration["CosmosDb:TelemetryContainer"];
        var statusContainerName = configuration["CosmosDb:StatusContainer"];
        
        _telemetryContainer = _cosmosClient.GetContainer(databaseName, telemetryContainerName);
        _statusContainer = _cosmosClient.GetContainer(databaseName, statusContainerName);
    }

    public async Task<FurnaceStatus?> GetFurnaceStatusAsync(string furnaceId)
    {
        try
        {
            var response = await _statusContainer.ReadItemAsync<FurnaceStatus>(furnaceId, new PartitionKey(furnaceId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<FurnaceTelemetry>> GetFurnaceTelemetryAsync(string furnaceId, DateTime? startTime, DateTime? endTime)
    {
        var queryText = "SELECT * FROM c WHERE c.FurnaceId = @furnaceId";
        
        if (startTime.HasValue)
            queryText += " AND c.Timestamp >= @startTime";
        if (endTime.HasValue)
            queryText += " AND c.Timestamp <= @endTime";
            
        queryText += " ORDER BY c.Timestamp DESC";

        var queryDefinition = new QueryDefinition(queryText)
            .WithParameter("@furnaceId", furnaceId);
            
        if (startTime.HasValue)
            queryDefinition.WithParameter("@startTime", startTime.Value);
        if (endTime.HasValue)
            queryDefinition.WithParameter("@endTime", endTime.Value);

        var results = new List<FurnaceTelemetry>();
        
        using var iterator = _telemetryContainer.GetItemQueryIterator<FurnaceTelemetry>(queryDefinition);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        
        return results;
    }

    public async Task SetTemperatureSetpointAsync(string furnaceId, TemperatureSetpoint setpoint)
    {
        var status = await GetFurnaceStatusAsync(furnaceId) ?? new FurnaceStatus { FurnaceId = furnaceId };
        
        status.TargetTemperature = setpoint.TargetTemperature;
        status.LastUpdated = DateTime.UtcNow;
        
        await _statusContainer.UpsertItemAsync(status, new PartitionKey(furnaceId));
    }

    public async Task SaveTelemetryAsync(FurnaceTelemetry telemetry)
    {
        await _telemetryContainer.CreateItemAsync(telemetry, new PartitionKey(telemetry.FurnaceId));
    }
}