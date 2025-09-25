using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualityControlService.Models;
using QualityControlService.Services;

namespace QualityControlService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QualityControlController : ControllerBase
{
    private readonly IQualityControlDataService _dataService;
    private readonly ILogger<QualityControlController> _logger;

    public QualityControlController(
        IQualityControlDataService dataService,
        ILogger<QualityControlController> logger
    )
    {
        _dataService = dataService;
        _logger = logger;
    }

    [HttpGet("samples/{sampleId}")]
    public async Task<ActionResult<QualityTestSample>> GetSample(string sampleId)
    {
        try
        {
            var sample = await _dataService.GetSampleAsync(sampleId);
            return sample != null ? Ok(sample) : NotFound($"Sample {sampleId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sample {SampleId}", sampleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("samples")]
    public async Task<ActionResult<IEnumerable<QualityTestSample>>> GetSamples(
        [FromQuery] string? batchId = null,
        [FromQuery] string? heatNumber = null,
        [FromQuery] QualityStatus? status = null,
        [FromQuery] int limit = 100
    )
    {
        try
        {
            var samples = await _dataService.GetSamplesAsync(batchId, heatNumber, status, limit);
            return Ok(samples);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quality samples");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("samples")]
    public async Task<ActionResult<QualityTestSample>> CreateSample([FromBody] CreateSampleRequest request)
    {
        if (request == null)
        {
            return BadRequest("Sample data is required");
        }

        try
        {
            var sample = new QualityTestSample
            {
                BatchId = request.BatchId,
                HeatNumber = request.HeatNumber,
                Grade = request.Grade,
                Size = request.Size,
                Standard = request.Standard,
                SampleSource = request.SampleSource,
                SampledBy = request.SampledBy,
                SampleLength = request.SampleLength,
                SampleWeight = request.SampleWeight,
                CustomerOrderId = request.CustomerOrderId,
                CustomerRequirements = request.CustomerRequirements
            };

            var createdSample = await _dataService.CreateSampleAsync(sample);
            
            _logger.LogInformation("Created quality test sample {SampleId} for heat {HeatNumber}", 
                createdSample.SampleId, createdSample.HeatNumber);
            
            return CreatedAtAction(nameof(GetSample), new { sampleId = createdSample.SampleId }, createdSample);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating quality test sample");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("samples/{sampleId}/status")]
    public async Task<ActionResult> UpdateSampleStatus(string sampleId, [FromBody] UpdateSampleStatusRequest request)
    {
        try
        {
            await _dataService.UpdateSampleStatusAsync(sampleId, request.Status, request.Notes);
            
            _logger.LogInformation("Updated sample {SampleId} status to {Status}", sampleId, request.Status);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sample status for {SampleId}", sampleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("samples/{sampleId}/tests/tensile")]
    public async Task<ActionResult<TensileTestResult>> AddTensileTestResult(string sampleId, [FromBody] TensileTestResultRequest request)
    {
        try
        {
            var testResult = new TensileTestResult
            {
                TestType = QualityTestType.TensileTest,
                TestName = "Tensile Strength Test",
                TestDate = DateTime.UtcNow,
                TestEquipmentId = request.TestEquipmentId,
                TechnicianId = request.TechnicianId,
                YieldStrength = request.YieldStrength,
                TensileStrength = request.TensileStrength,
                ElongationAtBreak = request.ElongationAtBreak,
                ReductionInArea = request.ReductionInArea,
                CrossSectionalArea = request.CrossSectionalArea,
                GaugeLength = request.GaugeLength
            };

            // Set up measured values and acceptance criteria
            testResult.MeasuredValues["YieldStrength"] = request.YieldStrength;
            testResult.MeasuredValues["TensileStrength"] = request.TensileStrength;
            testResult.MeasuredValues["Elongation"] = request.ElongationAtBreak;

            var addedResult = await _dataService.AddTestResultAsync(sampleId, testResult);
            
            _logger.LogInformation("Added tensile test result for sample {SampleId}: YS={YieldStrength} MPa, TS={TensileStrength} MPa", 
                sampleId, request.YieldStrength, request.TensileStrength);
            
            return Ok(addedResult);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tensile test result for sample {SampleId}", sampleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("samples/{sampleId}/tests/bend")]
    public async Task<ActionResult<BendTestResult>> AddBendTestResult(string sampleId, [FromBody] BendTestResultRequest request)
    {
        try
        {
            var testResult = new BendTestResult
            {
                TestType = QualityTestType.BendTest,
                TestName = "180° Bend Test",
                TestDate = DateTime.UtcNow,
                TestEquipmentId = request.TestEquipmentId,
                TechnicianId = request.TechnicianId,
                BendAngle = request.BendAngle,
                BendRadius = request.BendRadius,
                PinDiameter = request.PinDiameter,
                CompletedWithoutCracking = request.CompletedWithoutCracking,
                CrackLocations = request.CrackLocations,
                MaxCrackLength = request.MaxCrackLength
            };

            testResult.ASTMBendCompliant = request.CompletedWithoutCracking && request.BendAngle >= 180.0;
            testResult.MeasuredValues["BendAngle"] = request.BendAngle;
            testResult.MeasuredValues["CrackCount"] = request.CrackLocations?.Count ?? 0;

            var addedResult = await _dataService.AddTestResultAsync(sampleId, testResult);
            
            _logger.LogInformation("Added bend test result for sample {SampleId}: {Result} at {BendAngle}°", 
                sampleId, request.CompletedWithoutCracking ? "PASS" : "FAIL", request.BendAngle);
            
            return Ok(addedResult);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding bend test result for sample {SampleId}", sampleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("samples/{sampleId}/tests/dimensional")]
    public async Task<ActionResult<DimensionalTestResult>> AddDimensionalTestResult(string sampleId, [FromBody] DimensionalTestResultRequest request)
    {
        try
        {
            var testResult = new DimensionalTestResult
            {
                TestType = QualityTestType.DimensionalTest,
                TestName = "Dimensional and Ribbing Test",
                TestDate = DateTime.UtcNow,
                TestEquipmentId = request.TestEquipmentId,
                TechnicianId = request.TechnicianId,
                NominalDiameter = request.NominalDiameter,
                ActualDiameter = request.ActualDiameter,
                RibHeight = request.RibHeight,
                RibSpacing = request.RibSpacing,
                RibAngle = request.RibAngle,
                RelativeRibArea = request.RelativeRibArea,
                ActualLength = request.ActualLength,
                Straightness = request.Straightness,
                WeightPerMeter = request.WeightPerMeter,
                SurfaceDefectCount = request.SurfaceDefectCount
            };

            testResult.DiameterTolerance = Math.Abs(request.ActualDiameter - request.NominalDiameter);
            testResult.Ovality = request.Ovality;
            testResult.RibbingCompliant = testResult.RibHeight >= (request.NominalDiameter * 0.043);

            // Set measured values
            testResult.MeasuredValues["ActualDiameter"] = request.ActualDiameter;
            testResult.MeasuredValues["RibHeight"] = request.RibHeight;
            testResult.MeasuredValues["RibSpacing"] = request.RibSpacing;
            testResult.MeasuredValues["Straightness"] = request.Straightness;

            var addedResult = await _dataService.AddTestResultAsync(sampleId, testResult);
            
            _logger.LogInformation("Added dimensional test result for sample {SampleId}: Diameter={ActualDiameter}mm, RibHeight={RibHeight}mm", 
                sampleId, request.ActualDiameter, request.RibHeight);
            
            return Ok(addedResult);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding dimensional test result for sample {SampleId}", sampleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("samples/{sampleId}/tests/chemical")]
    public async Task<ActionResult<ChemicalAnalysisResult>> AddChemicalAnalysisResult(string sampleId, [FromBody] ChemicalAnalysisResultRequest request)
    {
        try
        {
            var testResult = new ChemicalAnalysisResult
            {
                TestType = QualityTestType.ChemicalAnalysis,
                TestName = "Chemical Composition Analysis",
                TestDate = DateTime.UtcNow,
                TestEquipmentId = request.TestEquipmentId,
                TechnicianId = request.TechnicianId,
                CarbonContent = request.CarbonContent,
                ManganeseContent = request.ManganeseContent,
                PhosphorusContent = request.PhosphorusContent,
                SulfurContent = request.SulfurContent,
                SiliconContent = request.SiliconContent,
                AnalysisMethod = request.AnalysisMethod,
                NumberOfReadings = request.NumberOfReadings
            };

            // Set measured values
            testResult.MeasuredValues["Carbon"] = request.CarbonContent;
            testResult.MeasuredValues["Manganese"] = request.ManganeseContent;
            testResult.MeasuredValues["Phosphorus"] = request.PhosphorusContent;
            testResult.MeasuredValues["Sulfur"] = request.SulfurContent;
            testResult.MeasuredValues["Silicon"] = request.SiliconContent;

            var addedResult = await _dataService.AddTestResultAsync(sampleId, testResult);
            
            _logger.LogInformation("Added chemical analysis result for sample {SampleId}: C={Carbon}%, Mn={Manganese}%", 
                sampleId, request.CarbonContent, request.ManganeseContent);
            
            return Ok(addedResult);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding chemical analysis result for sample {SampleId}", sampleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("samples/{sampleId}/tests")]
    public async Task<ActionResult<IEnumerable<QualityTestResult>>> GetSampleTestResults(string sampleId)
    {
        try
        {
            var testResults = await _dataService.GetSampleTestResultsAsync(sampleId);
            return Ok(testResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving test results for sample {SampleId}", sampleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("samples/{sampleId}/validate/astm")]
    public async Task<ActionResult> ValidateASTMCompliance(string sampleId)
    {
        try
        {
            var result = await _dataService.ValidateASTMComplianceAsync(sampleId);
            
            if (result.Success)
            {
                _logger.LogInformation("ASTM compliance validation for sample {SampleId}: {Message}", sampleId, result.Message);
                return Ok(result);
            }
            else
            {
                return BadRequest(result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating ASTM compliance for sample {SampleId}", sampleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("samples/{sampleId}/validate/customer")]
    public async Task<ActionResult> ValidateCustomerSpecification(string sampleId, [FromBody] CustomerSpecificationRequest request)
    {
        try
        {
            var result = await _dataService.ValidateCustomerSpecificationAsync(sampleId, request.Requirements);
            
            if (result.Success)
            {
                _logger.LogInformation("Customer specification validation for sample {SampleId}: {Message}", sampleId, result.Message);
                return Ok(result);
            }
            else
            {
                return BadRequest(result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating customer specification for sample {SampleId}", sampleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("samples/{sampleId}/certificate")]
    public async Task<ActionResult> GenerateQualityCertificate(string sampleId)
    {
        try
        {
            var result = await _dataService.GenerateQualityCertificateAsync(sampleId);
            
            if (result.Success)
            {
                _logger.LogInformation("Generated quality certificate for sample {SampleId}", sampleId);
                return Ok(result);
            }
            else
            {
                return BadRequest(result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating quality certificate for sample {SampleId}", sampleId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("stations/{stationId}/status")]
    public async Task<ActionResult<QualityControlStation>> GetStationStatus(string stationId)
    {
        try
        {
            var station = await _dataService.GetStationStatusAsync(stationId);
            return station != null ? Ok(station) : NotFound($"Station {stationId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving station status for {StationId}", stationId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("stations")]
    public async Task<ActionResult<IEnumerable<QualityControlStation>>> GetAllStations()
    {
        try
        {
            var stations = await _dataService.GetAllStationsAsync();
            return Ok(stations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all quality control stations");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("stations/{stationId}/workload")]
    public async Task<ActionResult> UpdateStationWorkload(string stationId, [FromBody] UpdateWorkloadRequest request)
    {
        try
        {
            await _dataService.UpdateStationWorkloadAsync(stationId, request.CurrentWorkload);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workload for station {StationId}", stationId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("stations/{stationId}/performance")]
    public async Task<ActionResult<QualityPerformanceMetrics>> GetPerformanceMetrics(
        string stationId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null
    )
    {
        try
        {
            var metrics = await _dataService.GetPerformanceMetricsAsync(stationId, from, to);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance metrics for station {StationId}", stationId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("stations/{stationId}/alerts")]
    public async Task<ActionResult<QualityAlert>> CreateQualityAlert(string stationId, [FromBody] QualityAlert alert)
    {
        if (alert == null)
        {
            return BadRequest("Alert data is required");
        }

        try
        {
            alert.StationId = stationId;
            var createdAlert = await _dataService.CreateQualityAlertAsync(alert);
            
            _logger.LogWarning("Quality alert created for station {StationId}: {AlertType} - {Description}",
                stationId, alert.AlertType, alert.Description);
            
            return Created($"/api/qualitycontrol/stations/{stationId}/alerts/{createdAlert.Id}", createdAlert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alert for station {StationId}", stationId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("stations/{stationId}/alerts")]
    public async Task<ActionResult<IEnumerable<QualityAlert>>> GetStationAlerts(
        string stationId,
        [FromQuery] bool includeResolved = false
    )
    {
        try
        {
            var alerts = await _dataService.GetStationAlertsAsync(stationId, includeResolved);
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alerts for station {StationId}", stationId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("alerts/{alertId}/resolve")]
    public async Task<ActionResult> ResolveAlert(string alertId, [FromBody] ResolveAlertRequest request)
    {
        try
        {
            var result = await _dataService.ResolveAlertAsync(alertId, request.ResolutionAction, request.InspectorId);
            
            if (result.Success)
            {
                _logger.LogInformation("Quality alert {AlertId} resolved by inspector {InspectorId}",
                    alertId, request.InspectorId);
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

    [HttpGet("specifications/astm/{standard}/{grade}/{size}")]
    public async Task<ActionResult<ASTMSpecification>> GetASTMSpecification(ASTMStandard standard, RebarGrade grade, RebarSize size)
    {
        try
        {
            var specification = await _dataService.GetASTMSpecificationAsync(standard, grade, size);
            return Ok(specification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ASTM specification for {Standard} Grade {Grade} Size {Size}", standard, grade, size);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("dashboard/summary")]
    public async Task<ActionResult> GetQualityDashboardSummary()
    {
        try
        {
            var stations = await _dataService.GetAllStationsAsync();
            var recentSamples = await _dataService.GetSamplesAsync(limit: 50);
            
            var summary = new
            {
                StationsOnline = stations.Count(s => s.OperationalStatus == "Active"),
                TotalStations = stations.Count(),
                SamplesInQueue = recentSamples.Count(s => s.Status == QualityStatus.InQueue),
                SamplesInTesting = recentSamples.Count(s => s.Status == QualityStatus.Testing),
                SamplesCompleted = recentSamples.Count(s => s.Status == QualityStatus.Completed),
                PassRate = recentSamples.Any() ? recentSamples.Count(s => s.PassedAllTests) * 100.0 / recentSamples.Count(s => s.Status == QualityStatus.Completed) : 0,
                ASTMComplianceRate = recentSamples.Any() ? recentSamples.Count(s => s.ASTMCompliant) * 100.0 / recentSamples.Count(s => s.Status == QualityStatus.Completed) : 0,
                AverageTestTime = recentSamples.Where(s => s.TestingStarted.HasValue && s.TestingCompleted.HasValue)
                                             .Select(s => s.TestingCompleted!.Value - s.TestingStarted!.Value)
                                             .DefaultIfEmpty()
                                             .Average(ts => ts.TotalMinutes),
                Timestamp = DateTime.UtcNow
            };

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quality dashboard summary");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("health")]
    public ActionResult GetHealthStatus()
    {
        return Ok(new
        {
            Service = "Quality Control Service",
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "2.0.0",
            Features = new[]
            {
                "ASTM A615/A706 compliance testing",
                "Tensile strength and bend testing",
                "Dimensional and ribbing verification",
                "Chemical composition analysis",
                "Automated quality scoring",
                "Real-time performance monitoring",
                "Quality certificate generation",
                "Multi-station workflow management"
            }
        });
    }
}

// Request/Response DTOs
public class CreateSampleRequest
{
    public string BatchId { get; set; } = string.Empty;
    public string HeatNumber { get; set; } = string.Empty;
    public RebarGrade Grade { get; set; }
    public RebarSize Size { get; set; }
    public ASTMStandard Standard { get; set; } = ASTMStandard.A615;
    public string SampleSource { get; set; } = string.Empty;
    public string SampledBy { get; set; } = string.Empty;
    public double SampleLength { get; set; }
    public double SampleWeight { get; set; }
    public string CustomerOrderId { get; set; } = string.Empty;
    public Dictionary<string, string> CustomerRequirements { get; set; } = new();
}

public class UpdateSampleStatusRequest
{
    public QualityStatus Status { get; set; }
    public string? Notes { get; set; }
}

public class TensileTestResultRequest
{
    public string TestEquipmentId { get; set; } = string.Empty;
    public string TechnicianId { get; set; } = string.Empty;
    public double YieldStrength { get; set; }
    public double TensileStrength { get; set; }
    public double ElongationAtBreak { get; set; }
    public double ReductionInArea { get; set; }
    public double CrossSectionalArea { get; set; }
    public double GaugeLength { get; set; }
}

public class BendTestResultRequest
{
    public string TestEquipmentId { get; set; } = string.Empty;
    public string TechnicianId { get; set; } = string.Empty;
    public double BendAngle { get; set; } = 180.0;
    public double BendRadius { get; set; }
    public double PinDiameter { get; set; }
    public bool CompletedWithoutCracking { get; set; }
    public List<string> CrackLocations { get; set; } = new();
    public double MaxCrackLength { get; set; }
}

public class DimensionalTestResultRequest
{
    public string TestEquipmentId { get; set; } = string.Empty;
    public string TechnicianId { get; set; } = string.Empty;
    public double NominalDiameter { get; set; }
    public double ActualDiameter { get; set; }
    public double RibHeight { get; set; }
    public double RibSpacing { get; set; }
    public double RibAngle { get; set; }
    public double RelativeRibArea { get; set; }
    public double ActualLength { get; set; }
    public double Straightness { get; set; }
    public double WeightPerMeter { get; set; }
    public double Ovality { get; set; }
    public int SurfaceDefectCount { get; set; }
}

public class ChemicalAnalysisResultRequest
{
    public string TestEquipmentId { get; set; } = string.Empty;
    public string TechnicianId { get; set; } = string.Empty;
    public double CarbonContent { get; set; }
    public double ManganeseContent { get; set; }
    public double PhosphorusContent { get; set; }
    public double SulfurContent { get; set; }
    public double SiliconContent { get; set; }
    public string AnalysisMethod { get; set; } = "OES";
    public int NumberOfReadings { get; set; } = 3;
}

public class CustomerSpecificationRequest
{
    public Dictionary<string, object> Requirements { get; set; } = new();
}

public class UpdateWorkloadRequest
{
    public int CurrentWorkload { get; set; }
}

public class ResolveAlertRequest
{
    public string ResolutionAction { get; set; } = string.Empty;
    public string InspectorId { get; set; } = string.Empty;
}