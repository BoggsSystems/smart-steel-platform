using Azure.Messaging.EventHubs;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SteelMillShared.Services;

public interface IEnhancedEventProcessorService
{
    Task<bool> ProcessEventWithValidationAsync(EventData eventData);
    Task SendToDeadLetterQueueAsync(string eventData, string reason, Exception? exception = null);
}

public class EnhancedEventProcessorService : IEnhancedEventProcessorService
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusSender _deadLetterSender;
    private readonly ILogger<EnhancedEventProcessorService> _logger;

    public EnhancedEventProcessorService(
        ServiceBusClient serviceBusClient,
        ILogger<EnhancedEventProcessorService> logger,
        string deadLetterQueueName = "steel-mill-dead-letters")
    {
        _serviceBusClient = serviceBusClient;
        _deadLetterSender = _serviceBusClient.CreateSender(deadLetterQueueName);
        _logger = logger;
    }

    public async Task<bool> ProcessEventWithValidationAsync(EventData eventData)
    {
        try
        {
            var body = eventData.EventBody.ToString();
            
            if (string.IsNullOrWhiteSpace(body))
            {
                await SendToDeadLetterQueueAsync(body, "Empty event body");
                return false;
            }

            // Validate JSON structure
            if (!IsValidJson(body))
            {
                await SendToDeadLetterQueueAsync(body, "Invalid JSON format");
                return false;
            }

            // Parse and validate event structure
            var eventObject = JsonSerializer.Deserialize<JsonElement>(body);
            
            if (!ValidateEventStructure(eventObject))
            {
                await SendToDeadLetterQueueAsync(body, "Invalid event structure");
                return false;
            }

            // Additional business validation
            if (!ValidateBusinessRules(eventObject))
            {
                await SendToDeadLetterQueueAsync(body, "Business rule validation failed");
                return false;
            }

            _logger.LogDebug("Event validation passed for event: {EventId}", 
                eventObject.TryGetProperty("Id", out var id) ? id.GetString() : "Unknown");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating event");
            await SendToDeadLetterQueueAsync(eventData.EventBody.ToString(), 
                "Validation processing error", ex);
            return false;
        }
    }

    public async Task SendToDeadLetterQueueAsync(string eventData, string reason, Exception? exception = null)
    {
        try
        {
            var deadLetterMessage = new ServiceBusMessage(eventData)
            {
                Subject = "Dead Letter",
                ApplicationProperties =
                {
                    ["DeadLetterReason"] = reason,
                    ["DeadLetterTime"] = DateTimeOffset.UtcNow,
                    ["OriginalEventData"] = eventData
                }
            };

            if (exception != null)
            {
                deadLetterMessage.ApplicationProperties["Exception"] = exception.ToString();
            }

            await _deadLetterSender.SendMessageAsync(deadLetterMessage);
            
            _logger.LogWarning("Event sent to dead letter queue. Reason: {Reason}", reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to dead letter queue");
        }
    }

    private static bool IsValidJson(string jsonString)
    {
        try
        {
            JsonSerializer.Deserialize<JsonElement>(jsonString);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ValidateEventStructure(JsonElement eventObject)
    {
        // Check required fields
        if (!eventObject.TryGetProperty("EventType", out var eventType) || 
            string.IsNullOrEmpty(eventType.GetString()))
        {
            return false;
        }

        if (!eventObject.TryGetProperty("DeviceId", out var deviceId) || 
            string.IsNullOrEmpty(deviceId.GetString()))
        {
            return false;
        }

        if (!eventObject.TryGetProperty("Timestamp", out var timestamp))
        {
            return false;
        }

        // Validate timestamp
        if (timestamp.ValueKind == JsonValueKind.String)
        {
            if (!DateTime.TryParse(timestamp.GetString(), out _))
                return false;
        }

        return true;
    }

    private static bool ValidateBusinessRules(JsonElement eventObject)
    {
        // Timestamp should not be in the future
        if (eventObject.TryGetProperty("Timestamp", out var timestamp) && 
            timestamp.ValueKind == JsonValueKind.String)
        {
            if (DateTime.TryParse(timestamp.GetString(), out var eventTime))
            {
                if (eventTime > DateTime.UtcNow.AddMinutes(5)) // Allow 5 minutes clock skew
                {
                    return false;
                }
            }
        }

        // Event should not be too old (older than 7 days)
        if (eventObject.TryGetProperty("Timestamp", out var ts) && 
            ts.ValueKind == JsonValueKind.String)
        {
            if (DateTime.TryParse(ts.GetString(), out var eventTime))
            {
                if (eventTime < DateTime.UtcNow.AddDays(-7))
                {
                    return false;
                }
            }
        }

        // Validate device ID format
        if (eventObject.TryGetProperty("DeviceId", out var deviceId))
        {
            var deviceIdString = deviceId.GetString();
            if (string.IsNullOrEmpty(deviceIdString) || 
                deviceIdString.Length < 3 || 
                deviceIdString.Length > 50)
            {
                return false;
            }
        }

        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_deadLetterSender != null)
        {
            await _deadLetterSender.DisposeAsync();
        }
        
        if (_serviceBusClient != null)
        {
            await _serviceBusClient.DisposeAsync();
        }
    }
}