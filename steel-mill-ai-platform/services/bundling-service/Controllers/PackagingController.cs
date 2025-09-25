using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PackagingService.Models;
using PackagingService.Services;

namespace PackagingService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PackagingController : ControllerBase
{
    private readonly IPackagingDataService _dataService;
    private readonly ILogger<PackagingController> _logger;

    public PackagingController(
        IPackagingDataService dataService,
        ILogger<PackagingController> logger
    )
    {
        _dataService = dataService;
        _logger = logger;
    }

    [HttpGet("{lineId}/status")]
    public async Task<IActionResult> GetLineStatus(string lineId)
    {
        var status = await _dataService.GetLineStatusAsync(lineId);
        return status != null ? Ok(status) : NotFound();
    }

    [HttpGet("{lineId}/telemetry")]
    public async Task<IActionResult> GetLineTelemetry(
        string lineId,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime
    )
    {
        var telemetry = await _dataService.GetLineTelemetryAsync(lineId, startTime, endTime);
        return Ok(telemetry);
    }

    [HttpPost("{lineId}/configuration")]
    public async Task<IActionResult> UpdatePackagingConfiguration(
        string lineId,
        [FromBody] PackagingConfiguration config
    )
    {
        await _dataService.UpdatePackagingConfigurationAsync(lineId, config);
        _logger.LogInformation("Updated packaging configuration for line {LineId}", lineId);
        return Accepted();
    }

    [HttpGet("{lineId}/quality-metrics")]
    public async Task<IActionResult> GetQualityControlMetrics(string lineId)
    {
        var metrics = await _dataService.GetQualityControlMetricsAsync(lineId);
        return Ok(metrics);
    }

    [HttpPost("{lineId}/packaging-alert")]
    public async Task<IActionResult> CreatePackagingAlert(
        string lineId,
        [FromBody] PackagingAlert alert
    )
    {
        alert.LineId = lineId;
        await _dataService.CreatePackagingAlertAsync(alert);
        _logger.LogWarning(
            "Packaging alert created for line {LineId}: {AlertType}",
            lineId,
            alert.AlertType
        );
        return Created($"/api/packaging/{lineId}/packaging-alert/{alert.Id}", alert);
    }
}