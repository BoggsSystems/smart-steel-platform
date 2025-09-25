namespace SteelMillShared.Models;

public class SteelMillEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EventType { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Properties { get; set; } = new();
}

public class MaintenanceAlert
{
    public string AlertId { get; set; } = Guid.NewGuid().ToString();
    public string DeviceId { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Low";
    public string Description { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AnomalyDetection
{
    public string AnomalyId { get; set; } = Guid.NewGuid().ToString();
    public string DeviceId { get; set; } = string.Empty;
    public string AnomalyType { get; set; } = string.Empty;
    public double AnomalyScore { get; set; }
    public Dictionary<string, double> FeatureContributions { get; set; } = new();
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

public class EnergyOptimizationRecommendation
{
    public string RecommendationId { get; set; } = Guid.NewGuid().ToString();
    public string Zone { get; set; } = string.Empty;
    public double PredictedEnergyPeak { get; set; }
    public double CurrentConsumption { get; set; }
    public List<string> RecommendedActions { get; set; } = new();
    public double PotentialSavings { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}