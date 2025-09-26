using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using QualityControlService.Models;
using QualityControlService.Services;
using QualityControlService.Tests.Fixtures;
using System.Net;

namespace QualityControlService.Tests.Unit.Services;

public class QualityControlDataServiceTests : IClassFixture<QualityTestFixture>
{
    private readonly QualityTestFixture _fixture;
    private readonly Mock<CosmosClient> _mockCosmosClient;
    private readonly Mock<Container> _mockSamplesContainer;
    private readonly Mock<Container> _mockTestResultsContainer;
    private readonly Mock<Container> _mockStationsContainer;
    private readonly Mock<Container> _mockAlertsContainer;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<QualityControlDataService>> _mockLogger;
    private readonly QualityControlDataService _service;

    public QualityControlDataServiceTests(QualityTestFixture fixture)
    {
        _fixture = fixture;
        _mockCosmosClient = fixture.CosmosClientMock;
        _mockSamplesContainer = fixture.SamplesContainerMock;
        _mockTestResultsContainer = fixture.TestResultsContainerMock;
        _mockStationsContainer = fixture.StationsContainerMock;
        _mockAlertsContainer = fixture.AlertsContainerMock;
        _mockConfiguration = fixture.ConfigurationMock;
        _mockLogger = fixture.LoggerMock;
        
        _service = new QualityControlDataService(
            _mockCosmosClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);
    }

    #region GetSampleAsync Tests

    [Fact]
    public async Task GetSampleAsync_ExistingSample_ReturnsSample()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var expectedSample = _fixture.CreateValidQualityTestSample(sampleId);
        
        var mockResponse = new Mock<ItemResponse<QualityTestSample>>();
        mockResponse.Setup(r => r.Resource).Returns(expectedSample);
        
        _mockSamplesContainer.Setup(c => c.ReadItemAsync<QualityTestSample>(
                sampleId, 
                new PartitionKey(sampleId), 
                null, 
                default))
            .ReturnsAsync(mockResponse.Object);

