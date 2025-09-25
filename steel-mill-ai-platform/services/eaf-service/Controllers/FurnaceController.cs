using Microsoft.AspNetCore.Mvc;
using FurnaceService.Models;
using FurnaceService.Services;

namespace FurnaceService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FurnaceController : ControllerBase
{
    private readonly IFurnaceDataService _furnaceDataService;
    private readonly ILogger<FurnaceController> _logger;

    public FurnaceController(IFurnaceDataService furnaceDataService, ILogger<FurnaceController> logger)
    {
        _furnaceDataService = furnaceDataService;
        _logger = logger;
    }

    [HttpGet("{furnaceId}/status")]
    public async Task<IActionResult> GetFurnaceStatus(string furnaceId)
    {
        var status = await _furnaceDataService.GetFurnaceStatusAsync(furnaceId);
        return status != null ? Ok(status) : NotFound();
    }

    [HttpGet("{furnaceId}/telemetry")]
    public async Task<IActionResult> GetFurnaceTelemetry(string furnaceId, [FromQuery] DateTime? startTime, [FromQuery] DateTime? endTime)
    {
        var telemetry = await _furnaceDataService.GetFurnaceTelemetryAsync(furnaceId, startTime, endTime);
        return Ok(telemetry);
    }

    [HttpPost("{furnaceId}/setpoint")]
    public async Task<IActionResult> SetFurnaceTemperature(string furnaceId, [FromBody] TemperatureSetpoint setpoint)
    {
        await _furnaceDataService.SetTemperatureSetpointAsync(furnaceId, setpoint);
        return Accepted();
    }
}