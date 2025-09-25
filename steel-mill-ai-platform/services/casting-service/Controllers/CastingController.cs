using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CastingService.Models;
using CastingService.Services;

namespace CastingService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CastingController : ControllerBase
{
    private readonly ICastingDataService _dataService;
    private readonly ILogger<CastingController> _logger;

    public CastingController(
        ICastingDataService dataService,
        ILogger<CastingController> logger
    )
    {
        _dataService = dataService;
        _logger = logger;
    }

    [HttpGet("{casterId}/status")]
    public async Task<IActionResult> GetCasterStatus(string casterId)
    {
        var status = await _dataService.GetCasterStatusAsync(casterId);
        return status != null ? Ok(status) : NotFound();
    }

    [HttpGet("{casterId}/telemetry")]
    public async Task<IActionResult> GetCasterTelemetry(
        string casterId,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime
    )
    {
        var telemetry = await _dataService.GetCasterTelemetryAsync(casterId, startTime, endTime);
        return Ok(telemetry);
    }

    [HttpPost("{casterId}/mold-configuration")]
    public async Task<IActionResult> UpdateMoldConfiguration(
        string casterId,
        [FromBody] MoldConfiguration config
    )
    {
        await _dataService.UpdateMoldConfigurationAsync(casterId, config);
        _logger.LogInformation("Updated mold configuration for caster {CasterId}", casterId);
        return Accepted();
    }

    [HttpGet("{casterId}/performance")]
    public async Task<IActionResult> GetPerformanceMetrics(string casterId)
    {
        var metrics = await _dataService.GetPerformanceMetricsAsync(casterId);
        return Ok(metrics);
    }

    [HttpPost("{casterId}/casting-alert")]
    public async Task<IActionResult> CreateCastingAlert(
        string casterId,
        [FromBody] CastingAlert alert
    )
    {
        alert.CasterId = casterId;
        await _dataService.CreateCastingAlertAsync(alert);
        _logger.LogWarning(
            "Casting alert created for caster {CasterId}: {AlertType}",
            casterId,
            alert.AlertType
        );
        return Created($"/api/casting/{casterId}/casting-alert/{alert.Id}", alert);
    }
}