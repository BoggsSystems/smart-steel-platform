using System.Text.Json;
using Azure.Messaging.EventHubs;
using RollingMillService.Models;
using SteelMillShared.Models;

namespace RollingMillService.Services;

public interface IEventProcessorService
{
    Task ProcessEventAsync(EventData eventData);
}

public class EventProcessorService : IEventProcessorService
{
    private readonly IRollingMillDataService _dataService;
    private readonly ILogger<EventProcessorService> _logger;

    public EventProcessorService(
        IRollingMillDataService dataService,
        ILogger<EventProcessorService> logger
    )
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

            if (baseEvent?.EventType == "RollingMillTelemetry")
            {
                var telemetryData = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    baseEvent.Data
                );

                var telemetry = new RollingMillTelemetry
                {
                    MillId = baseEvent.DeviceId,
                    MotorTorque = GetDoubleValue(telemetryData, "motorTorque"),
                    MotorCurrent = GetDoubleValue(telemetryData, "motorCurrent"),
                    VibrationX = GetDoubleValue(telemetryData, "vibrationX"),
                    VibrationY = GetDoubleValue(telemetryData, "vibrationY"),
                    VibrationZ = GetDoubleValue(telemetryData, "vibrationZ"),
                    RollGap = GetDoubleValue(telemetryData, "rollGap"),
                    RollPressure = GetDoubleValue(telemetryData, "rollPressure"),
                    Temperature = GetDoubleValue(telemetryData, "temperature"),
                    Speed = GetDoubleValue(telemetryData, "speed"),
                    Timestamp = baseEvent.Timestamp
                };

                await _dataService.SaveTelemetryAsync(telemetry);

                // Check for maintenance alerts
                await CheckForMaintenanceAlerts(telemetry);

                _logger.LogInformation("Processed rolling mill telemetry for {MillId}", telemetry.MillId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event");
            throw;
        }
    }

    private async Task CheckForMaintenanceAlerts(RollingMillTelemetry telemetry)
    {
        var alerts = new List<MaintenanceAlert>();

        // High vibration alert
        var totalVibration = Math.Sqrt(
            telemetry.VibrationX * telemetry.VibrationX +
            telemetry.VibrationY * telemetry.VibrationY +
            telemetry.VibrationZ * telemetry.VibrationZ
        );

        if (totalVibration > 0.25)
        {
            alerts.Add(new MaintenanceAlert
            {
                MillId = telemetry.MillId,
                AlertType = "High Vibration",
                Severity = totalVibration > 0.35 ? "High" : "Medium",
                Description = $"Excessive vibration detected: {totalVibration:F3}g",
                VibrationReading = totalVibration
            });
        }

        // High temperature alert
        if (telemetry.Temperature > 95)
        {
            alerts.Add(new MaintenanceAlert
            {
                MillId = telemetry.MillId,
                AlertType = "High Temperature",
                Severity = telemetry.Temperature > 105 ? "High" : "Medium",
                Description = $"High temperature detected: {telemetry.Temperature:F1}°C",
                TemperatureReading = telemetry.Temperature
            });
        }

        // High torque alert
        if (telemetry.MotorTorque > 5800)
        {
            alerts.Add(new MaintenanceAlert
            {
                MillId = telemetry.MillId,
                AlertType = "High Torque",
                Severity = telemetry.MotorTorque > 6200 ? "High" : "Medium",
                Description = $"High motor torque detected: {telemetry.MotorTorque:F0}Nm"
            });
        }

        // Save all alerts
        foreach (var alert in alerts)
        {
            await _dataService.CreateMaintenanceAlertAsync(alert);
        }
    }

    private static double GetDoubleValue(Dictionary<string, object> data, string key)
    {
        if (data.TryGetValue(key, out var value) && value is JsonElement element)
        {
            return element.GetDouble();
        }
        return 0.0;
    }
}