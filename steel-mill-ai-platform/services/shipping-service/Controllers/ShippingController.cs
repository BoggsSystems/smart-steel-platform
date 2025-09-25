using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShippingService.Models;
using ShippingService.Services;

namespace ShippingService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShippingController : ControllerBase
{
    private readonly IShippingDataService _dataService;
    private readonly ILogger<ShippingController> _logger;

    public ShippingController(
        IShippingDataService dataService,
        ILogger<ShippingController> logger
    )
    {
        _dataService = dataService;
        _logger = logger;
    }

    [HttpGet("{shipmentId}/status")]
    public async Task<IActionResult> GetShipmentStatus(string shipmentId)
    {
        var status = await _dataService.GetShipmentStatusAsync(shipmentId);
        return status != null ? Ok(status) : NotFound();
    }

    [HttpGet("{shipmentId}/telemetry")]
    public async Task<IActionResult> GetShipmentTelemetry(
        string shipmentId,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime
    )
    {
        var telemetry = await _dataService.GetShipmentTelemetryAsync(shipmentId, startTime, endTime);
        return Ok(telemetry);
    }

    [HttpPost("{shipmentId}/delivery-schedule")]
    public async Task<IActionResult> UpdateDeliverySchedule(
        string shipmentId,
        [FromBody] DeliverySchedule schedule
    )
    {
        await _dataService.UpdateDeliveryScheduleAsync(shipmentId, schedule);
        _logger.LogInformation("Updated delivery schedule for shipment {ShipmentId}", shipmentId);
        return Accepted();
    }

    [HttpGet("{shipmentId}/logistics-metrics")]
    public async Task<IActionResult> GetLogisticsMetrics(string shipmentId)
    {
        var metrics = await _dataService.GetLogisticsMetricsAsync(shipmentId);
        return Ok(metrics);
    }

    [HttpPost("{shipmentId}/shipping-alert")]
    public async Task<IActionResult> CreateShippingAlert(
        string shipmentId,
        [FromBody] ShippingAlert alert
    )
    {
        alert.ShipmentId = shipmentId;
        await _dataService.CreateShippingAlertAsync(alert);
        _logger.LogWarning(
            "Shipping alert created for shipment {ShipmentId}: {AlertType}",
            shipmentId,
            alert.AlertType
        );
        return Created($"/api/shipping/{shipmentId}/shipping-alert/{alert.Id}", alert);
    }
}