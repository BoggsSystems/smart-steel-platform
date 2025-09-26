using Xunit;
using FluentAssertions;
using QualityControlService.Models;
using QualityControlService.Tests.Fixtures;
using System.Text.Json;

namespace QualityControlService.Tests.Unit.Models;

public class QualityControlModelsTests : IClassFixture<QualityTestFixture>
{
    private readonly QualityTestFixture _fixture;

    public QualityControlModelsTests(QualityTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region QualityTestSample Tests

    [Fact]
    public void QualityTestSample_DefaultConstructor_SetsDefaultValues()
    {
        // Act
        var sample = new QualityTestSample();

        // Assert
        sample.SampleId.Should().NotBeEmpty();
        sample.Status.Should().Be(QualityStatus.InQueue);
        sample.Standard.Should().Be(ASTMStandard.A615);
        sample.TestResults.Should().NotBeNull().And.BeEmpty();
        sample.FailureReasons.Should().NotBeNull().And.BeEmpty();
        sample.QualityNotes.Should().NotBeNull().And.BeEmpty();
        sample.CustomerRequirements.Should().NotBeNull().And.BeEmpty();
        sample.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void QualityTestSample_PropertyAssignment_WorksCorrectly()
    {
        // Arrange
        var sample = new QualityTestSample();
        var batchId = "BATCH-001-20241201";
        var heatNumber = "H20241201001";
        var grade = RebarGrade.Grade60;
        var size = RebarSize.Size5;

        // Act
        sample.BatchId = batchId;
        sample.HeatNumber = heatNumber;
        sample.Grade = grade;
        sample.Size = size;
        sample.Status = QualityStatus.Testing;

        // Assert
        sample.BatchId.Should().Be(batchId);
        sample.HeatNumber.Should().Be(heatNumber);
        sample.Grade.Should().Be(grade);
        sample.Size.Should().Be(size);
        sample.Status.Should().Be(QualityStatus.Testing);
    }

    [Fact]
    public void QualityTestSample_JsonSerialization_RoundTripSuccessful()
    {
        // Arrange
        var originalSample = _fixture.CreateValidQualityTestSample();
        originalSample.TestResults.Add(_fixture.CreateValidTensileTestResult());
        originalSample.CustomerRequirements.Add("SpecialReq", "HighStrength");

        // Act
        var json = JsonSerializer.Serialize(originalSample);
        var deserializedSample = JsonSerializer.Deserialize<QualityTestSample>(json);

        // Assert
        deserializedSample.Should().NotBeNull();
        deserializedSample!.SampleId.Should().Be(originalSample.SampleId);
        deserializedSample.BatchId.Should().Be(originalSample.BatchId);
        deserializedSample.Grade.Should().Be(originalSample.Grade);
        deserializedSample.Size.Should().Be(originalSample.Size);
        deserializedSample.TestResults.Should().HaveCount(1);
        deserializedSample.CustomerRequirements.Should().ContainKey("SpecialReq");
    }

    #endregion

    #region TensileTestResult Tests

    [Fact]
    public void TensileTestResult_InheritsFromQualityTestResult()
    {
        // Arrange & Act
        var tensileResult = new TensileTestResult();

        // Assert
        tensileResult.Should().BeAssignableTo<QualityTestResult>();
        tensileResult.TestType.Should().Be(QualityTestType.TensileTest);
    }

    [Fact]
    public void TensileTestResult_MechanicalProperties_CanBeSet()
    {
        // Arrange
        var tensileResult = new TensileTestResult();
        var yieldStrength = 450.0;
        var tensileStrength = 650.0;
        var elongation = 12.5;

        // Act
        tensileResult.YieldStrength = yieldStrength;
        tensileResult.TensileStrength = tensileStrength;
        tensileResult.ElongationAtBreak = elongation;
        tensileResult.YieldToTensileRatio = yieldStrength / tensileStrength;

        // Assert
        tensileResult.YieldStrength.Should().Be(yieldStrength);
        tensileResult.TensileStrength.Should().Be(tensileStrength);
        tensileResult.ElongationAtBreak.Should().Be(elongation);
        tensileResult.YieldToTensileRatio.Should().BeApproximately(0.692, 0.001);
    }

    [Fact]
    public void TensileTestResult_ValidGrade60Values_MeetsASTMRequirements()
    {
        // Arrange
        var tensileResult = _fixture.CreateValidTensileTestResult(grade: RebarGrade.Grade60);

        // Assert
        tensileResult.YieldStrength.Should().BeGreaterOrEqualTo(420); // Grade 60 minimum
        tensileResult.TensileStrength.Should().BeGreaterOrEqualTo(620);
        tensileResult.ElongationAtBreak.Should().BeGreaterOrEqualTo(9.0);
        tensileResult.YieldToTensileRatio.Should().BeLessOrEqualTo(0.85); // ASTM A706 limit
    }

    #endregion

    #region BendTestResult Tests

    [Fact]
    public void BendTestResult_PassingTest_SetsCorrectProperties()
    {
        // Arrange & Act
        var bendResult = _fixture.CreateValidBendTestResult();
        bendResult.CompletedWithoutCracking = true;
        bendResult.BendAngle = 180.0;

        // Assert
        bendResult.TestType.Should().Be(QualityTestType.BendTest);
        bendResult.BendAngle.Should().Be(180.0);
        bendResult.CompletedWithoutCracking.Should().BeTrue();
        bendResult.CrackLocations.Should().BeEmpty();
        bendResult.MaxCrackLength.Should().Be(0.0);
    }

    [Fact]
    public void BendTestResult_FailingTest_RecordsCrackInformation()
    {
        // Arrange
        var bendResult = new BendTestResult
        {
            TestType = QualityTestType.BendTest,
            BendAngle = 180.0,
            CompletedWithoutCracking = false,
            CrackLocations = new List<string> { "Surface crack at apex", "Minor crack on tension side" },
            MaxCrackLength = 2.5,
            CrackType = "Surface"
        };

        // Assert
        bendResult.CompletedWithoutCracking.Should().BeFalse();
        bendResult.CrackLocations.Should().HaveCount(2);
        bendResult.CrackLocations.Should().Contain("Surface crack at apex");
        bendResult.MaxCrackLength.Should().Be(2.5);
        bendResult.CrackType.Should().Be("Surface");
    }

    #endregion

    #region DimensionalTestResult Tests

    [Fact]
    public void DimensionalTestResult_ValidSize5Rebar_MeetsTolerances()
    {
        // Arrange
        var dimensionalResult = _fixture.CreateValidDimensionalTestResult(size: RebarSize.Size5);

        // Assert
        dimensionalResult.TestType.Should().Be(QualityTestType.DimensionalTest);
        dimensionalResult.NominalDiameter.Should().Be(16.0); // Size 5 = 16mm
        dimensionalResult.ActualDiameter.Should().BeInRange(15.5, 16.5); // ±0.5mm tolerance
        dimensionalResult.DiameterTolerance.Should().BeLessOrEqualTo(0.5);
        dimensionalResult.RibHeight.Should().BeGreaterOrEqualTo(16.0 * 0.043); // 4.3% minimum
        dimensionalResult.RelativeRibArea.Should().BeGreaterOrEqualTo(0.055); // 5.5% minimum
    }

    [Fact]
    public void DimensionalTestResult_SurfaceDefects_RecordsCorrectly()
    {
        // Arrange
        var dimensionalResult = new DimensionalTestResult
        {
            SurfaceDefectCount = 3,
            SurfaceDefectTypes = new List<string> { "Minor scale", "Light scratches", "Small inclusion" },
            SurfaceRoughness = 2.5
        };

        // Assert
        dimensionalResult.SurfaceDefectCount.Should().Be(3);
        dimensionalResult.SurfaceDefectTypes.Should().HaveCount(3);
        dimensionalResult.SurfaceDefectTypes.Should().Contain("Minor scale");
        dimensionalResult.SurfaceRoughness.Should().Be(2.5);
    }

    #endregion

    #region ChemicalAnalysisResult Tests

    [Fact]
    public void ChemicalAnalysisResult_A615StandardLimits_ValidatesCorrectly()
    {
        // Arrange
        var chemicalResult = _fixture.CreateValidChemicalAnalysisResult(standard: ASTMStandard.A615);

        // Assert
        chemicalResult.TestType.Should().Be(QualityTestType.ChemicalAnalysis);
        chemicalResult.CarbonContent.Should().BeLessOrEqualTo(0.30); // A615 limit
        chemicalResult.ManganeseContent.Should().BeLessOrEqualTo(1.50);
        chemicalResult.PhosphorusContent.Should().BeLessOrEqualTo(0.040);
        chemicalResult.SulfurContent.Should().BeLessOrEqualTo(0.050);
        chemicalResult.ChemicalCompositionCompliant.Should().BeTrue();
    }

    [Fact]
    public void ChemicalAnalysisResult_AnalysisMethodAndReadings_RecordsCorrectly()
    {
        // Arrange
        var chemicalResult = new ChemicalAnalysisResult
        {
            AnalysisMethod = "OES",
            NumberOfReadings = 5,
            StandardDeviation = 0.0015,
            SamplePreparation = "Disc grinding, argon atmosphere"
        };

        // Assert
        chemicalResult.AnalysisMethod.Should().Be("OES");
        chemicalResult.NumberOfReadings.Should().Be(5);
        chemicalResult.StandardDeviation.Should().Be(0.0015);
        chemicalResult.SamplePreparation.Should().Be("Disc grinding, argon atmosphere");
    }

    #endregion

    #region QualityControlStation Tests

    [Fact]
    public void QualityControlStation_DefaultConstructor_InitializesCollections()
    {
        // Act
        var station = new QualityControlStation();

        // Assert
        station.AvailableTests.Should().NotBeNull().And.BeEmpty();
        station.Equipment.Should().NotBeNull().And.BeEmpty();
        station.CertifiedTechnicians.Should().NotBeNull().And.BeEmpty();
        station.TypicalTestTimes.Should().NotBeNull().And.BeEmpty();
        station.QueuedSamples.Should().NotBeNull().And.BeEmpty();
        station.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void QualityControlStation_UtilizationCalculation_WorksCorrectly()
    {
        // Arrange
        var station = _fixture.CreateValidQualityControlStation();
        station.MaxCapacity = 20;
        station.CurrentWorkload = 15;

        // Act
        station.Utilization = (double)station.CurrentWorkload / station.MaxCapacity * 100;

        // Assert
        station.Utilization.Should().Be(75.0);
    }

    [Fact]
    public void QualityControlStation_AvailableTests_CanBeManaged()
    {
        // Arrange
        var station = new QualityControlStation();
        var tests = new List<QualityTestType>
        {
            QualityTestType.TensileTest,
            QualityTestType.BendTest,
            QualityTestType.DimensionalTest
        };

        // Act
        station.AvailableTests = tests;

        // Assert
        station.AvailableTests.Should().HaveCount(3);
        station.AvailableTests.Should().Contain(QualityTestType.TensileTest);
        station.AvailableTests.Should().Contain(QualityTestType.BendTest);
        station.AvailableTests.Should().Contain(QualityTestType.DimensionalTest);
    }

    #endregion

    #region QualityEquipment Tests

    [Fact]
    public void QualityEquipment_CalibrationStatus_CanBeTracked()
    {
        // Arrange
        var equipment = new QualityEquipment
        {
            EquipmentId = "TENSILE-01",
            LastCalibration = DateTime.UtcNow.AddDays(-25),
            NextCalibrationDue = DateTime.UtcNow.AddDays(5),
            CalibrationValid = true,
            CalibrationCertificate = "CAL-20241201-001"
        };

        // Assert
        equipment.LastCalibration.Should().BeBefore(DateTime.UtcNow);
        equipment.NextCalibrationDue.Should().BeAfter(DateTime.UtcNow);
        equipment.CalibrationValid.Should().BeTrue();
        equipment.CalibrationCertificate.Should().StartWith("CAL-");
    }

    [Fact]
    public void QualityEquipment_SupportedTests_MatchesEquipmentType()
    {
        // Arrange
        var tensileEquipment = new QualityEquipment
        {
            EquipmentType = "Tensile Machine",
            SupportedTests = new List<QualityTestType> { QualityTestType.TensileTest }
        };

        var bendEquipment = new QualityEquipment
        {
            EquipmentType = "Bend Tester",
            SupportedTests = new List<QualityTestType> { QualityTestType.BendTest }
        };

        // Assert
        tensileEquipment.SupportedTests.Should().Contain(QualityTestType.TensileTest);
        tensileEquipment.SupportedTests.Should().NotContain(QualityTestType.BendTest);
        
        bendEquipment.SupportedTests.Should().Contain(QualityTestType.BendTest);
        bendEquipment.SupportedTests.Should().NotContain(QualityTestType.TensileTest);
    }

    #endregion

    #region QualityAlert Tests

    [Fact]
    public void QualityAlert_DefaultConstructor_SetsDefaults()
    {
        // Act
        var alert = new QualityAlert();

        // Assert
        alert.Id.Should().NotBeEmpty();
        alert.Severity.Should().Be("Medium");
        alert.IsResolved.Should().BeFalse();
        alert.FailedCriteria.Should().NotBeNull().And.BeEmpty();
        alert.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void QualityAlert_GetRecommendedAction_ReturnsCorrectActions()
    {
        // Arrange & Act & Assert
        var testFailureAlert = new QualityAlert { AlertType = QualityAlert.AlertTypes.TestFailure };
        testFailureAlert.GetRecommendedAction().Should().Contain("Review test procedure");

        var astmAlert = new QualityAlert { AlertType = QualityAlert.AlertTypes.ASTMNonCompliance };
        astmAlert.GetRecommendedAction().Should().Contain("Stop production");

        var calibrationAlert = new QualityAlert { AlertType = QualityAlert.AlertTypes.CalibrationExpired };
        calibrationAlert.GetRecommendedAction().Should().Contain("schedule immediate calibration");

        var unknownAlert = new QualityAlert { AlertType = "UNKNOWN_TYPE" };
        unknownAlert.GetRecommendedAction().Should().Contain("Contact quality control supervisor");
    }

    [Fact]
    public void QualityAlert_AlertTypes_Constants_AreCorrect()
    {
        // Assert
        QualityAlert.AlertTypes.TestFailure.Should().Be("TEST_FAILURE");
        QualityAlert.AlertTypes.ASTMNonCompliance.Should().Be("ASTM_NON_COMPLIANCE");
        QualityAlert.AlertTypes.CustomerSpecFailure.Should().Be("CUSTOMER_SPEC_FAILURE");
        QualityAlert.AlertTypes.EquipmentMalfunction.Should().Be("EQUIPMENT_MALFUNCTION");
        QualityAlert.AlertTypes.CalibrationExpired.Should().Be("CALIBRATION_EXPIRED");
    }

    #endregion

    #region ASTMSpecification Tests

    [Fact]
    public void ASTMSpecification_GetSpecification_Grade60A615_ReturnsCorrectValues()
    {
        // Act
        var spec = ASTMSpecification.GetSpecification(ASTMStandard.A615, RebarGrade.Grade60, RebarSize.Size5);

        // Assert
        spec.Standard.Should().Be(ASTMStandard.A615);
        spec.Grade.Should().Be(RebarGrade.Grade60);
        spec.Size.Should().Be(RebarSize.Size5);
        spec.MinYieldStrength.Should().Be(420); // 60 ksi = 420 MPa
        spec.MinTensileStrength.Should().Be(620);
        spec.MinElongation.Should().Be(9.0);
        spec.MaxCarbon.Should().Be(0.30);
        spec.MaxPhosphorus.Should().Be(0.040);
        spec.MaxSulfur.Should().Be(0.050);
        spec.NominalDiameter.Should().Be(16.0); // Size 5 = 16mm
        spec.DiameterTolerance.Should().Be(0.5);
    }

    [Fact]
    public void ASTMSpecification_GetSpecification_Grade75A706_HasStricterLimits()
    {
        // Act
        var spec = ASTMSpecification.GetSpecification(ASTMStandard.A706, RebarGrade.Grade75, RebarSize.Size8);

        // Assert
        spec.Standard.Should().Be(ASTMStandard.A706);
        spec.Grade.Should().Be(RebarGrade.Grade75);
        spec.MinYieldStrength.Should().Be(520); // 75 ksi = 520 MPa
        spec.MinTensileStrength.Should().Be(690);
        spec.MinElongation.Should().Be(7.0); // Lower elongation for higher grade
        spec.MaxCarbon.Should().Be(0.25); // Stricter than A615 (0.30)
        spec.MaxManganese.Should().Be(1.35); // Stricter than A615 (1.50)
        spec.MaxYieldToTensileRatio.Should().Be(0.85); // A706 specific requirement
    }

    [Theory]
    [InlineData(RebarGrade.Grade40, 275, 420, 12.0)]
    [InlineData(RebarGrade.Grade60, 420, 620, 9.0)]
    [InlineData(RebarGrade.Grade75, 520, 690, 7.0)]
    [InlineData(RebarGrade.Grade80, 550, 720, 6.0)]
    public void ASTMSpecification_GetSpecification_DifferentGrades_CorrectMechanicalProperties(
        RebarGrade grade, double expectedYield, double expectedTensile, double expectedElongation)
    {
        // Act
        var spec = ASTMSpecification.GetSpecification(ASTMStandard.A615, grade, RebarSize.Size5);

        // Assert
        spec.MinYieldStrength.Should().Be(expectedYield);
        spec.MinTensileStrength.Should().Be(expectedTensile);
        spec.MinElongation.Should().Be(expectedElongation);
    }

    [Theory]
    [InlineData(RebarSize.Size3, 10.0)]
    [InlineData(RebarSize.Size4, 13.0)]
    [InlineData(RebarSize.Size5, 16.0)]
    [InlineData(RebarSize.Size8, 25.0)]
    [InlineData(RebarSize.Size10, 32.0)]
    public void ASTMSpecification_GetSpecification_DifferentSizes_CorrectDimensions(
        RebarSize size, double expectedDiameter)
    {
        // Act
        var spec = ASTMSpecification.GetSpecification(ASTMStandard.A615, RebarGrade.Grade60, size);

        // Assert
        spec.NominalDiameter.Should().Be(expectedDiameter);
        spec.MinRibHeight.Should().Be(expectedDiameter * 0.043); // 4.3% of diameter
        spec.MaxRibSpacing.Should().Be(expectedDiameter * 0.7); // 70% of diameter
    }

    [Fact]
    public void ASTMSpecification_BendTestRequirements_DependOnSize()
    {
        // Arrange & Act
        var smallSpec = ASTMSpecification.GetSpecification(ASTMStandard.A615, RebarGrade.Grade60, RebarSize.Size8);
        var largeSpec = ASTMSpecification.GetSpecification(ASTMStandard.A615, RebarGrade.Grade60, RebarSize.Size14);

        // Assert
        smallSpec.BendPinDiameter.Should().Be(3.0); // 3d for ≤#10
        largeSpec.BendPinDiameter.Should().Be(4.0); // 4d for >#10
        smallSpec.BendTestAngle.Should().Be(180.0);
        largeSpec.BendTestAngle.Should().Be(180.0);
    }

    #endregion

    #region Enum Tests

    [Fact]
    public void RebarGrade_EnumValues_MatchStrengthValues()
    {
        // Assert
        ((int)RebarGrade.Grade40).Should().Be(40);
        ((int)RebarGrade.Grade60).Should().Be(60);
        ((int)RebarGrade.Grade75).Should().Be(75);
        ((int)RebarGrade.Grade80).Should().Be(80);
    }

    [Fact]
    public void RebarSize_EnumValues_MatchDiameters()
    {
        // Assert
        ((int)RebarSize.Size3).Should().Be(10); // #3 = 10mm
        ((int)RebarSize.Size4).Should().Be(13); // #4 = 13mm
        ((int)RebarSize.Size5).Should().Be(16); // #5 = 16mm
        ((int)RebarSize.Size8).Should().Be(25); // #8 = 25mm
    }

    [Fact]
    public void QualityTestType_AllTypesPresent_ForComprehensiveTesting()
    {
        // Arrange
        var allTestTypes = Enum.GetValues<QualityTestType>().ToList();

        // Assert
        allTestTypes.Should().Contain(QualityTestType.TensileTest);
        allTestTypes.Should().Contain(QualityTestType.BendTest);
        allTestTypes.Should().Contain(QualityTestType.DimensionalTest);
        allTestTypes.Should().Contain(QualityTestType.ChemicalAnalysis);
        allTestTypes.Should().Contain(QualityTestType.HardnessTest);
        allTestTypes.Should().Contain(QualityTestType.SurfaceInspection);
        allTestTypes.Should().Contain(QualityTestType.UltrasonicTest);
        allTestTypes.Should().HaveCountGreaterOrEqualTo(12); // Ensure comprehensive coverage
    }

    #endregion

    #region Integration and Performance Tests

    [Fact]
    public void QualityTestSample_CompleteWorkflow_HandlesAllTestTypes()
    {
        // Arrange
        var sample = _fixture.CreateValidQualityTestSample();
        
        // Act - Add all test types
        sample.TestResults.Add(_fixture.CreateValidTensileTestResult());
        sample.TestResults.Add(_fixture.CreateValidBendTestResult());
        sample.TestResults.Add(_fixture.CreateValidDimensionalTestResult());
        sample.TestResults.Add(_fixture.CreateValidChemicalAnalysisResult());
        
        sample.PassedAllTests = sample.TestResults.All(r => r.Result == TestResult.Pass);
        sample.OverallQualityScore = sample.TestResults.Count(r => r.Result == TestResult.Pass) * 100.0 / sample.TestResults.Count;

        // Assert
        sample.TestResults.Should().HaveCount(4);
        sample.TestResults.Should().AllSatisfy(r => r.Result.Should().Be(TestResult.Pass));
        sample.PassedAllTests.Should().BeTrue();
        sample.OverallQualityScore.Should().Be(100.0);
    }

    [Fact]
    public void QualityModels_LargeDataSets_PerformCorrectly()
    {
        // Arrange
        var samples = new List<QualityTestSample>();
        
        // Act - Create large dataset
        for (int i = 1; i <= 1000; i++)
        {
            var sample = _fixture.CreateValidQualityTestSample($"QTS-{i:D4}");
            sample.TestResults.Add(_fixture.CreateValidTensileTestResult());
            samples.Add(sample);
        }

        // Assert
        samples.Should().HaveCount(1000);
        samples.Should().AllSatisfy(s => 
        {
            s.SampleId.Should().StartWith("QTS-");
            s.TestResults.Should().HaveCount(1);
        });
        
        // Performance check - should complete quickly
        var distinctBatches = samples.Select(s => s.BatchId).Distinct().Count();
        distinctBatches.Should().BeGreaterThan(0);
    }

    [Fact]
    public void QualityTestSample_SerializationPerformance_WithManyResults()
    {
        // Arrange
        var sample = _fixture.CreateValidQualityTestSample();
        for (int i = 0; i < 100; i++)
        {
            sample.TestResults.Add(_fixture.CreateValidTensileTestResult());
        }

        // Act & Assert - Should not throw and should be reasonably fast
        var json = JsonSerializer.Serialize(sample);
        json.Should().NotBeNullOrEmpty();
        json.Length.Should().BeGreaterThan(1000);

        var deserialized = JsonSerializer.Deserialize<QualityTestSample>(json);
        deserialized.Should().NotBeNull();
        deserialized!.TestResults.Should().HaveCount(100);
    }

    #endregion
}