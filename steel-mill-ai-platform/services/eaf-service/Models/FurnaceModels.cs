namespace FurnaceService.Models;

public class FurnaceStatus
{
    public string FurnaceId { get; set; } = string.Empty;
    public double CurrentTemperature { get; set; }
    public double TargetTemperature { get; set; }
    public string OperationalStatus { get; set; } = "Idle";
    public double FuelConsumptionRate { get; set; }
    public double OxygenLevel { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class FurnaceTelemetry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FurnaceId { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public double Pressure { get; set; }
    public double FuelFlow { get; set; }
    public double OxygenMix { get; set; }
    public double PowerDraw { get; set; }
    public DateTime Timestamp { get; set; }
}

public class TemperatureSetpoint
{
    public double TargetTemperature { get; set; }
    public double RampRate { get; set; }
}