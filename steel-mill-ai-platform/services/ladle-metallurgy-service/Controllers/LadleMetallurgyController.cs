using Microsoft.AspNetCore.Mvc;
using LadleMetallurgyService.Models;
using LadleMetallurgyService.Services;

namespace LadleMetallurgyService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LadleMetallurgyController : ControllerBase
{
    private readonly LadleMetallurgyDataService _dataService;
    private readonly ILogger<LadleMetallurgyController> _logger;

    public LadleMetallurgyController(
        LadleMetallurgyDataService dataService,
        ILogger<LadleMetallurgyController> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    [HttpGet("ladles")]
    public async Task<ActionResult<IEnumerable<LadleStatus>>> GetAllLadles()
    {
        try
        {
            var ladles = await _dataService.GetAllLadlesAsync();
            return Ok(ladles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ladle statuses");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("ladles/{ladleId}")]
    public async Task<ActionResult<LadleStatus>> GetLadle(string ladleId)
    {
        try
        {
            var ladle = await _dataService.GetLadleStatusAsync(ladleId);
            if (ladle == null)
            {
                return NotFound($"Ladle {ladleId} not found");
            }
            return Ok(ladle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ladle {LadleId}", ladleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("ladles/{ladleId}/telemetry")]
    public async Task<ActionResult> ReceiveTelemetry(string ladleId, [FromBody] LadleMetallurgyTelemetry telemetry)
    {
        if (telemetry == null)
        {
            return BadRequest("Telemetry data is required");
        }

        try
        {
            telemetry.LadleId = ladleId;
            telemetry.Timestamp = DateTime.UtcNow;

            await _dataService.ProcessTelemetryAsync(telemetry);
            _logger.LogInformation("Telemetry processed for ladle {LadleId}", ladleId);

            return Ok("Telemetry processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing telemetry for ladle {LadleId}", ladleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("ladles/{ladleId}/start-treatment")]
    public async Task<ActionResult> StartTreatment(string ladleId, [FromBody] TreatmentRecipe recipe)
    {
        if (recipe == null)
        {
            return BadRequest("Treatment recipe is required");
        }

        try
        {
            var result = await _dataService.StartTreatmentAsync(ladleId, recipe);
            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            _logger.LogInformation("Treatment started for ladle {LadleId} with recipe {RecipeName}", 
                ladleId, recipe.RecipeName);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting treatment for ladle {LadleId}", ladleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("ladles/{ladleId}/complete-treatment")]
    public async Task<ActionResult> CompleteTreatment(string ladleId)
    {
        try
        {
            var result = await _dataService.CompleteTreatmentAsync(ladleId);
            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            _logger.LogInformation("Treatment completed for ladle {LadleId}", ladleId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing treatment for ladle {LadleId}", ladleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("ladles/{ladleId}/chemistry-status")]
    public async Task<ActionResult> GetChemistryStatus(string ladleId)
    {
        try
        {
            var ladle = await _dataService.GetLadleStatusAsync(ladleId);
            if (ladle == null)
            {
                return NotFound($"Ladle {ladleId} not found");
            }

            var target = TargetChemistry.GetTargetForGrade(ladle.TargetGrade);
            var status = new
            {
                LadleId = ladleId,
                Grade = ladle.TargetGrade.ToString(),
                IsOnTarget = ladle.IsChemistryOnTarget(),
                Current = new
                {
                    Carbon = ladle.CurrentCarbon,
                    Manganese = ladle.CurrentManganese,
                    Phosphorus = ladle.CurrentPhosphorus,
                    Sulfur = ladle.CurrentSulfur,
                    Silicon = ladle.CurrentSilicon
                },
                Target = new
                {
                    Carbon = target.TargetCarbon,
                    Manganese = target.TargetManganese,
                    Phosphorus = target.TargetPhosphorus,
                    Sulfur = target.TargetSulfur,
                    Silicon = target.TargetSilicon
                },
                Deviations = new
                {
                    Carbon = Math.Abs(ladle.CurrentCarbon - target.TargetCarbon),
                    Manganese = Math.Abs(ladle.CurrentManganese - target.TargetManganese),
                    Phosphorus = ladle.CurrentPhosphorus - target.TargetPhosphorus,
                    Sulfur = ladle.CurrentSulfur - target.TargetSulfur
                }
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chemistry status for ladle {LadleId}", ladleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("performance-metrics")]
    public async Task<ActionResult<IEnumerable<LadleMetallurgyPerformanceMetrics>>> GetPerformanceMetrics(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] RebarGrade? grade = null)
    {
        try
        {
            var metrics = await _dataService.GetPerformanceMetricsAsync(from, to, grade);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance metrics");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<IEnumerable<LadleMetallurgyAlert>>> GetAlerts(
        [FromQuery] bool includeResolved = false)
    {
        try
        {
            var alerts = await _dataService.GetAlertsAsync(includeResolved);
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alerts");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("alerts/{alertId}/resolve")]
    public async Task<ActionResult> ResolveAlert(string alertId, [FromBody] string resolutionAction)
    {
        try
        {
            var result = await _dataService.ResolveAlertAsync(alertId, resolutionAction);
            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            _logger.LogInformation("Alert {AlertId} resolved", alertId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving alert {AlertId}", alertId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("recipes")]
    public async Task<ActionResult<IEnumerable<TreatmentRecipe>>> GetTreatmentRecipes(
        [FromQuery] RebarGrade? grade = null)
    {
        try
        {
            var recipes = await _dataService.GetTreatmentRecipesAsync(grade);
            return Ok(recipes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving treatment recipes");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("recipes")]
    public async Task<ActionResult<TreatmentRecipe>> CreateTreatmentRecipe([FromBody] TreatmentRecipe recipe)
    {
        if (recipe == null)
        {
            return BadRequest("Recipe is required");
        }

        try
        {
            var createdRecipe = await _dataService.CreateTreatmentRecipeAsync(recipe);
            _logger.LogInformation("Treatment recipe created: {RecipeName}", recipe.RecipeName);

            return CreatedAtAction(
                nameof(GetTreatmentRecipes),
                new { grade = recipe.TargetGrade },
                createdRecipe);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating treatment recipe");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("health")]
    public ActionResult GetHealthStatus()
    {
        return Ok(new
        {
            Service = "Ladle Metallurgy Service",
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Features = new[]
            {
                "Secondary refining for rebar chemistry",
                "Real-time composition monitoring",
                "Automated treatment recipes",
                "Quality compliance tracking",
                "Performance analytics"
            }
        });
    }
}

// Result classes for service operations
public class ServiceResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}