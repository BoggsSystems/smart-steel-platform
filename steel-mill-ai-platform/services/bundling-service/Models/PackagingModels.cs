namespace PackagingService.Models;

public class PackagingStatus
{
    public string LineId { get; set; } = string.Empty;
    public double PackagingSpeed { get; set; }
    public double LabelingAccuracy { get; set; }
    public double QualityControlScore { get; set; }
    public int UnitsPackaged { get; set; }
    public int DefectiveUnits { get; set; }
    public string LineStatus { get; set; } = "Idle";
    public DateTime LastUpdated { get; set; }
}

public class PackagingTelemetry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string LineId { get; set; } = string.Empty;
    public double ConveyorSpeed { get; set; }
    public double LabelingPressure { get; set; }
    public double SealingTemperature { get; set; }
    public double WeightAccuracy { get; set; }
    public double BarcodeReadRate { get; set; }
    public double PackagingEfficiency { get; set; }
    public int UnitsPerMinute { get; set; }
    public bool QualityCheckPassed { get; set; }
    public DateTime Timestamp { get; set; }
}

public class PackagingConfiguration
{
    public double TargetPackagingSpeed { get; set; }
    public double TargetSealingTemperature { get; set; }
    public double TargetLabelingPressure { get; set; }
    public double WeightTolerance { get; set; }
    public double QualityThreshold { get; set; }
}

public class QualityControlMetrics
{
    public string LineId { get; set; } = string.Empty;
    public double OverallQualityScore { get; set; }
    public double DefectRate { get; set; }
    public double LabelingAccuracy { get; set; }
    public double WeightVariance { get; set; }
    public double ThroughputEfficiency { get; set; }
    public double BarcodeAccuracy { get; set; }
    public DateTime CalculatedAt { get; set; }
}

public class PackagingAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string LineId { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string Description { get; set; } = string.Empty;
    public double? QualityReading { get; set; }
    public double? SpeedReading { get; set; }
    public double? TemperatureReading { get; set; }
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}