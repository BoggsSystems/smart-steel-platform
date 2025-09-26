using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using QualityControlService.Controllers;
using QualityControlService.Models;
using QualityControlService.Services;
using QualityControlService.Tests.Fixtures;
using System.Net;

namespace QualityControlService.Tests.Unit.Controllers;

public class QualityControlControllerTests : IClassFixture<QualityTestFixture>
{
    private readonly QualityTestFixture _fixture;
    private readonly Mock<IQualityControlDataService> _mockDataService;
    private readonly Mock<ILogger<QualityControlController>> _mockLogger;
    private readonly QualityControlController _controller;

    public QualityControlControllerTests(QualityTestFixture fixture)
    {
        _fixture = fixture;
        _mockDataService = new Mock<IQualityControlDataService>();
        _mockLogger = new Mock<ILogger<QualityControlController>>();
        
        _controller = new QualityControlController(_mockDataService.Object, _mockLogger.Object);
    }

    #region GetSample Tests

    [Fact]
    public async Task GetSample_ExistingSample_ReturnsOkWithSample()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var expectedSample = _fixture.CreateValidQualityTestSample(sampleId);
        _mockDataService.Setup(s => s.GetSampleAsync(sampleId))
            .ReturnsAsync(expectedSample);

        // Act
        var result = await _controller.GetSample(sampleId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedSample = okResult.Value.Should().BeOfType<QualityTestSample>().Subject;
        returnedSample.SampleId.Should().Be(sampleId);
        returnedSample.Should().BeEquivalentTo(expectedSample);
        
        _mockDataService.Verify(s => s.GetSampleAsync(sampleId), Times.Once);
    }

    [Fact]
    public async Task GetSample_NonExistentSample_ReturnsNotFound()
    {
        // Arrange
        var sampleId = "QTS-9999";
        _mockDataService.Setup(s => s.GetSampleAsync(sampleId))
            .ReturnsAsync((QualityTestSample?)null);

        // Act
        var result = await _controller.GetSample(sampleId);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be($"Sample {sampleId} not found");
    }

    [Fact]
    public async Task GetSample_ServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var sampleId = "QTS-0001";
        _mockDataService.Setup(s => s.GetSampleAsync(sampleId))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _controller.GetSample(sampleId);

