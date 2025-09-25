using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RollingMillService.Models;
using RollingMillService.Services;

namespace RollingMillService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RebarRollingController : ControllerBase
{
    private readonly IRebarRollingDataService _dataService;
    private readonly ILogger<RebarRollingController> _logger;

    public RebarRollingController(
        IRebarRollingDataService dataService,
        ILogger<RebarRollingController> logger
    )
    {
        _dataService = dataService;
        _logger = logger;
    }

    [HttpGet("mills/{millId}/status")]
    public async Task<ActionResult<RebarRollingStatus>> GetMillStatus(string millId)
    {
        try
        {
            var status = await _dataService.GetMillStatusAsync(millId);
            return status != null ? Ok(status) : NotFound($"Rolling mill {millId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving mill status for {MillId}", millId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("mills/{millId}/telemetry")]
    public async Task<ActionResult<IEnumerable<RebarRollingTelemetry>>> GetMillTelemetry(
        string millId,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] RollingStand? stand = null,
        [FromQuery] RebarGrade? grade = null
    )
    {
        try
        {
            var telemetry = await _dataService.GetMillTelemetryAsync(millId, startTime, endTime, stand, grade);
            return Ok(telemetry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving telemetry for mill {MillId}", millId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("mills/{millId}/configuration")]
    public async Task<ActionResult> UpdateRollingConfiguration(
        string millId,
        [FromBody] RebarRollingConfiguration config
    )
    {
        if (config == null)
        {
            return BadRequest("Configuration is required");
        }

        try
        {
            await _dataService.UpdateRollingConfigurationAsync(millId, config);
            
            _logger.LogInformation("Updated rolling configuration for mill {MillId} - Grade: {Grade}, Size: {Size}, Pattern: {Pattern}", 
                millId, config.TargetGrade, config.TargetSize, config.RibbingPattern);
            
            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration for mill {MillId}", millId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("mills/{millId}/performance")]
    public async Task<ActionResult<RebarRollingPerformanceMetrics>> GetPerformanceMetrics(
        string millId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null
    )
    {
        try
        {
            var metrics = await _dataService.GetPerformanceMetricsAsync(millId, from, to);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance metrics for mill {MillId}", millId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("mills/{millId}/alerts")]
    public async Task<ActionResult<RebarRollingAlert>> CreateRollingAlert(
        string millId,
        [FromBody] RebarRollingAlert alert
    )
    {
        if (alert == null)
        {
            return BadRequest("Alert data is required");
        }

        try
        {
            alert.MillId = millId;
            var createdAlert = await _dataService.CreateRollingAlertAsync(alert);
            
            _logger.LogWarning("Rolling alert created for mill {MillId}: {AlertType} - {Description}",
                millId, alert.AlertType, alert.Description);
            
            return Created($"/api/rebarrolling/mills/{millId}/alerts/{createdAlert.Id}", createdAlert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alert for mill {MillId}", millId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("mills/{millId}/alerts")]
    public async Task<ActionResult<IEnumerable<RebarRollingAlert>>> GetMillAlerts(
        string millId,
        [FromQuery] bool includeResolved = false
    )
    {
        try
        {
            var alerts = await _dataService.GetMillAlertsAsync(millId, includeResolved);
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alerts for mill {MillId}", millId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("alerts/{alertId}/resolve")]
    public async Task<ActionResult> ResolveAlert(string alertId, [FromBody] AlertResolutionRequest request)
    {
        try
        {
            var result = await _dataService.ResolveAlertAsync(alertId, request.ResolutionAction, request.OperatorId);
            
            if (result.Success)
            {
                _logger.LogInformation("Rolling alert {AlertId} resolved by operator {OperatorId}",
                    alertId, request.OperatorId);
                return Ok(result);
            }
            else
            {
                return BadRequest(result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving alert {AlertId}", alertId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("mills/{millId}/telemetry")]
    public async Task<ActionResult> ReceiveTelemetry(string millId, [FromBody] RebarRollingTelemetry telemetry)
    {
        if (telemetry == null)
        {
            return BadRequest("Telemetry data is required");
        }

        try
        {
            telemetry.MillId = millId;
            telemetry.Timestamp = DateTime.UtcNow;

            await _dataService.ProcessTelemetryAsync(telemetry);
            
            _logger.LogDebug("Processed telemetry for mill {MillId}, stand {Stand}", millId, telemetry.StandPosition);
            return Ok("Telemetry processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing telemetry for mill {MillId}", millId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("specifications/ribbing/{size}/{pattern}")]
    public async Task<ActionResult<RibbingSpecification>> GetRibbingSpecification(RebarSize size, RibbingPattern pattern)
    {
        try
        {
            var specification = await _dataService.GetRibbingSpecificationAsync(size, pattern);
            return Ok(specification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ribbing specification for size {Size}, pattern {Pattern}", size, pattern);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("mills/{millId}/validate-ribbing")]
    public async Task<ActionResult> ValidateRibbingQuality(
        string millId, 
        [FromBody] RibbingQualityRequest request
    )
    {
        try
        {
            var result = await _dataService.ValidateRibbingQualityAsync(millId, request.HeatNumber, request.Measurements);
            
            if (result.Success)
            {
                _logger.LogInformation("Ribbing quality validation completed for mill {MillId}, heat {HeatNumber}",
                    millId, request.HeatNumber);
                return Ok(result);
            }
            else
            {
                return BadRequest(result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating ribbing quality for mill {MillId}", millId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("mills/{millId}/rolling-schedule")]
    public async Task<ActionResult> GetRollingSchedule(string millId)
    {
        try
        {
            var status = await _dataService.GetMillStatusAsync(millId);
            if (status == null)
            {
                return NotFound($"Rolling mill {millId} not found");
            }

            // Generate rolling schedule based on current configuration
            var schedule = new
            {
                MillId = millId,
                CurrentGrade = status.CurrentGrade.ToString(),
                CurrentSize = status.CurrentSize.ToString(),
                RollingPattern = status.CurrentPattern.ToString(),
                Stands = status.RollingStands.Select(s => new
                {
                    Stand = s.StandType.ToString(),
                    StandNumber = s.StandNumber,
                    IsOperational = s.IsOperational,
                    MaintenanceStatus = s.MaintenanceStatus,
                    CurrentGap = s.RollGap,
                    CurrentPressure = s.RollPressure
                }),
                LastUpdated = status.LastUpdated
            };

            return Ok(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rolling schedule for mill {MillId}", millId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("specifications/passes/{grade}/{size}")]
    public ActionResult<IEnumerable<RollingPassConfig>> GetRollingPassSchedule(RebarGrade grade, RebarSize size)
    {
        try
        {
            var passes = GenerateRollingPassSchedule(grade, size);
            return Ok(passes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating rolling pass schedule for grade {Grade}, size {Size}", grade, size);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("health")]
    public ActionResult GetHealthStatus()
    {
        return Ok(new
        {
            Service = "Rebar Rolling Mill Service",
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "2.0.0",
            Features = new[]
            {
                "Multi-stand rolling mill control",
                "Rebar ribbing pattern application",
                "Real-time dimensional monitoring",
                "Quality control and defect detection",
                "Performance metrics and analytics",
                "Predictive maintenance alerts",
                "ASTM compliance verification",
                "Energy consumption optimization"
            }
        });
    }

    private IEnumerable<RollingPassConfig> GenerateRollingPassSchedule(RebarGrade grade, RebarSize size)
    {
        var targetDiameter = (double)size;
        var billetDiameter = 160.0; // Standard billet size (160x160mm)
        var totalReduction = (billetDiameter - targetDiameter) / billetDiameter * 100;
        
        var passes = new List<RollingPassConfig>();
        var currentDiameter = billetDiameter;
        var reductionPerPass = new[] { 15, 12, 10, 8, 6, 5 }; // % reduction per pass
        var standTypes = new[] 
        { 
            RollingStand.RoughingStand1, 
            RollingStand.RoughingStand2, 
            RollingStand.IntermediateStand1, 
            RollingStand.IntermediateStand2, 
            RollingStand.FinishingStand, 
            RollingStand.RibbingStand 
        };

        for (int i = 0; i < standTypes.Length - 1; i++) // Exclude ribbing stand from dimensional passes
        {
            var reduction = Math.Min(reductionPerPass[i], (currentDiameter - targetDiameter) / currentDiameter * 100);
            var exitDiameter = currentDiameter - (currentDiameter * reduction / 100);
            
            passes.Add(new RollingPassConfig
            {
                Stand = standTypes[i],
                PassNumber = i + 1,
                EntryDiameter = currentDiameter,
                ExitDiameter = exitDiameter,
                ReductionRatio = reduction,
                RollGap = exitDiameter - 2.0, // 2mm draft
                DraftAngle = 6.0, // Standard draft angle
                RollingForce = EstimateRollingForce(currentDiameter, exitDiameter, grade),
                IsRibbingPass = false
            });

            currentDiameter = exitDiameter;
            
            if (Math.Abs(currentDiameter - targetDiameter) < 0.5)
                break;
        }

        // Add ribbing pass
        passes.Add(new RollingPassConfig
        {
            Stand = RollingStand.RibbingStand,
            PassNumber = passes.Count + 1,
            EntryDiameter = targetDiameter,
            ExitDiameter = targetDiameter,
            ReductionRatio = 0, // No dimensional change, only ribbing
            RollGap = targetDiameter,
            DraftAngle = 0,
            RollingForce = EstimateRibbingForce(targetDiameter, grade),
            IsRibbingPass = true
        });

        return passes;
    }

    private double EstimateRollingForce(double entryDiameter, double exitDiameter, RebarGrade grade)
    {
        // Simplified rolling force calculation
        var area = Math.PI * Math.Pow(entryDiameter / 2, 2);
        var reduction = entryDiameter - exitDiameter;
        var strength = grade switch
        {
            RebarGrade.Grade40 => 275,
            RebarGrade.Grade60 => 420,
            RebarGrade.Grade75 => 520,
            RebarGrade.Grade80 => 550,
            _ => 420
        };
        
        return area * reduction * strength * 0.001; // kN
    }

    private double EstimateRibbingForce(double diameter, RebarGrade grade)
    {
        // Ribbing force is typically lower than rolling force
        return EstimateRollingForce(diameter, diameter * 0.95, grade) * 0.3;
    }
}

// Request/Response DTOs
public class RibbingQualityRequest
{
    public string HeatNumber { get; set; } = string.Empty;
    public Dictionary<string, double> Measurements { get; set; } = new();
}

public class AlertResolutionRequest
{
    public string ResolutionAction { get; set; } = string.Empty;
    public string OperatorId { get; set; } = string.Empty;
}