        // Act
        var result = await _service.GetSampleAsync(sampleId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedSample);
        result!.SampleId.Should().Be(sampleId);
    }

    [Fact]
    public async Task GetSampleAsync_NonExistentSample_ReturnsNull()
    {
        // Arrange
        var sampleId = "QTS-9999";
        
        _mockSamplesContainer.Setup(c => c.ReadItemAsync<QualityTestSample>(
                sampleId, 
                new PartitionKey(sampleId), 
                null, 
                default))
            .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

        // Act
        var result = await _service.GetSampleAsync(sampleId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSampleAsync_CosmosException_ThrowsException()
    {
        // Arrange
        var sampleId = "QTS-0001";
        
        _mockSamplesContainer.Setup(c => c.ReadItemAsync<QualityTestSample>(
                sampleId, 
                new PartitionKey(sampleId), 
                null, 
                default))
            .ThrowsAsync(new CosmosException("Server error", HttpStatusCode.InternalServerError, 0, "", 0));

        // Act & Assert
        await FluentActions
            .Invoking(() => _service.GetSampleAsync(sampleId))
            .Should().ThrowAsync<CosmosException>()
            .Where(ex => ex.StatusCode == HttpStatusCode.InternalServerError);
    }

    #endregion

    #region CreateSampleAsync Tests

    [Fact]
    public async Task CreateSampleAsync_ValidSample_CreatesSample()
    {
        // Arrange
        var sample = _fixture.CreateValidQualityTestSample();
        sample.Status = QualityStatus.InQueue; // Should be set by service
        
        var mockResponse = new Mock<ItemResponse<QualityTestSample>>();
        mockResponse.Setup(r => r.Resource).Returns(sample);
        
        _mockSamplesContainer.Setup(c => c.CreateItemAsync<QualityTestSample>(
                It.IsAny<QualityTestSample>(),
                new PartitionKey(sample.SampleId),
                null,
                default))
            .ReturnsAsync(mockResponse.Object);

        // Mock station assignments (simplified)
        var mockStations = new List<QualityControlStation>
        {
            _fixture.CreateValidQualityControlStation("QC-STN-01")
        };
        
        var mockStationQueryResponse = new Mock<FeedResponse<QualityControlStation>>();
        mockStationQueryResponse.Setup(r => r.GetEnumerator()).Returns(mockStations.GetEnumerator());
        mockStationQueryResponse.Setup(r => r.Count).Returns(mockStations.Count);
        
        var mockStationIterator = new Mock<FeedIterator<QualityControlStation>>();
        mockStationIterator.Setup(i => i.HasMoreResults).Returns(true);
        mockStationIterator.Setup(i => i.ReadNextAsync(default))
            .ReturnsAsync(mockStationQueryResponse.Object);
        
        _mockStationsContainer.Setup(c => c.GetItemQueryIterator<QualityControlStation>(
                It.IsAny<QueryDefinition>(), 
                null, 
                null))
            .Returns(mockStationIterator.Object);

        // Act
        var result = await _service.CreateSampleAsync(sample);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(QualityStatus.InQueue);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        
        _mockSamplesContainer.Verify(c => c.CreateItemAsync<QualityTestSample>(
            It.IsAny<QualityTestSample>(),
            new PartitionKey(sample.SampleId),
            null,
            default), Times.Once);
    }

    #endregion

    #region UpdateSampleStatusAsync Tests

    [Fact]
    public async Task UpdateSampleStatusAsync_ExistingSample_UpdatesStatus()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var existingSample = _fixture.CreateValidQualityTestSample(sampleId);
        var newStatus = QualityStatus.Testing;
        var notes = "Started testing process";
        
        var mockReadResponse = new Mock<ItemResponse<QualityTestSample>>();
        mockReadResponse.Setup(r => r.Resource).Returns(existingSample);
        
        _mockSamplesContainer.Setup(c => c.ReadItemAsync<QualityTestSample>(
                sampleId, 
                new PartitionKey(sampleId), 
                null, 
                default))
            .ReturnsAsync(mockReadResponse.Object);

        var mockUpsertResponse = new Mock<ItemResponse<QualityTestSample>>();
        mockUpsertResponse.Setup(r => r.Resource).Returns(existingSample);
        
        _mockSamplesContainer.Setup(c => c.UpsertItemAsync<QualityTestSample>(
                It.IsAny<QualityTestSample>(),
                new PartitionKey(sampleId),
                null,
                default))
            .ReturnsAsync(mockUpsertResponse.Object);

        // Act
        await _service.UpdateSampleStatusAsync(sampleId, newStatus, notes);

        // Assert
        _mockSamplesContainer.Verify(c => c.UpsertItemAsync<QualityTestSample>(
            It.Is<QualityTestSample>(s => 
                s.Status == newStatus && 
                s.TestingStarted.HasValue &&
                s.QualityNotes.Any(n => n.Contains(notes))),
            new PartitionKey(sampleId),
            null,
            default), Times.Once);
    }

    [Fact]
    public async Task UpdateSampleStatusAsync_NonExistentSample_ThrowsInvalidOperationException()
    {
        // Arrange
        var sampleId = "QTS-9999";
        
        _mockSamplesContainer.Setup(c => c.ReadItemAsync<QualityTestSample>(
                sampleId, 
                new PartitionKey(sampleId), 
                null, 
                default))
            .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

        // Act & Assert
        await FluentActions
            .Invoking(() => _service.UpdateSampleStatusAsync(sampleId, QualityStatus.Testing))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Sample {sampleId} not found");
    }

    #endregion

    #region AddTestResultAsync Tests

    [Fact]
    public async Task AddTestResultAsync_ValidTensileTest_AddsResult()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var sample = _fixture.CreateValidQualityTestSample(sampleId);
        var testResult = _fixture.CreateValidTensileTestResult();
        
        var mockSampleResponse = new Mock<ItemResponse<QualityTestSample>>();
        mockSampleResponse.Setup(r => r.Resource).Returns(sample);
        
        _mockSamplesContainer.Setup(c => c.ReadItemAsync<QualityTestSample>(
                sampleId, 
                new PartitionKey(sampleId), 
                null, 
                default))
            .ReturnsAsync(mockSampleResponse.Object);

        var mockTestResultResponse = new Mock<ItemResponse<QualityTestResult>>();
        mockTestResultResponse.Setup(r => r.Resource).Returns(testResult);
        
        _mockTestResultsContainer.Setup(c => c.CreateItemAsync<QualityTestResult>(
                testResult,
                new PartitionKey(sampleId),
                null,
                default))
            .ReturnsAsync(mockTestResultResponse.Object);

        var mockUpsertResponse = new Mock<ItemResponse<QualityTestSample>>();
        mockUpsertResponse.Setup(r => r.Resource).Returns(sample);
        
        _mockSamplesContainer.Setup(c => c.UpsertItemAsync<QualityTestSample>(
                It.IsAny<QualityTestSample>(),
                new PartitionKey(sampleId),
                null,
                default))
            .ReturnsAsync(mockUpsertResponse.Object);

        // Act
        var result = await _service.AddTestResultAsync(sampleId, testResult);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(testResult);
        
        _mockTestResultsContainer.Verify(c => c.CreateItemAsync<QualityTestResult>(
            testResult,
            new PartitionKey(sampleId),
            null,
            default), Times.Once);
    }

    [Fact]
    public async Task AddTestResultAsync_NonExistentSample_ThrowsInvalidOperationException()
    {
        // Arrange
        var sampleId = "QTS-9999";
        var testResult = _fixture.CreateValidTensileTestResult();
        
        _mockSamplesContainer.Setup(c => c.ReadItemAsync<QualityTestSample>(
                sampleId, 
                new PartitionKey(sampleId), 
                null, 
                default))
            .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

        // Act & Assert
        await FluentActions
            .Invoking(() => _service.AddTestResultAsync(sampleId, testResult))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Sample {sampleId} not found");
    }

    #endregion

    #region ValidateASTMComplianceAsync Tests

    [Fact]
    public async Task ValidateASTMComplianceAsync_CompliantSample_ReturnsSuccess()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var sample = _fixture.CreateValidQualityTestSample(sampleId, RebarGrade.Grade60);
        
        // Add compliant test results
        var tensileResult = _fixture.CreateValidTensileTestResult(grade: RebarGrade.Grade60);
        var bendResult = _fixture.CreateValidBendTestResult();
        var dimensionalResult = _fixture.CreateValidDimensionalTestResult();
        var chemicalResult = _fixture.CreateValidChemicalAnalysisResult();
        
        sample.TestResults.AddRange(new QualityTestResult[] 
        { 
            tensileResult, bendResult, dimensionalResult, chemicalResult 
        });
        
        var mockSampleResponse = new Mock<ItemResponse<QualityTestSample>>();
        mockSampleResponse.Setup(r => r.Resource).Returns(sample);
        
        _mockSamplesContainer.Setup(c => c.ReadItemAsync<QualityTestSample>(
                sampleId, 
                new PartitionKey(sampleId), 
                null, 
                default))
            .ReturnsAsync(mockSampleResponse.Object);

        var mockUpsertResponse = new Mock<ItemResponse<QualityTestSample>>();
        mockUpsertResponse.Setup(r => r.Resource).Returns(sample);
        
        _mockSamplesContainer.Setup(c => c.UpsertItemAsync<QualityTestSample>(
                It.IsAny<QualityTestSample>(),
                new PartitionKey(sampleId),
                null,
                default))
            .ReturnsAsync(mockUpsertResponse.Object);

        // Act
        var result = await _service.ValidateASTMComplianceAsync(sampleId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Be("ASTM compliant");
    }

    [Fact]
    public async Task ValidateASTMComplianceAsync_NonCompliantSample_ReturnsFailure()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var sample = _fixture.CreateValidQualityTestSample(sampleId, RebarGrade.Grade60);
        
        // Add non-compliant tensile test result (yield strength too low)
        var tensileResult = _fixture.CreateValidTensileTestResult(grade: RebarGrade.Grade60);
        tensileResult.YieldStrength = 350.0; // Below 420 MPa minimum for Grade 60
        sample.TestResults.Add(tensileResult);
        
        var mockSampleResponse = new Mock<ItemResponse<QualityTestSample>>();
        mockSampleResponse.Setup(r => r.Resource).Returns(sample);
        
        _mockSamplesContainer.Setup(c => c.ReadItemAsync<QualityTestSample>(
                sampleId, 
                new PartitionKey(sampleId), 
                null, 
                default))
            .ReturnsAsync(mockSampleResponse.Object);

        var mockUpsertResponse = new Mock<ItemResponse<QualityTestSample>>();
        mockUpsertResponse.Setup(r => r.Resource).Returns(sample);
        
        _mockSamplesContainer.Setup(c => c.UpsertItemAsync<QualityTestSample>(
                It.IsAny<QualityTestSample>(),
                new PartitionKey(sampleId),
                null,
                default))
            .ReturnsAsync(mockUpsertResponse.Object);

        // Act
        var result = await _service.ValidateASTMComplianceAsync(sampleId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Method always returns success, but with compliance info
        result.Message.Should().Contain("Non-compliant");
        result.Message.Should().Contain("Yield strength");
    }

    [Fact]
    public async Task ValidateASTMComplianceAsync_A706Standard_ValidatesCorrectLimits()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var sample = _fixture.CreateValidQualityTestSample(sampleId, RebarGrade.Grade60);
        sample.Standard = ASTMStandard.A706; // Seismic standard with stricter limits
        
        // Add chemical result that passes A615 but fails A706 (carbon too high)
        var chemicalResult = _fixture.CreateValidChemicalAnalysisResult();
        chemicalResult.CarbonContent = 0.28; // Above A706 limit of 0.25%
        sample.TestResults.Add(chemicalResult);
        
        var mockSampleResponse = new Mock<ItemResponse<QualityTestSample>>();
        mockSampleResponse.Setup(r => r.Resource).Returns(sample);
        
        _mockSamplesContainer.Setup(c => c.ReadItemAsync<QualityTestSample>(
                sampleId, 
                new PartitionKey(sampleId), 
                null, 
                default))
            .ReturnsAsync(mockSampleResponse.Object);

        var mockUpsertResponse = new Mock<ItemResponse<QualityTestSample>>();
        mockUpsertResponse.Setup(r => r.Resource).Returns(sample);
        
        _mockSamplesContainer.Setup(c => c.UpsertItemAsync<QualityTestSample>(
                It.IsAny<QualityTestSample>(),
                new PartitionKey(sampleId),
                null,
                default))
            .ReturnsAsync(mockUpsertResponse.Object);

        // Act
        var result = await _service.ValidateASTMComplianceAsync(sampleId);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Contain("Carbon content");
        result.Message.Should().Contain("0.25"); // A706 carbon limit
    }

    #endregion

    #region GetASTMSpecificationAsync Tests

    [Fact]
    public async Task GetASTMSpecificationAsync_Grade60A615_ReturnsCorrectSpec()
    {
        // Act
        var spec = await _service.GetASTMSpecificationAsync(ASTMStandard.A615, RebarGrade.Grade60, RebarSize.Size5);

        // Assert
        spec.Should().NotBeNull();
        spec.Standard.Should().Be(ASTMStandard.A615);
        spec.Grade.Should().Be(RebarGrade.Grade60);
        spec.Size.Should().Be(RebarSize.Size5);
        spec.MinYieldStrength.Should().Be(420); // Grade 60 = 420 MPa minimum yield
        spec.MinTensileStrength.Should().Be(620);
        spec.MinElongation.Should().Be(9.0);
        spec.MaxCarbon.Should().Be(0.30); // A615 carbon limit
        spec.MaxPhosphorus.Should().Be(0.040);
        spec.MaxSulfur.Should().Be(0.050);
    }

    [Fact]
    public async Task GetASTMSpecificationAsync_Grade75A706_ReturnsCorrectSpec()
    {
        // Act
        var spec = await _service.GetASTMSpecificationAsync(ASTMStandard.A706, RebarGrade.Grade75, RebarSize.Size8);

        // Assert
        spec.Should().NotBeNull();
        spec.Standard.Should().Be(ASTMStandard.A706);
        spec.Grade.Should().Be(RebarGrade.Grade75);
        spec.MinYieldStrength.Should().Be(520); // Grade 75 = 520 MPa minimum yield
        spec.MinTensileStrength.Should().Be(690);
        spec.MinElongation.Should().Be(7.0);
        spec.MaxCarbon.Should().Be(0.25); // A706 stricter carbon limit
        spec.MaxYieldToTensileRatio.Should().Be(0.85); // A706 specific requirement
    }

    [Fact]
    public async Task GetASTMSpecificationAsync_CachesResults()
    {
        // Arrange
        var standard = ASTMStandard.A615;
        var grade = RebarGrade.Grade60;
        var size = RebarSize.Size5;

        // Act
        var spec1 = await _service.GetASTMSpecificationAsync(standard, grade, size);
        var spec2 = await _service.GetASTMSpecificationAsync(standard, grade, size);

        // Assert
        spec1.Should().BeSameAs(spec2); // Should return same cached instance
    }

    #endregion

    #region GetStationStatusAsync Tests

    [Fact]
    public async Task GetStationStatusAsync_ExistingStation_ReturnsStation()
    {
        // Arrange
        var stationId = "QC-STN-01";
        var expectedStation = _fixture.CreateValidQualityControlStation(stationId);
        
        var mockResponse = new Mock<ItemResponse<QualityControlStation>>();
        mockResponse.Setup(r => r.Resource).Returns(expectedStation);
        
        _mockStationsContainer.Setup(c => c.ReadItemAsync<QualityControlStation>(
                stationId, 
                new PartitionKey(stationId), 
                null, 
                default))
            .ReturnsAsync(mockResponse.Object);

        // Act
        var result = await _service.GetStationStatusAsync(stationId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedStation);
    }

    [Fact]
    public async Task GetStationStatusAsync_NonExistentStation_ReturnsNull()
    {
        // Arrange
        var stationId = "QC-STN-99";
        
        _mockStationsContainer.Setup(c => c.ReadItemAsync<QualityControlStation>(
                stationId, 
                new PartitionKey(stationId), 
                null, 
                default))
            .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

        // Act
        var result = await _service.GetStationStatusAsync(stationId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region CreateQualityAlertAsync Tests

    [Fact]
    public async Task CreateQualityAlertAsync_ValidAlert_CreatesAlert()
    {
        // Arrange
        var alert = _fixture.CreateQualityAlert("QC-STN-01", "TEST_FAILURE");
        
        var mockResponse = new Mock<ItemResponse<QualityAlert>>();
        mockResponse.Setup(r => r.Resource).Returns(alert);
        
        _mockAlertsContainer.Setup(c => c.CreateItemAsync<QualityAlert>(
                alert,
                new PartitionKey(alert.StationId),
                null,
                default))
            .ReturnsAsync(mockResponse.Object);

        // Act
        var result = await _service.CreateQualityAlertAsync(alert);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(alert);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        
        _mockAlertsContainer.Verify(c => c.CreateItemAsync<QualityAlert>(
            alert,
            new PartitionKey(alert.StationId),
            null,
            default), Times.Once);
    }

    #endregion

    #region Integration and Performance Tests

    [Fact]
    public async Task ValidateASTMCompliance_AllTestTypes_ValidatesComprehensively()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var sample = _fixture.CreateValidQualityTestSample(sampleId, RebarGrade.Grade60, RebarSize.Size5);
        sample.Standard = ASTMStandard.A615;
        
        // Add all test types with compliant results
        sample.TestResults.Add(_fixture.CreateValidTensileTestResult(grade: RebarGrade.Grade60));
        sample.TestResults.Add(_fixture.CreateValidBendTestResult());
        sample.TestResults.Add(_fixture.CreateValidDimensionalTestResult(size: RebarSize.Size5));
        sample.TestResults.Add(_fixture.CreateValidChemicalAnalysisResult(standard: ASTMStandard.A615));
        
        var mockSampleResponse = new Mock<ItemResponse<QualityTestSample>>();
        mockSampleResponse.Setup(r => r.Resource).Returns(sample);
        
        _mockSamplesContainer.Setup(c => c.ReadItemAsync<QualityTestSample>(
                sampleId, 
                new PartitionKey(sampleId), 
                null, 
                default))
            .ReturnsAsync(mockSampleResponse.Object);

        var mockUpsertResponse = new Mock<ItemResponse<QualityTestSample>>();
        mockUpsertResponse.Setup(r => r.Resource).Returns(sample);
        
        _mockSamplesContainer.Setup(c => c.UpsertItemAsync<QualityTestSample>(
                It.IsAny<QualityTestSample>(),
                new PartitionKey(sampleId),
                null,
                default))
            .ReturnsAsync(mockUpsertResponse.Object);

        // Act
        var result = await _service.ValidateASTMComplianceAsync(sampleId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Be("ASTM compliant");
        
        // Verify certification details would be set
        _mockSamplesContainer.Verify(c => c.UpsertItemAsync<QualityTestSample>(
            It.Is<QualityTestSample>(s => 
                s.ASTMCompliant && 
                s.CertificationDate.HasValue &&
                !string.IsNullOrEmpty(s.CertificationNumber)),
            new PartitionKey(sampleId),
            null,
            default), Times.Once);
    }

    [Fact]
    public async Task ValidateASTMCompliance_MultipleFailures_ReportsAllIssues()
    {
        // Arrange
        var sampleId = "QTS-0001";
        var sample = _fixture.CreateValidQualityTestSample(sampleId, RebarGrade.Grade60, RebarSize.Size5);
        
        // Add failing tensile test (low yield and tensile strength)
        var tensileResult = _fixture.CreateValidTensileTestResult();
        tensileResult.YieldStrength = 350.0; // Below 420 MPa
        tensileResult.TensileStrength = 580.0; // Below 620 MPa
        tensileResult.ElongationAtBreak = 7.0; // Below 9% for Grade 60
        sample.TestResults.Add(tensileResult);
        
        // Add failing chemical analysis (high carbon and phosphorus)
        var chemicalResult = _fixture.CreateValidChemicalAnalysisResult();
        chemicalResult.CarbonContent = 0.35; // Above 0.30%
        chemicalResult.PhosphorusContent = 0.045; // Above 0.040%
        sample.TestResults.Add(chemicalResult);
        
        // Add failing bend test
        var bendResult = _fixture.CreateValidBendTestResult();
        bendResult.CompletedWithoutCracking = false;
        sample.TestResults.Add(bendResult);
        
        var mockSampleResponse = new Mock<ItemResponse<QualityTestSample>>();
        mockSampleResponse.Setup(r => r.Resource).Returns(sample);
        
        _mockSamplesContainer.Setup(c => c.ReadItemAsync<QualityTestSample>(
                It.IsAny<string>(), 
                It.IsAny<PartitionKey>(), 
                null, 
                default))
            .ReturnsAsync(mockSampleResponse.Object);

        var mockUpsertResponse = new Mock<ItemResponse<QualityTestSample>>();
        _mockSamplesContainer.Setup(c => c.UpsertItemAsync<QualityTestSample>(
                It.IsAny<QualityTestSample>(),
                It.IsAny<PartitionKey>(),
                null,
                default))
            .ReturnsAsync(mockUpsertResponse.Object);

        // Act
        var result = await _service.ValidateASTMComplianceAsync(sampleId);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Contain("Non-compliant");
        result.Message.Should().Contain("Yield strength");
        result.Message.Should().Contain("Tensile strength");
        result.Message.Should().Contain("Elongation");
        result.Message.Should().Contain("Carbon content");
        result.Message.Should().Contain("Phosphorus content");
        result.Message.Should().Contain("bend test");
    }

    [Theory]
    [InlineData(RebarGrade.Grade40, 275, 420, 12.0)]
    [InlineData(RebarGrade.Grade60, 420, 620, 9.0)]
    [InlineData(RebarGrade.Grade75, 520, 690, 7.0)]
    [InlineData(RebarGrade.Grade80, 550, 720, 6.0)]
    public async Task GetASTMSpecificationAsync_DifferentGrades_ReturnsCorrectMechanicalProperties(
        RebarGrade grade, double expectedYield, double expectedTensile, double expectedElongation)
    {
        // Act
        var spec = await _service.GetASTMSpecificationAsync(ASTMStandard.A615, grade, RebarSize.Size5);

        // Assert
        spec.MinYieldStrength.Should().Be(expectedYield);
        spec.MinTensileStrength.Should().Be(expectedTensile);
        spec.MinElongation.Should().Be(expectedElongation);
    }

    #endregion
}