namespace ShippingService.Models;

public class ShippingStatus
{
    public string ShipmentId { get; set; } = string.Empty;
    public string CurrentLocation { get; set; } = string.Empty;
    public string ShippingStatus { get; set; } = "Pending";
    public DateTime EstimatedDelivery { get; set; }
    public DateTime ActualShipDate { get; set; }
    public double LoadWeight { get; set; }
    public string CarrierInfo { get; set; } = string.Empty;
    public string TrackingNumber { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}

public class ShippingTelemetry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ShipmentId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Speed { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double FuelLevel { get; set; }
    public string VehicleStatus { get; set; } = "In Transit";
    public bool SecuritySealIntact { get; set; } = true;
    public DateTime Timestamp { get; set; }
}

public class DeliverySchedule
{
    public string DeliveryId { get; set; } = Guid.NewGuid().ToString();
    public string ShipmentId { get; set; } = string.Empty;
    public string DestinationAddress { get; set; } = string.Empty;
    public DateTime ScheduledDeliveryTime { get; set; }
    public string CustomerContact { get; set; } = string.Empty;
    public string DeliveryInstructions { get; set; } = string.Empty;
    public string DeliveryStatus { get; set; } = "Scheduled";
}

public class LogisticsMetrics
{
    public string ShipmentId { get; set; } = string.Empty;
    public double DeliveryEfficiency { get; set; }
    public double OnTimeDeliveryRate { get; set; }
    public double FuelEfficiency { get; set; }
    public double AverageSpeed { get; set; }
    public double SecurityComplianceScore { get; set; }
    public double CustomerSatisfactionScore { get; set; }
    public DateTime CalculatedAt { get; set; }
}

public class ShippingAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ShipmentId { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string Description { get; set; } = string.Empty;
    public double? LocationLat { get; set; }
    public double? LocationLon { get; set; }
    public double? TemperatureReading { get; set; }
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}