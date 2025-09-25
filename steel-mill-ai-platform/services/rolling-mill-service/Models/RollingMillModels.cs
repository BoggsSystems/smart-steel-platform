namespace RollingMillService.Models;

public class RollingMillStatus
{
    public string MillId { get; set; } = string.Empty;
    public double MotorTorque { get; set; }
    public double MotorCurrent { get; set; }
    public double RollGap { get; set; }
    public double RollPressure { get; set; }
    public double Temperature { get; set; }
    public double Speed { get; set; }
    public string OperationalStatus { get; set; } = "Idle";
    public DateTime LastUpdated { get; set; }
}

public class RollingMillTelemetry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MillId { get; set; } = string.Empty;
    public double MotorTorque { get; set; }
    public double MotorCurrent { get; set; }
    public double VibrationX { get; set; }
    public double VibrationY { get; set; }
    public double VibrationZ { get; set; }
    public double RollGap { get; set; }
    public double RollPressure { get; set; }
    public double Temperature { get; set; }
    public double Speed { get; set; }
    public DateTime Timestamp { get; set; }
}

public class RollingConfiguration
{
    public double TargetRollGap { get; set; }
    public double TargetPressure { get; set; }
    public double TargetSpeed { get; set; }
    public double MaxTorque { get; set; }
    public double MaxTemperature { get; set; }
}

public class PerformanceMetrics
{
    public string MillId { get; set; } = string.Empty;
    public double Throughput { get; set; }
    public double QualityScore { get; set; }
    public double Efficiency { get; set; }
    public double AvgVibration { get; set; }
    public double EnergyConsumption { get; set; }
    public DateTime CalculatedAt { get; set; }
}

public class MaintenanceAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MillId { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string Description { get; set; } = string.Empty;
    public double? VibrationReading { get; set; }
    public double? TemperatureReading { get; set; }
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}