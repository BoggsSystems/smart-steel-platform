using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BundlingService.Models;
using BundlingService.Services;

namespace BundlingService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RebarBundlingController : ControllerBase
{
    private readonly IRebarBundlingDataService _dataService;
    private readonly ILogger<RebarBundlingController> _logger;

    public RebarBundlingController(
        IRebarBundlingDataService dataService,
        ILogger<RebarBundlingController> logger
    )
    {
        _dataService = dataService;
        _logger = logger;
    }

    [HttpGet("lines/{lineId}/status")]
    public async Task<ActionResult<RebarBundlingStatus>> GetLineStatus(string lineId)
    {
        try
        {
            var status = await _dataService.GetLineStatusAsync(lineId);
            return status != null ? Ok(status) : NotFound($"Bundling line {lineId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving line status for {LineId}", lineId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("lines/{lineId}/telemetry")]
    public async Task<ActionResult<IEnumerable<RebarBundlingTelemetry>>> GetLineTelemetry(
        string lineId,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] RebarGrade? grade = null,
        [FromQuery] RebarSize? size = null
    )
    {
        try
        {
            var telemetry = await _dataService.GetLineTelemetryAsync(lineId, startTime, endTime, grade, size);
            return Ok(telemetry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving telemetry for line {LineId}", lineId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("lines/{lineId}/configuration")]
    public async Task<ActionResult> UpdateBundlingConfiguration(
        string lineId,
        [FromBody] BundlingConfiguration config
    )
    {
        if (config == null)
        {
            return BadRequest("Configuration is required");
        }

        try
        {
            config.BundlingLineId = lineId;
            await _dataService.UpdateBundlingConfigurationAsync(lineId, config);
            
            _logger.LogInformation("Updated bundling configuration for line {LineId} - Grade: {Grade}, Size: {Size}", 
                lineId, config.TargetGrade, config.TargetSize);
            
            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration for line {LineId}", lineId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("bundles/create")]
    public async Task<ActionResult<RebarBundle>> CreateBundleFromBars(
        [FromBody] CreateBundleRequest request
    )
    {
        if (request?.Bars == null || !request.Bars.Any())
        {
            return BadRequest("Bars list is required and cannot be empty");
        }

        try
        {
            var bundle = await _dataService.CreateBundleFromBarsAsync(request.Bars, request.CustomerOrderId, request.BundlingLineId);
            
            _logger.LogInformation("Created bundle {BundleId} with {BarCount} bars - Grade: {Grade}, Size: {Size}",
                bundle.BundleId, bundle.BarCount, bundle.Grade, bundle.Size);
            
            return CreatedAtAction(nameof(GetBundle), new { bundleId = bundle.BundleId }, bundle);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bundle");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("bundles/{bundleId}")]
    public async Task<ActionResult<RebarBundle>> GetBundle(string bundleId)
    {
        try
        {
            var bundle = await _dataService.GetBundleAsync(bundleId);
            return bundle != null ? Ok(bundle) : NotFound($"Bundle {bundleId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bundle {BundleId}", bundleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("bundles/{bundleId}/composition")]
    public async Task<ActionResult> GetBundleComposition(string bundleId)
    {
        try
        {
            var bundle = await _dataService.GetBundleAsync(bundleId);
            if (bundle == null)
            {
                return NotFound($"Bundle {bundleId} not found");
            }

            var composition = new
            {
                BundleId = bundle.BundleId,
                Grade = bundle.Grade.ToString(),
                Size = bundle.Size.ToString(),
                Length = bundle.Length.ToString(),
                BarCount = bundle.BarCount,
                TotalWeight = bundle.TotalWeight,
                HeatNumbers = bundle.HeatNumbers.ToList(),
                QualityStatus = bundle.PassedQualityCheck ? "Passed" : "Pending",
                QualityScore = bundle.QualityScore,
                Bars = bundle.Bars.Select(b => new
                {
                    BarId = b.BarId,
                    HeatNumber = b.HeatNumber,
                    Weight = b.Weight,
                    ActualLength = b.ActualLength,
                    QualityStatus = new
                    {
                        TensileTest = b.PassedTensileTest,
                        BendTest = b.PassedBendTest,
                        SurfaceQuality = b.SurfaceQuality,
                        HasDefects = b.HasSurfaceDefects
                    }
                })
            };

            return Ok(composition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving composition for bundle {BundleId}", bundleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("bundles/{bundleId}/quality-verify")]
    public async Task<ActionResult> VerifyBundleQuality(string bundleId, [FromBody] QualityVerificationRequest request)
    {
        try
        {
            var result = await _dataService.VerifyBundleQualityAsync(bundleId, request.InspectorId, request.QualityChecks);
            
            if (result.Success)
            {
                _logger.LogInformation("Bundle {BundleId} quality verification completed - Passed: {Passed}",
                    bundleId, result.QualityPassed);
                return Ok(result);
            }
            else
            {
                return BadRequest(result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying quality for bundle {BundleId}", bundleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("bundles")]
    public async Task<ActionResult<IEnumerable<RebarBundle>>> GetBundles(
        [FromQuery] RebarGrade? grade = null,
        [FromQuery] RebarSize? size = null,
        [FromQuery] BundleStatus? status = null,
        [FromQuery] string? customerOrderId = null,
        [FromQuery] int limit = 100
    )
    {
        try
        {
            var bundles = await _dataService.GetBundlesAsync(grade, size, status, customerOrderId, limit);
            return Ok(bundles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bundles");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("bundles/{bundleId}/tie-complete")]
    public async Task<ActionResult> CompleteBundleTying(string bundleId, [FromBody] TyingCompletionRequest request)
    {
        try
        {
            var result = await _dataService.CompleteBundleTyingAsync(bundleId, request.OperatorId, request.TyingDetails);
            
            if (result.Success)
            {
                _logger.LogInformation("Bundle {BundleId} tying completed by operator {OperatorId}",
                    bundleId, request.OperatorId);
                return Ok(result);
            }
            else
            {
                return BadRequest(result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing tying for bundle {BundleId}", bundleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("bundles/{bundleId}/generate-mtc")]
    public async Task<ActionResult<MillTestCertificate>> GenerateMillTestCertificate(string bundleId)
    {
        try
        {
            var certificate = await _dataService.GenerateMillTestCertificateAsync(bundleId);
            
            if (certificate != null)
            {
                _logger.LogInformation("Generated Mill Test Certificate {CertificateNumber} for bundle {BundleId}",
                    certificate.CertificateNumber, bundleId);
                return Ok(certificate);
            }
            else
            {
                return BadRequest("Unable to generate certificate - bundle may not be ready");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating MTC for bundle {BundleId}", bundleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("bundles/{bundleId}/ship")]
    public async Task<ActionResult> MarkBundleShipped(string bundleId, [FromBody] ShippingRequest request)
    {
        try
        {
            var result = await _dataService.MarkBundleShippedAsync(bundleId, request.ShippingDestination, 
                request.ShippingInstructions, request.OperatorId);
            
            if (result.Success)
            {
                _logger.LogInformation("Bundle {BundleId} marked as shipped to {Destination}",
                    bundleId, request.ShippingDestination);
                return Ok(result);
            }
            else
            {
                return BadRequest(result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking bundle {BundleId} as shipped", bundleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("lines/{lineId}/performance")]
    public async Task<ActionResult<BundlingPerformanceMetrics>> GetPerformanceMetrics(
        string lineId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null
    )
    {
        try
        {
            var metrics = await _dataService.GetPerformanceMetricsAsync(lineId, from, to);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance metrics for line {LineId}", lineId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("lines/{lineId}/alerts")]
    public async Task<ActionResult<RebarBundlingAlert>> CreateBundlingAlert(
        string lineId,
        [FromBody] RebarBundlingAlert alert
    )
    {
        if (alert == null)
        {
            return BadRequest("Alert data is required");
        }

        try
        {
            alert.BundlingLineId = lineId;
            var createdAlert = await _dataService.CreateBundlingAlertAsync(alert);
            
            _logger.LogWarning("Bundling alert created for line {LineId}: {AlertType} - {Description}",
                lineId, alert.AlertType, alert.Description);
            
            return Created($"/api/bundling/lines/{lineId}/alerts/{createdAlert.Id}", createdAlert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alert for line {LineId}", lineId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("lines/{lineId}/alerts")]
    public async Task<ActionResult<IEnumerable<RebarBundlingAlert>>> GetLineAlerts(
        string lineId,
        [FromQuery] bool includeResolved = false
    )
    {
        try
        {
            var alerts = await _dataService.GetLineAlertsAsync(lineId, includeResolved);
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alerts for line {LineId}", lineId);
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
                _logger.LogInformation("Alert {AlertId} resolved by operator {OperatorId}",
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

    [HttpGet("specifications/{grade}/{size}/{length}")]
    public ActionResult<BundleSpecification> GetBundleSpecification(RebarGrade grade, RebarSize size, RebarLength length)
    {
        try
        {
            var specification = RebarBundle.GetBundleSpec(grade, size, length);
            return Ok(specification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bundle specification");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("lines/{lineId}/telemetry")]
    public async Task<ActionResult> ReceiveTelemetry(string lineId, [FromBody] RebarBundlingTelemetry telemetry)
    {
        if (telemetry == null)
        {
            return BadRequest("Telemetry data is required");
        }

        try
        {
            telemetry.BundlingLineId = lineId;
            telemetry.Timestamp = DateTime.UtcNow;

            await _dataService.ProcessTelemetryAsync(telemetry);
            
            _logger.LogDebug("Processed telemetry for line {LineId}", lineId);
            return Ok("Telemetry processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing telemetry for line {LineId}", lineId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("health")]
    public ActionResult GetHealthStatus()
    {
        return Ok(new
        {
            Service = "Rebar Bundling Service",
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "2.0.0",
            Features = new[]
            {
                "Automated rebar bundle creation",
                "Quality verification and compliance",
                "Mill test certificate generation",
                "Real-time performance monitoring",
                "ASTM traceability and documentation",
                "Customer order fulfillment",
                "Equipment status monitoring"
            }
        });
    }
}

// Request/Response DTOs
public class CreateBundleRequest
{
    public List<RebarBar> Bars { get; set; } = new();
    public string CustomerOrderId { get; set; } = string.Empty;
    public string BundlingLineId { get; set; } = string.Empty;
}

public class QualityVerificationRequest
{
    public string InspectorId { get; set; } = string.Empty;
    public Dictionary<string, bool> QualityChecks { get; set; } = new();
}

public class TyingCompletionRequest
{
    public string OperatorId { get; set; } = string.Empty;
    public TyingDetails TyingDetails { get; set; } = new();
}

public class TyingDetails
{
    public int TyingPoints { get; set; }
    public string TyingMaterial { get; set; } = string.Empty;
    public double TyingTension { get; set; }
    public bool QualityApproved { get; set; }
}

public class ShippingRequest
{
    public string ShippingDestination { get; set; } = string.Empty;
    public string ShippingInstructions { get; set; } = string.Empty;
    public string OperatorId { get; set; } = string.Empty;
}

public class AlertResolutionRequest
{
    public string ResolutionAction { get; set; } = string.Empty;
    public string OperatorId { get; set; } = string.Empty;
}

public class ServiceResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}

public class QualityVerificationResult : ServiceResult
{
    public bool QualityPassed { get; set; }
    public double QualityScore { get; set; }
    public List<string> QualityIssues { get; set; } = new();
}