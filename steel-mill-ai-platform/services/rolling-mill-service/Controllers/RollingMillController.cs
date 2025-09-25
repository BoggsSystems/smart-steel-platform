using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RollingMillService.Models;
using RollingMillService.Services;

namespace RollingMillService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RollingMillController : ControllerBase
{
    private readonly IRollingMillDataService _dataService;
    private readonly ILogger<RollingMillController> _logger;

    public RollingMillController(
        IRollingMillDataService dataService,
        ILogger<RollingMillController> logger
    )
    {
        _dataService = dataService;
        _logger = logger;
    }

    [HttpGet("{millId}/status")]
    public async Task<IActionResult> GetMillStatus(string millId)
    {
        var status = await _dataService.GetMillStatusAsync(millId);
        return status != null ? Ok(status) : NotFound();
    }

    [HttpGet("{millId}/telemetry")]
    public async Task<IActionResult> GetMillTelemetry(
        string millId,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime
    )
    {
        var telemetry = await _dataService.GetMillTelemetryAsync(millId, startTime, endTime);
        return Ok(telemetry);
    }

    [HttpPost("{millId}/configuration")]
    public async Task<IActionResult> UpdateConfiguration(
        string millId,
        [FromBody] RollingConfiguration config
    )
    {
        await _dataService.UpdateConfigurationAsync(millId, config);
        _logger.LogInformation("Updated configuration for mill {MillId}", millId);
        return Accepted();
    }

    [HttpGet("{millId}/performance")]
    public async Task<IActionResult> GetPerformanceMetrics(string millId)
    {
        var metrics = await _dataService.GetPerformanceMetricsAsync(millId);
        return Ok(metrics);
    }

    [HttpPost("{millId}/maintenance-alert")]
    public async Task<IActionResult> CreateMaintenanceAlert(
        string millId,
        [FromBody] MaintenanceAlert alert
    )
    {
        alert.MillId = millId;
        await _dataService.CreateMaintenanceAlertAsync(alert);
        _logger.LogWarning(
            "Maintenance alert created for mill {MillId}: {AlertType}",
            millId,
            alert.AlertType
        );
        return Created($"/api/rollingmill/{millId}/maintenance-alert/{alert.Id}", alert);
    }
}