        // Assert
        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
        statusResult.Value.Should().Be("Internal server error");
    }

    #endregion

    #region GetSamples Tests

    [Fact]
    public async Task GetSamples_NoFilters_ReturnsAllSamples()
    {
        // Arrange
        var expectedSamples = new List<QualityTestSample>
        {
            _fixture.CreateValidQualityTestSample("QTS-0001"),
            _fixture.CreateValidQualityTestSample("QTS-0002"),
            _fixture.CreateValidQualityTestSample("QTS-0003")
        };
        
        _mockDataService.Setup(s => s.GetSamplesAsync(null, null, null, 100))
            .ReturnsAsync(expectedSamples);

        // Act
        var result = await _controller.GetSamples();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedSamples = okResult.Value.Should().BeOfType<List<QualityTestSample>>().Subject;
        returnedSamples.Should().HaveCount(3);
        returnedSamples.Should().BeEquivalentTo(expectedSamples);
    }

    [Fact]
    public async Task GetSamples_WithBatchIdFilter_ReturnsFilteredSamples()
    {
        // Arrange
        var batchId = "BATCH-001-20241201";
        var expectedSamples = new List<QualityTestSample>
        {
            _fixture.CreateValidQualityTestSample("QTS-0001"),
            _fixture.CreateValidQualityTestSample("QTS-0002")
        };
        expectedSamples.ForEach(s => s.BatchId = batchId);
        
        _mockDataService.Setup(s => s.GetSamplesAsync(batchId, null, null, 100))
            .ReturnsAsync(expectedSamples);

        // Act
        var result = await _controller.GetSamples(batchId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedSamples = okResult.Value.Should().BeOfType<List<QualityTestSample>>().Subject;
        returnedSamples.Should().HaveCount(2);
        returnedSamples.All(s => s.BatchId == batchId).Should().BeTrue();
    }

    [Fact]
    public async Task GetSamples_WithStatusFilter_ReturnsFilteredSamples()
    {
        // Arrange
        var status = QualityStatus.Testing;
        var expectedSamples = new List<QualityTestSample>
        {
            _fixture.CreateValidQualityTestSample("QTS-0001")
        };
        expectedSamples.ForEach(s => s.Status = status);
        
        _mockDataService.Setup(s => s.GetSamplesAsync(null, null, status, 100))
            .ReturnsAsync(expectedSamples);

        // Act
        var result = await _controller.GetSamples(status: status);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedSamples = okResult.Value.Should().BeOfType<List<QualityTestSample>>().Subject;
        returnedSamples.Should().HaveCount(1);
        returnedSamples.All(s => s.Status == status).Should().BeTrue();
    }

    #endregion

    #region CreateSample Tests

    [Fact]
    public async Task CreateSample_ValidRequest_ReturnsCreatedAtActionWithSample()
    {
        // Arrange
        var createRequest = new CreateSampleRequest
        {
            BatchId = "BATCH-001-20241201",
            HeatNumber = "H20241201001",
            Grade = RebarGrade.Grade60,
            Size = RebarSize.Size5,
            Standard = ASTMStandard.A615,
            SampleSource = "Line-1",
            SampledBy = "Tech-01",
            SampleLength = 2.5,
            SampleWeight = 25.3,
            CustomerOrderId = "PO-2024-1234",
            CustomerRequirements = new Dictionary<string, string> { { "SpecialReq", "HighStrength" } }
        };

        var createdSample = _fixture.CreateValidQualityTestSample();
        _mockDataService.Setup(s => s.CreateSampleAsync(It.IsAny<QualityTestSample>()))
            .ReturnsAsync(createdSample);

        // Act
        var result = await _controller.CreateSample(createRequest);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(QualityControlController.GetSample));
        createdResult.RouteValues!["sampleId"].Should().Be(createdSample.SampleId);
        
        var returnedSample = createdResult.Value.Should().BeOfType<QualityTestSample>().Subject;
        returnedSample.Should().BeEquivalentTo(createdSample);
    }

    [Fact]
    public async Task CreateSample_NullRequest_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.CreateSample(null!);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Sample data is required");
    }

    [Fact]
    public async Task CreateSample_ServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var createRequest = new CreateSampleRequest
        {
            BatchId = "BATCH-001-20241201",
            HeatNumber = "H20241201001",
            Grade = RebarGrade.Grade60,
            Size = RebarSize.Size5
        };

        _mockDataService.Setup(s => s.CreateSampleAsync(It.IsAny<QualityTestSample>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.CreateSample(createRequest);

        // Assert
        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region UpdateSampleStatus Tests

    [Fact]
    public async Task UpdateSampleStatus_ValidRequest_ReturnsOk()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var request = new UpdateSampleStatusRequest 
        { 
            Status = QualityStatus.Testing, 
            Notes = "Started testing" 
        };

        _mockDataService.Setup(s => s.UpdateSampleStatusAsync(sampleId, request.Status, request.Notes))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.UpdateSampleStatus(sampleId, request);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockDataService.Verify(s => s.UpdateSampleStatusAsync(sampleId, request.Status, request.Notes), Times.Once);
    }

    [Fact]
    public async Task UpdateSampleStatus_SampleNotFound_ReturnsNotFound()
    {
        // Arrange
        var sampleId = "QTS-9999";
        var request = new UpdateSampleStatusRequest { Status = QualityStatus.Testing };

        _mockDataService.Setup(s => s.UpdateSampleStatusAsync(sampleId, request.Status, request.Notes))
            .ThrowsAsync(new InvalidOperationException("Sample not found"));

        // Act
        var result = await _controller.UpdateSampleStatus(sampleId, request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be("Sample not found");
    }

    #endregion

    #region AddTensileTestResult Tests

    [Fact]
    public async Task AddTensileTestResult_ValidRequest_ReturnsOkWithResult()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var request = new TensileTestResultRequest
        {
            TestEquipmentId = "TENSILE-01",
            TechnicianId = "TECH-01",
            YieldStrength = 450.0,
            TensileStrength = 650.0,
            ElongationAtBreak = 12.5,
            ReductionInArea = 65.0,
            CrossSectionalArea = 201.1,
            GaugeLength = 200.0
        };

        var expectedResult = _fixture.CreateValidTensileTestResult();
        _mockDataService.Setup(s => s.AddTestResultAsync(sampleId, It.IsAny<TensileTestResult>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.AddTensileTestResult(sampleId, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedResult = okResult.Value.Should().BeOfType<TensileTestResult>().Subject;
        returnedResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public async Task AddTensileTestResult_SampleNotFound_ReturnsNotFound()
    {
        // Arrange
        var sampleId = "QTS-9999";
        var request = new TensileTestResultRequest
        {
            TestEquipmentId = "TENSILE-01",
            TechnicianId = "TECH-01",
            YieldStrength = 450.0,
            TensileStrength = 650.0
        };

        _mockDataService.Setup(s => s.AddTestResultAsync(sampleId, It.IsAny<TensileTestResult>()))
            .ThrowsAsync(new InvalidOperationException("Sample not found"));

        // Act
        var result = await _controller.AddTensileTestResult(sampleId, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region AddBendTestResult Tests

    [Fact]
    public async Task AddBendTestResult_PassingTest_ReturnsOkWithResult()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var request = new BendTestResultRequest
        {
            TestEquipmentId = "BEND-01",
            TechnicianId = "TECH-01",
            BendAngle = 180.0,
            BendRadius = 48.0,
            PinDiameter = 48.0,
            CompletedWithoutCracking = true,
            CrackLocations = new List<string>(),
            MaxCrackLength = 0.0
        };

        var expectedResult = _fixture.CreateValidBendTestResult();
        expectedResult.CompletedWithoutCracking = true;
        expectedResult.ASTMBendCompliant = true;
        
        _mockDataService.Setup(s => s.AddTestResultAsync(sampleId, It.IsAny<BendTestResult>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.AddBendTestResult(sampleId, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedResult = okResult.Value.Should().BeOfType<BendTestResult>().Subject;
        returnedResult.CompletedWithoutCracking.Should().BeTrue();
        returnedResult.ASTMBendCompliant.Should().BeTrue();
    }

    [Fact]
    public async Task AddBendTestResult_FailingTest_ReturnsOkWithFailureResult()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var request = new BendTestResultRequest
        {
            TestEquipmentId = "BEND-01",
            TechnicianId = "TECH-01",
            BendAngle = 180.0,
            BendRadius = 48.0,
            PinDiameter = 48.0,
            CompletedWithoutCracking = false,
            CrackLocations = new List<string> { "Surface crack at bend apex" },
            MaxCrackLength = 1.2
        };

        var expectedResult = _fixture.CreateValidBendTestResult();
        expectedResult.CompletedWithoutCracking = false;
        expectedResult.ASTMBendCompliant = false;
        
        _mockDataService.Setup(s => s.AddTestResultAsync(sampleId, It.IsAny<BendTestResult>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.AddBendTestResult(sampleId, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedResult = okResult.Value.Should().BeOfType<BendTestResult>().Subject;
        returnedResult.CompletedWithoutCracking.Should().BeFalse();
        returnedResult.ASTMBendCompliant.Should().BeFalse();
    }

    #endregion

    #region AddDimensionalTestResult Tests

    [Fact]
    public async Task AddDimensionalTestResult_ValidRequest_ReturnsOkWithResult()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var request = new DimensionalTestResultRequest
        {
            TestEquipmentId = "CMM-01",
            TechnicianId = "TECH-01",
            NominalDiameter = 16.0,
            ActualDiameter = 15.95,
            RibHeight = 0.72,
            RibSpacing = 10.5,
            RibAngle = 52.0,
            RelativeRibArea = 0.065,
            ActualLength = 12.0,
            Straightness = 1.2,
            WeightPerMeter = 1.58,
            Ovality = 0.12,
            SurfaceDefectCount = 1
        };

        var expectedResult = _fixture.CreateValidDimensionalTestResult();
        _mockDataService.Setup(s => s.AddTestResultAsync(sampleId, It.IsAny<DimensionalTestResult>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.AddDimensionalTestResult(sampleId, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedResult = okResult.Value.Should().BeOfType<DimensionalTestResult>().Subject;
        returnedResult.Should().BeEquivalentTo(expectedResult);
    }

    #endregion

    #region AddChemicalAnalysisResult Tests

    [Fact]
    public async Task AddChemicalAnalysisResult_ValidComposition_ReturnsOkWithResult()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var request = new ChemicalAnalysisResultRequest
        {
            TestEquipmentId = "OES-01",
            TechnicianId = "TECH-01",
            CarbonContent = 0.25,
            ManganeseContent = 1.20,
            PhosphorusContent = 0.025,
            SulfurContent = 0.030,
            SiliconContent = 0.35,
            AnalysisMethod = "OES",
            NumberOfReadings = 3
        };

        var expectedResult = _fixture.CreateValidChemicalAnalysisResult();
        _mockDataService.Setup(s => s.AddTestResultAsync(sampleId, It.IsAny<ChemicalAnalysisResult>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.AddChemicalAnalysisResult(sampleId, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedResult = okResult.Value.Should().BeOfType<ChemicalAnalysisResult>().Subject;
        returnedResult.Should().BeEquivalentTo(expectedResult);
    }

    #endregion

    #region ValidateASTMCompliance Tests

    [Fact]
    public async Task ValidateASTMCompliance_CompliantSample_ReturnsOk()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var successResult = new ServiceResult 
        { 
            Success = true, 
            Message = "ASTM compliant",
            Data = new { IsCompliant = true, Issues = new List<string>() }
        };
        
        _mockDataService.Setup(s => s.ValidateASTMComplianceAsync(sampleId))
            .ReturnsAsync(successResult);

        // Act
        var result = await _controller.ValidateASTMCompliance(sampleId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedResult = okResult.Value.Should().BeOfType<ServiceResult>().Subject;
        returnedResult.Success.Should().BeTrue();
        returnedResult.Message.Should().Be("ASTM compliant");
    }

    [Fact]
    public async Task ValidateASTMCompliance_NonCompliantSample_ReturnsBadRequest()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var failureResult = new ServiceResult 
        { 
            Success = false, 
            Message = "Non-compliant: Yield strength below minimum",
            Data = new { IsCompliant = false, Issues = new[] { "Yield strength below minimum" } }
        };
        
        _mockDataService.Setup(s => s.ValidateASTMComplianceAsync(sampleId))
            .ReturnsAsync(failureResult);

        // Act
        var result = await _controller.ValidateASTMCompliance(sampleId);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Non-compliant: Yield strength below minimum");
    }

    #endregion

    #region GetStationStatus Tests

    [Fact]
    public async Task GetStationStatus_ExistingStation_ReturnsOkWithStation()
    {
        // Arrange
        var stationId = "QC-STN-01";
        var expectedStation = _fixture.CreateValidQualityControlStation(stationId);
        
        _mockDataService.Setup(s => s.GetStationStatusAsync(stationId))
            .ReturnsAsync(expectedStation);

        // Act
        var result = await _controller.GetStationStatus(stationId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedStation = okResult.Value.Should().BeOfType<QualityControlStation>().Subject;
        returnedStation.StationId.Should().Be(stationId);
    }

    [Fact]
    public async Task GetStationStatus_NonExistentStation_ReturnsNotFound()
    {
        // Arrange
        var stationId = "QC-STN-99";
        
        _mockDataService.Setup(s => s.GetStationStatusAsync(stationId))
            .ReturnsAsync((QualityControlStation?)null);

        // Act
        var result = await _controller.GetStationStatus(stationId);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be($"Station {stationId} not found");
    }

    #endregion

    #region GetQualityDashboardSummary Tests

    [Fact]
    public async Task GetQualityDashboardSummary_ValidData_ReturnsOkWithSummary()
    {
        // Arrange
        var stations = new List<QualityControlStation>
        {
            _fixture.CreateValidQualityControlStation("QC-STN-01"),
            _fixture.CreateValidQualityControlStation("QC-STN-02")
        };
        stations[0].OperationalStatus = "Active";
        stations[1].OperationalStatus = "Maintenance";
        
        var samples = new List<QualityTestSample>
        {
            _fixture.CreateValidQualityTestSample("QTS-0001"),
            _fixture.CreateValidQualityTestSample("QTS-0002"),
            _fixture.CreateValidQualityTestSample("QTS-0003")
        };
        samples[0].Status = QualityStatus.InQueue;
        samples[1].Status = QualityStatus.Testing;
        samples[2].Status = QualityStatus.Completed;
        samples[2].PassedAllTests = true;
        samples[2].ASTMCompliant = true;
        
        _mockDataService.Setup(s => s.GetAllStationsAsync()).ReturnsAsync(stations);
        _mockDataService.Setup(s => s.GetSamplesAsync(null, null, null, 50)).ReturnsAsync(samples);

        // Act
        var result = await _controller.GetQualityDashboardSummary();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var summary = okResult.Value;
        
        // Verify summary structure using reflection or dynamic
        var summaryType = summary!.GetType();
        var stationsOnline = summaryType.GetProperty("StationsOnline")?.GetValue(summary);
        var totalStations = summaryType.GetProperty("TotalStations")?.GetValue(summary);
        
        stationsOnline.Should().Be(1); // Only one active station
        totalStations.Should().Be(2);
    }

    #endregion

    #region GetHealthStatus Tests

    [Fact]
    public void GetHealthStatus_Always_ReturnsOkWithHealthInfo()
    {
        // Act
        var result = _controller.GetHealthStatus();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var healthInfo = okResult.Value;
        
        // Verify health info structure using reflection
        var healthType = healthInfo!.GetType();
        var service = healthType.GetProperty("Service")?.GetValue(healthInfo);
        var status = healthType.GetProperty("Status")?.GetValue(healthInfo);
        var features = healthType.GetProperty("Features")?.GetValue(healthInfo);
        
        service.Should().Be("Quality Control Service");
        status.Should().Be("Healthy");
        features.Should().BeOfType<string[]>();
        
        var featuresArray = features as string[];
        featuresArray.Should().Contain("ASTM A615/A706 compliance testing");
        featuresArray.Should().Contain("Tensile strength and bend testing");
    }

    #endregion

    #region Performance and Edge Case Tests

    [Fact]
    public async Task GetSamples_LargeLimit_HandlesCorrectly()
    {
        // Arrange
        var largeSampleSet = Enumerable.Range(1, 500)
            .Select(i => _fixture.CreateValidQualityTestSample($"QTS-{i:D4}"))
            .ToList();
        
        _mockDataService.Setup(s => s.GetSamplesAsync(null, null, null, 1000))
            .ReturnsAsync(largeSampleSet);

        // Act
        var result = await _controller.GetSamples(limit: 1000);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedSamples = okResult.Value.Should().BeOfType<List<QualityTestSample>>().Subject;
        returnedSamples.Should().HaveCount(500);
    }

    [Fact]
    public async Task Multiple_Concurrent_Requests_HandleCorrectly()
    {
        // Arrange
        var tasks = new List<Task<ActionResult<QualityTestSample>>>();
        
        for (int i = 1; i <= 10; i++)
        {
            var sampleId = $"QTS-{i:D4}";
            var sample = _fixture.CreateValidQualityTestSample(sampleId);
            _mockDataService.Setup(s => s.GetSampleAsync(sampleId))
                .ReturnsAsync(sample);
            
            tasks.Add(_controller.GetSample(sampleId));
        }

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(10);
        results.Should().AllSatisfy(result => 
            result.Result.Should().BeOfType<OkObjectResult>());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task GetSample_InvalidSampleId_StillCallsService(string sampleId)
    {
        // Arrange
        _mockDataService.Setup(s => s.GetSampleAsync(sampleId!))
            .ReturnsAsync((QualityTestSample?)null);

        // Act
        var result = await _controller.GetSample(sampleId!);

        // Assert
        _mockDataService.Verify(s => s.GetSampleAsync(sampleId!), Times.Once);
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateSample_ASTMStandardA706_CreatesCorrectly()
    {
        // Arrange
        var createRequest = new CreateSampleRequest
        {
            BatchId = "BATCH-001-20241201",
            HeatNumber = "H20241201001",
            Grade = RebarGrade.Grade60,
            Size = RebarSize.Size5,
            Standard = ASTMStandard.A706, // Seismic application standard
            SampleSource = "Line-1",
            SampledBy = "Tech-01",
            SampleLength = 2.5,
            SampleWeight = 25.3
        };

        var createdSample = _fixture.CreateValidQualityTestSample();
        createdSample.Standard = ASTMStandard.A706;
        
        _mockDataService.Setup(s => s.CreateSampleAsync(It.IsAny<QualityTestSample>()))
            .ReturnsAsync(createdSample);

        // Act
        var result = await _controller.CreateSample(createRequest);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var returnedSample = createdResult.Value.Should().BeOfType<QualityTestSample>().Subject;
        returnedSample.Standard.Should().Be(ASTMStandard.A706);
    }

    #endregion
}