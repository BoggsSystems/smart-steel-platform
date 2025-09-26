using AutoFixture;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.Azure.Cosmos;
using QualityControlService.Models;
using QualityControlService.Services;
using Microsoft.Extensions.Configuration;

namespace QualityControlService.Tests.Fixtures;

public class QualityTestFixture
{
    public Fixture Fixture { get; }
    public Mock<CosmosClient> CosmosClientMock { get; }
    public Mock<Container> SamplesContainerMock { get; }
    public Mock<Container> TestResultsContainerMock { get; }
    public Mock<Container> StationsContainerMock { get; }
    public Mock<Container> MetricsContainerMock { get; }
    public Mock<Container> AlertsContainerMock { get; }
    public Mock<Container> CertificatesContainerMock { get; }
    public Mock<ILogger<QualityControlDataService>> LoggerMock { get; }
    public Mock<IConfiguration> ConfigurationMock { get; }
    public Mock<IQualityControlDataService> DataServiceMock { get; }
    
    public QualityTestFixture()
    {
        Fixture = new Fixture();
        
        CosmosClientMock = new Mock<CosmosClient>();
        SamplesContainerMock = new Mock<Container>();
        TestResultsContainerMock = new Mock<Container>();
        StationsContainerMock = new Mock<Container>();
        MetricsContainerMock = new Mock<Container>();
        AlertsContainerMock = new Mock<Container>();
        CertificatesContainerMock = new Mock<Container>();
        LoggerMock = new Mock<ILogger<QualityControlDataService>>();
        ConfigurationMock = new Mock<IConfiguration>();
        DataServiceMock = new Mock<IQualityControlDataService>();
        
        SetupConfiguration();
        SetupCosmosContainerMocks();
        
        Fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => Fixture.Behaviors.Remove(b));
        Fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }
    
    private void SetupConfiguration()
    {
        ConfigurationMock.Setup(c => c["CosmosDb:DatabaseName"]).Returns("SteelMillRebarDB");
    }
    
    private void SetupCosmosContainerMocks()
    {
        CosmosClientMock.Setup(c => c.GetContainer("SteelMillRebarDB", "QualityTestSamples"))
            .Returns(SamplesContainerMock.Object);
        CosmosClientMock.Setup(c => c.GetContainer("SteelMillRebarDB", "QualityTestResults"))
            .Returns(TestResultsContainerMock.Object);
        CosmosClientMock.Setup(c => c.GetContainer("SteelMillRebarDB", "QualityControlStations"))
            .Returns(StationsContainerMock.Object);
        CosmosClientMock.Setup(c => c.GetContainer("SteelMillRebarDB", "QualityPerformanceMetrics"))
            .Returns(MetricsContainerMock.Object);
        CosmosClientMock.Setup(c => c.GetContainer("SteelMillRebarDB", "QualityAlerts"))
            .Returns(AlertsContainerMock.Object);
        CosmosClientMock.Setup(c => c.GetContainer("SteelMillRebarDB", "QualityCertificates"))
            .Returns(CertificatesContainerMock.Object);
    }
    
    public QualityTestSample CreateValidQualityTestSample(string? sampleId = null, 
        RebarGrade grade = RebarGrade.Grade60, RebarSize size = RebarSize.Size5)
    {
        return new QualityTestSample
        {
            SampleId = sampleId ?? $"QTS-{Fixture.Create<int>() % 10000:D4}",
            BatchId = $"BATCH-{Fixture.Create<int>() % 100:D3}-{DateTime.UtcNow:yyyyMMdd}",
            HeatNumber = $"H{DateTime.UtcNow:yyyyMMdd}{Fixture.Create<int>() % 1000:D3}",
            Grade = grade,
            Size = size,
            Standard = ASTMStandard.A615,
            SampleSource = $"Line-{Fixture.Create<int>() % 5 + 1}",
            SampledBy = $"Tech-{Fixture.Create<int>() % 20 + 1:D2}",
            SampleLength = Math.Round(Fixture.Create<double>() % 5 + 0.5, 2), // 0.5-5.5m
            SampleWeight = Math.Round(Fixture.Create<double>() % 50 + 5, 2),  // 5-55kg
            CustomerOrderId = $"PO-{DateTime.UtcNow:yyyy}-{Fixture.Create<int>() % 10000:D4}",
            Status = QualityStatus.InQueue,
            CreatedAt = DateTime.UtcNow.AddMinutes(-Fixture.Create<int>() % 1440)
        };
    }
    
    public TensileTestResult CreateValidTensileTestResult(string? testId = null, 
        RebarGrade grade = RebarGrade.Grade60)
    {
        var (minYield, minTensile) = GetMechanicalPropertiesForGrade(grade);
        var yieldStrength = minYield + (Fixture.Create<double>() % 100);
        var tensileStrength = Math.Max(minTensile + (Fixture.Create<double>() % 150), yieldStrength * 1.25);
        
        var result = new TensileTestResult
        {
            TestId = testId ?? Fixture.Create<Guid>().ToString(),
            TestType = QualityTestType.TensileTest,
            TestName = "Tensile Strength Test",
            TestDate = DateTime.UtcNow.AddMinutes(-Fixture.Create<int>() % 120),
            TestEquipmentId = $"TENSILE-{Fixture.Create<int>() % 3 + 1:D2}",
            TechnicianId = $"TECH-{Fixture.Create<int>() % 15 + 1:D2}",
            TestDuration = TimeSpan.FromMinutes(Fixture.Create<int>() % 30 + 15),
            YieldStrength = Math.Round(yieldStrength, 1),
            TensileStrength = Math.Round(tensileStrength, 1),
            ElongationAtBreak = Math.Round(9 + (Fixture.Create<double>() % 6), 1), // 9-15%
            ElongationUniformly = Math.Round(6 + (Fixture.Create<double>() % 4), 1), // 6-10%
            ReductionInArea = Math.Round(50 + (Fixture.Create<double>() % 25), 1), // 50-75%
            ModulusOfElasticity = Math.Round(200 + (Fixture.Create<double>() % 20), 1), // 200-220 GPa
            CrossSectionalArea = GetCrossSectionalArea((int)RebarSize.Size5),
            GaugeLength = 200.0, // Standard 200mm gauge length
            FractureType = "Cup-cone"
        };
        
        result.YieldToTensileRatio = Math.Round(result.YieldStrength / result.TensileStrength, 3);
        result.Result = TestResult.Pass;
        result.PassedAllCriteria = true;
        
        // Set measured values
        result.MeasuredValues["YieldStrength"] = result.YieldStrength;
        result.MeasuredValues["TensileStrength"] = result.TensileStrength;
        result.MeasuredValues["Elongation"] = result.ElongationAtBreak;
        
        // Set requirements
        result.RequiredMinValues["YieldStrength"] = minYield;
        result.RequiredMinValues["TensileStrength"] = minTensile;
        result.RequiredMinValues["Elongation"] = GetMinElongationForGrade(grade);
        
        return result;
    }
    
    public BendTestResult CreateValidBendTestResult(string? testId = null)
    {
        var passTest = Fixture.Create<bool>();
        
        return new BendTestResult
        {
            TestId = testId ?? Fixture.Create<Guid>().ToString(),
            TestType = QualityTestType.BendTest,
            TestName = "180° Bend Test",
            TestDate = DateTime.UtcNow.AddMinutes(-Fixture.Create<int>() % 60),
            TestEquipmentId = $"BEND-{Fixture.Create<int>() % 2 + 1:D2}",
            TechnicianId = $"TECH-{Fixture.Create<int>() % 15 + 1:D2}",
            TestDuration = TimeSpan.FromMinutes(Fixture.Create<int>() % 10 + 5),
            BendAngle = 180.0,
            BendRadius = Math.Round(3.0 * (int)RebarSize.Size5, 1), // 3 x diameter
            PinDiameter = Math.Round(3.0 * (int)RebarSize.Size5, 1),
            CompletedWithoutCracking = passTest,
            CrackLocations = passTest ? new List<string>() : new List<string> { "Surface crack at bend apex" },
            MaxCrackLength = passTest ? 0.0 : Math.Round(Fixture.Create<double>() % 2 + 0.5, 1),
            CrackType = passTest ? string.Empty : "Surface",
            ASTMBendCompliant = passTest,
            Result = passTest ? TestResult.Pass : TestResult.Fail,
            PassedAllCriteria = passTest
        };
    }
    
    public DimensionalTestResult CreateValidDimensionalTestResult(string? testId = null, 
        RebarSize size = RebarSize.Size5)
    {
        var nominalDiameter = (double)size;
        var actualDiameter = nominalDiameter + (Fixture.Create<double>() - 0.5) * 0.6; // ±0.3mm variation
        var ribHeight = nominalDiameter * (0.043 + Fixture.Create<double>() * 0.02); // 4.3-6.3% of diameter
        
        return new DimensionalTestResult
        {
            TestId = testId ?? Fixture.Create<Guid>().ToString(),
            TestType = QualityTestType.DimensionalTest,
            TestName = "Dimensional and Ribbing Test",
            TestDate = DateTime.UtcNow.AddMinutes(-Fixture.Create<int>() % 60),
            TestEquipmentId = $"CMM-{Fixture.Create<int>() % 2 + 1:D2}",
            TechnicianId = $"TECH-{Fixture.Create<int>() % 15 + 1:D2}",
            TestDuration = TimeSpan.FromMinutes(Fixture.Create<int>() % 15 + 10),
            NominalDiameter = nominalDiameter,
            ActualDiameter = Math.Round(actualDiameter, 2),
            DiameterTolerance = Math.Round(Math.Abs(actualDiameter - nominalDiameter), 2),
            CrossSectionalArea = Math.Round(Math.PI * Math.Pow(actualDiameter / 2, 2), 1),
            Ovality = Math.Round(Fixture.Create<double>() * 0.2, 2), // ≤0.2mm ovality
            RibHeight = Math.Round(ribHeight, 2),
            RibSpacing = Math.Round(nominalDiameter * 0.6 + Fixture.Create<double>() * nominalDiameter * 0.1, 2),
            RibAngle = Math.Round(45 + (Fixture.Create<double>() - 0.5) * 20, 1), // 35-65°
            RelativeRibArea = Math.Round(0.055 + Fixture.Create<double>() * 0.02, 3), // 5.5-7.5%
            RibbingCompliant = true,
            ActualLength = Math.Round(12.0 + (Fixture.Create<double>() - 0.5) * 0.1, 3), // 12m ±50mm
            Straightness = Math.Round(Fixture.Create<double>() * 2.0, 1), // ≤2mm/m deviation
            WeightPerMeter = GetNominalWeightPerMeter(size) * (1 + (Fixture.Create<double>() - 0.5) * 0.06), // ±3%
            SurfaceRoughness = Math.Round(1.0 + Fixture.Create<double>() * 3.0, 1), // 1-4 μm Ra
            SurfaceDefectCount = Fixture.Create<int>() % 3,
            SurfaceDefectTypes = new List<string> { "Minor scale", "Light scratches" },
            Result = TestResult.Pass,
            PassedAllCriteria = true
        };
    }
    
    public ChemicalAnalysisResult CreateValidChemicalAnalysisResult(string? testId = null, 
        ASTMStandard standard = ASTMStandard.A615)
    {
        var (maxC, maxMn, maxP, maxS) = GetChemicalLimitsForStandard(standard);
        
        return new ChemicalAnalysisResult
        {
            TestId = testId ?? Fixture.Create<Guid>().ToString(),
            TestType = QualityTestType.ChemicalAnalysis,
            TestName = "Chemical Composition Analysis",
            TestDate = DateTime.UtcNow.AddMinutes(-Fixture.Create<int>() % 30),
            TestEquipmentId = $"OES-{Fixture.Create<int>() % 2 + 1:D2}",
            TechnicianId = $"TECH-{Fixture.Create<int>() % 15 + 1:D2}",
            TestDuration = TimeSpan.FromMinutes(Fixture.Create<int>() % 10 + 5),
            CarbonContent = Math.Round(maxC * 0.7 + Fixture.Create<double>() * maxC * 0.2, 4), // 70-90% of limit
            ManganeseContent = Math.Round(0.4 + Fixture.Create<double>() * 0.8, 3), // 0.4-1.2%
            PhosphorusContent = Math.Round(Fixture.Create<double>() * maxP * 0.8, 4), // Up to 80% of limit
            SulfurContent = Math.Round(Fixture.Create<double>() * maxS * 0.8, 4),
            SiliconContent = Math.Round(0.15 + Fixture.Create<double>() * 0.25, 3), // 0.15-0.40%
            NitrogenContent = Math.Round(0.005 + Fixture.Create<double>() * 0.015, 4), // 0.005-0.020%
            ChromiumContent = Math.Round(Fixture.Create<double>() * 0.15, 3),
            NickelContent = Math.Round(Fixture.Create<double>() * 0.10, 3),
            CopperContent = Math.Round(Fixture.Create<double>() * 0.20, 3),
            VanadiumContent = Math.Round(Fixture.Create<double>() * 0.05, 3),
            AnalysisMethod = "OES",
            SamplePreparation = "Disc grinding, argon atmosphere",
            NumberOfReadings = 3,
            StandardDeviation = Math.Round(Fixture.Create<double>() * 0.002 + 0.001, 4),
            CarbonCompliant = true,
            ManganeseCompliant = true,
            PhosphorusCompliant = true,
            SulfurCompliant = true,
            ChemicalCompositionCompliant = true,
            Result = TestResult.Pass,
            PassedAllCriteria = true
        };
    }
    
    public QualityControlStation CreateValidQualityControlStation(string? stationId = null)
    {
        return new QualityControlStation
        {
            StationId = stationId ?? $"QC-STN-{Fixture.Create<int>() % 10 + 1:D2}",
            StationName = $"Quality Control Station {Fixture.Create<int>() % 10 + 1}",
            Location = $"Building {Fixture.Create<char>().ToString().ToUpper()}, Bay {Fixture.Create<int>() % 20 + 1}",
            StationType = Fixture.Create<string[]>(new[] { "Incoming", "In-Process", "Final" })[Fixture.Create<int>() % 3],
            OperationalStatus = "Active",
            AvailableTests = new List<QualityTestType> 
            {
                QualityTestType.TensileTest,
                QualityTestType.BendTest,
                QualityTestType.DimensionalTest,
                QualityTestType.ChemicalAnalysis
            },
            CurrentWorkload = Fixture.Create<int>() % 15,
            MaxCapacity = 20,
            Utilization = Math.Round((Fixture.Create<int>() % 15) * 5.0, 1), // 0-70% utilization
            Equipment = new List<QualityEquipment>
            {
                CreateValidQualityEquipment("TENSILE-01"),
                CreateValidQualityEquipment("BEND-01"),
                CreateValidQualityEquipment("CMM-01")
            },
            CertifiedTechnicians = new List<string> 
            { 
                "TECH-01", "TECH-02", "TECH-03", "TECH-04", "TECH-05" 
            },
            ThroughputRate = Math.Round(5.0 + Fixture.Create<double>() * 10.0, 1), // 5-15 samples/hr
            QualityAccuracy = Math.Round(95.0 + Fixture.Create<double>() * 4.0, 1), // 95-99% accuracy
            EquipmentUptime = Math.Round(92.0 + Fixture.Create<double>() * 7.0, 1), // 92-99% uptime
            AverageTestTime = Math.Round(15.0 + Fixture.Create<double>() * 30.0, 1), // 15-45 minutes
            QueuedSamples = new List<string>(),
            EstimatedQueueTime = TimeSpan.FromMinutes(Fixture.Create<int>() % 120),
            LastUpdated = DateTime.UtcNow.AddMinutes(-Fixture.Create<int>() % 30)
        };
    }
    
    public QualityEquipment CreateValidQualityEquipment(string? equipmentId = null)
    {
        var equipmentTypes = new[] { "Tensile Machine", "Bend Tester", "CMM", "OES Spectrometer", "Hardness Tester" };
        var equipmentType = equipmentTypes[Fixture.Create<int>() % equipmentTypes.Length];
        
        return new QualityEquipment
        {
            EquipmentId = equipmentId ?? $"EQ-{Fixture.Create<int>() % 100 + 1:D3}",
            EquipmentName = $"{equipmentType} #{Fixture.Create<int>() % 5 + 1}",
            EquipmentType = equipmentType,
            Manufacturer = GetManufacturerForEquipmentType(equipmentType),
            Model = $"Model-{Fixture.Create<int>() % 1000 + 100}",
            IsOperational = true,
            MaintenanceStatus = "Good",
            LastMaintenance = DateTime.UtcNow.AddDays(-Fixture.Create<int>() % 90),
            NextMaintenanceDue = DateTime.UtcNow.AddDays(Fixture.Create<int>() % 90 + 30),
            LastCalibration = DateTime.UtcNow.AddDays(-Fixture.Create<int>() % 30),
            NextCalibrationDue = DateTime.UtcNow.AddDays(Fixture.Create<int>() % 30 + 30),
            CalibrationCertificate = $"CAL-{DateTime.UtcNow:yyyyMMdd}-{Fixture.Create<int>() % 1000:D3}",
            CalibrationValid = true,
            SupportedTests = GetSupportedTestsForEquipmentType(equipmentType),
            TestsPerformed = Fixture.Create<int>() % 10000,
            UtilizationRate = Math.Round(Fixture.Create<double>() * 80 + 10, 1), // 10-90%
            TotalOperatingTime = TimeSpan.FromHours(Fixture.Create<int>() % 8760), // Up to 1 year
            RecentIssues = new List<string>()
        };
    }
    
    public QualityAlert CreateQualityAlert(string? stationId = null, string alertType = "TEST_FAILURE")
    {
        return new QualityAlert
        {
            Id = Fixture.Create<Guid>().ToString(),
            StationId = stationId ?? $"QC-STN-{Fixture.Create<int>() % 10 + 1:D2}",
            SampleId = $"QTS-{Fixture.Create<int>() % 10000:D4}",
            BatchId = $"BATCH-{Fixture.Create<int>() % 100:D3}-{DateTime.UtcNow:yyyyMMdd}",
            AffectedGrade = RebarGrade.Grade60,
            AffectedSize = RebarSize.Size5,
            AlertType = alertType,
            Severity = GetSeverityForAlertType(alertType),
            Description = GetDescriptionForAlertType(alertType),
            TestType = QualityTestType.TensileTest,
            TestEquipmentId = $"TENSILE-{Fixture.Create<int>() % 3 + 1:D2}",
            TestValue = 400.0,
            SpecificationLimit = 420.0,
            Deviation = -20.0,
            ASTMNonCompliance = alertType == "ASTM_NON_COMPLIANCE",
            CustomerSpecNonCompliance = false,
            FailedCriteria = alertType == "TEST_FAILURE" ? new List<string> { "Yield strength below minimum" } : new List<string>(),
            EquipmentOperational = true,
            CalibrationValid = true,
            LastCalibrationCheck = DateTime.UtcNow.AddDays(-7),
            IsResolved = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-Fixture.Create<int>() % 1440)
        };
    }
    
    public ASTMSpecification CreateASTMSpecification(ASTMStandard standard = ASTMStandard.A615,
        RebarGrade grade = RebarGrade.Grade60, RebarSize size = RebarSize.Size5)
    {
        return ASTMSpecification.GetSpecification(standard, grade, size);
    }
    
    // Helper methods
    private (double minYield, double minTensile) GetMechanicalPropertiesForGrade(RebarGrade grade) =>
        grade switch
        {
            RebarGrade.Grade40 => (275, 420),
            RebarGrade.Grade60 => (420, 620),
            RebarGrade.Grade75 => (520, 690),
            RebarGrade.Grade80 => (550, 720),
            _ => (420, 620)
        };
    
    private double GetMinElongationForGrade(RebarGrade grade) =>
        grade switch
        {
            RebarGrade.Grade40 => 12.0,
            RebarGrade.Grade60 => 9.0,
            RebarGrade.Grade75 => 7.0,
            RebarGrade.Grade80 => 6.0,
            _ => 9.0
        };
    
    private (double maxC, double maxMn, double maxP, double maxS) GetChemicalLimitsForStandard(ASTMStandard standard) =>
        standard switch
        {
            ASTMStandard.A615 => (0.30, 1.50, 0.040, 0.050),
            ASTMStandard.A706 => (0.25, 1.35, 0.035, 0.045),
            _ => (0.30, 1.50, 0.040, 0.050)
        };
    
    private double GetCrossSectionalArea(int diameter)
    {
        return Math.Round(Math.PI * Math.Pow(diameter / 2.0, 2), 1);
    }
    
    private double GetNominalWeightPerMeter(RebarSize size)
    {
        var diameter = (double)size / 1000; // Convert mm to m
        return Math.Round(Math.PI * Math.Pow(diameter / 2, 2) * 7850, 2); // Steel density 7850 kg/m³
    }
    
    private string GetManufacturerForEquipmentType(string equipmentType) =>
        equipmentType switch
        {
            "Tensile Machine" => "MTS Systems",
            "Bend Tester" => "Controls Group",
            "CMM" => "Zeiss",
            "OES Spectrometer" => "SPECTRO Analytical",
            "Hardness Tester" => "Buehler",
            _ => "Generic Manufacturer"
        };
    
    private List<QualityTestType> GetSupportedTestsForEquipmentType(string equipmentType) =>
        equipmentType switch
        {
            "Tensile Machine" => new List<QualityTestType> { QualityTestType.TensileTest },
            "Bend Tester" => new List<QualityTestType> { QualityTestType.BendTest },
            "CMM" => new List<QualityTestType> { QualityTestType.DimensionalTest },
            "OES Spectrometer" => new List<QualityTestType> { QualityTestType.ChemicalAnalysis },
            "Hardness Tester" => new List<QualityTestType> { QualityTestType.HardnessTest },
            _ => new List<QualityTestType>()
        };
    
    private string GetSeverityForAlertType(string alertType) =>
        alertType switch
        {
            "ASTM_NON_COMPLIANCE" => "Critical",
            "TEST_FAILURE" => "High",
            "EQUIPMENT_MALFUNCTION" => "High",
            "CALIBRATION_EXPIRED" => "Medium",
            _ => "Medium"
        };
    
    private string GetDescriptionForAlertType(string alertType) =>
        alertType switch
        {
            "TEST_FAILURE" => "Tensile test failed - yield strength below specification",
            "ASTM_NON_COMPLIANCE" => "Sample does not meet ASTM A615 requirements",
            "EQUIPMENT_MALFUNCTION" => "Tensile testing machine showing calibration drift",
            "CALIBRATION_EXPIRED" => "Equipment calibration has expired",
            _ => "Quality control issue detected"
        };
}