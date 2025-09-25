namespace CastingService.Models;

public class CastingStatus
{
    public string CasterId { get; set; } = string.Empty;
    public double TundishTemperature { get; set; }
    public double MoldTemperature { get; set; }
    public double CastingSpeed { get; set; }
    public double LiquidLevel { get; set; }
    public double SteelFlow { get; set; }
    public string CastingState { get; set; } = "Idle";
    public DateTime LastUpdated { get; set; }
}

public class CastingTelemetry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CasterId { get; set; } = string.Empty;
    public double TundishTemperature { get; set; }
    public double MoldTemperature { get; set; }
    public double MoldCoolingWaterFlow { get; set; }
    public double MoldCoolingWaterTemp { get; set; }
    public double CastingSpeed { get; set; }
    public double LiquidLevel { get; set; }
    public double SteelFlow { get; set; }
    public double VibrationLevel { get; set; }
    public DateTime Timestamp { get; set; }
}

public class MoldConfiguration
{
    public double TargetTemperature { get; set; }
    public double TargetCoolingRate { get; set; }
    public double TargetCastingSpeed { get; set; }
    public double MaxVibrationLevel { get; set; }
    public double OptimalSteelFlow { get; set; }
}

public class CastingPerformanceMetrics
{
    public string CasterId { get; set; } = string.Empty;
    public double CastingEfficiency { get; set; }
    public double QualityIndex { get; set; }
    public double TemperatureStability { get; set; }
    public double CoolingEfficiency { get; set; }
    public double VibrationLevel { get; set; }
    public double ThroughputRate { get; set; }
    public DateTime CalculatedAt { get; set; }
}

public class CastingAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CasterId { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string Description { get; set; } = string.Empty;
    public double? TemperatureReading { get; set; }
    public double? VibrationReading { get; set; }
    public double? FlowReading { get; set; }
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}