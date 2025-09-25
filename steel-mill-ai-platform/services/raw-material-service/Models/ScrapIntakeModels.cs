namespace ScrapIntakeService.Models;

public class ScrapBatch
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BatchNumber { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string ScrapType { get; set; } = string.Empty;
    public double Weight { get; set; }
    public double EstimatedIronContent { get; set; }
    public double EstimatedCarbonContent { get; set; }
    public string QualityGrade { get; set; } = "Unknown";
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Received";
    public List<string> ContaminantWarnings { get; set; } = new();
}

public class WeighBridgeReading
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ScaleId { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public double GrossWeight { get; set; }
    public double TareWeight { get; set; }
    public double NetWeight { get; set; }
    public string VehicleId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class QualityInspection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BatchNumber { get; set; } = string.Empty;
    public string InspectorId { get; set; } = string.Empty;
    public double IronContent { get; set; }
    public double CarbonContent { get; set; }
    public double SiliconContent { get; set; }
    public double SulfurContent { get; set; }
    public double PhosphorusContent { get; set; }
    public List<string> ContaminantsFound { get; set; } = new();
    public string OverallGrade { get; set; } = string.Empty;
    public bool PassedInspection { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime InspectedAt { get; set; } = DateTime.UtcNow;
}

public class ScrapInventory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ScrapType { get; set; } = string.Empty;
    public double TotalWeight { get; set; }
    public double AvailableWeight { get; set; }
    public double AverageIronContent { get; set; }
    public int BatchCount { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class ScrapProcessingRequest
{
    public string BatchNumber { get; set; } = string.Empty;
    public double RequestedWeight { get; set; }
    public string DestinationFurnace { get; set; } = string.Empty;
    public DateTime RequestedBy { get; set; } = DateTime.UtcNow;
}