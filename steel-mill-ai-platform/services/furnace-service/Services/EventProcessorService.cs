using Azure.Messaging.EventHubs;
using System.Text.Json;
using FurnaceService.Models;
using SteelMillShared.Models;

namespace FurnaceService.Services;

public interface IEventProcessorService
{
    Task ProcessEventAsync(EventData eventData);
}

public class EventProcessorService : IEventProcessorService
{
    private readonly IFurnaceDataService _dataService;
    private readonly ILogger<EventProcessorService> _logger;

    public EventProcessorService(IFurnaceDataService dataService, ILogger<EventProcessorService> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    public async Task ProcessEventAsync(EventData eventData)
    {
        try
        {
            var body = eventData.EventBody.ToString();
            var baseEvent = JsonSerializer.Deserialize<SteelMillEvent>(body);
            
            if (baseEvent?.EventType == "FurnaceTelemetry")
            {
                var telemetryData = JsonSerializer.Deserialize<Dictionary<string, object>>(baseEvent.Data);
                
                var telemetry = new FurnaceTelemetry
                {
                    FurnaceId = baseEvent.DeviceId,
                    Temperature = GetDoubleValue(telemetryData, "temperature"),
                    Pressure = GetDoubleValue(telemetryData, "pressure"),
                    FuelFlow = GetDoubleValue(telemetryData, "fuelFlow"),
                    OxygenMix = GetDoubleValue(telemetryData, "oxygenMix"),
                    PowerDraw = GetDoubleValue(telemetryData, "powerDraw"),
                    Timestamp = baseEvent.Timestamp
                };
                
                await _dataService.SaveTelemetryAsync(telemetry);
                _logger.LogInformation($"Processed furnace telemetry for {telemetry.FurnaceId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event");
            throw;
        }
    }
    
    private double GetDoubleValue(Dictionary<string, object> data, string key)
    {
        if (data.TryGetValue(key, out var value) && value is JsonElement element)
        {
            return element.GetDouble();
        }
        return 0.0;
    }
}