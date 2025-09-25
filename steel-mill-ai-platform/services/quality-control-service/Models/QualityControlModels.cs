using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace QualityControlService.Models;

// Rebar-specific enumerations
public enum RebarGrade
{
    Grade40 = 40,    // 40 ksi yield strength (275 MPa)
    Grade60 = 60,    // 60 ksi yield strength (420 MPa) 
    Grade75 = 75,    // 75 ksi yield strength (520 MPa)
    Grade80 = 80     // 80 ksi yield strength (550 MPa)
}

public enum RebarSize
{
    Size3 = 10,      // #3 rebar (10mm diameter)
    Size4 = 13,      // #4 rebar (13mm diameter)
    Size5 = 16,      // #5 rebar (16mm diameter)
    Size6 = 19,      // #6 rebar (19mm diameter)
    Size7 = 22,      // #7 rebar (22mm diameter)
    Size8 = 25,      // #8 rebar (25mm diameter)
    Size9 = 29,      // #9 rebar (29mm diameter)
    Size10 = 32,     // #10 rebar (32mm diameter)
    Size11 = 36,     // #11 rebar (36mm diameter)
    Size14 = 43,     // #14 rebar (43mm diameter)
    Size18 = 57      // #18 rebar (57mm diameter)
}

public enum QualityTestType
{
    TensileTest,           // Yield and tensile strength
    BendTest,              // 180° bend test
    DimensionalTest,       // Diameter and rib measurements
    ChemicalAnalysis,      // Carbon, Manganese, P, S content
    HardnessTest,          // Brinell or Rockwell hardness
    SurfaceInspection,     // Visual and defect analysis
    StraightnessTest,      // Deviation measurement
    WeightVerification,    // Mass per unit length
    RibbingTest,           // Rib height, spacing, angle
    FatigueTest,           // Cyclic loading (special cases)
    ImpactTest,            // Charpy impact (low temp applications)
    UltrasonicTest         // Non-destructive internal defect detection
}

public enum TestResult
{
    Pass,                  // Meets all requirements
    Fail,                  // Does not meet requirements
    Conditional,           // Marginal - requires review
    Pending,               // Test in progress
    NotTested,             // Test not performed
    Waived                 // Test waived by authorization
}

public enum ASTMStandard
{
    A615,                  // Standard carbon steel rebar
    A706,                  // Low-alloy steel rebar (seismic)
    A996,                  // Rail-steel and axle-steel rebar
    A1035                  // High-strength low-alloy steel rebar
}

public enum QualityStatus
{
    InQueue,               // Waiting for testing
    Testing,               // Tests in progress
    Completed,             // All tests complete
    Approved,              // Quality approved
    ConditionalApproval,   // Approved with conditions
    Rejected,              // Failed quality requirements
    OnHold,                // Awaiting review/decision
    Rework                 // Requires rework/reprocessing
}

public class QualityTestSample
{
    public string SampleId { get; set; } = Guid.NewGuid().ToString();
    public string BatchId { get; set; } = string.Empty;
    public string HeatNumber { get; set; } = string.Empty;
    public RebarGrade Grade { get; set; }
    public RebarSize Size { get; set; }
    public ASTMStandard Standard { get; set; } = ASTMStandard.A615;
    
    // Sample information
    public string SampleSource { get; set; } = string.Empty; // Production line location
    public DateTime SampleTaken { get; set; } = DateTime.UtcNow;
    public string SampledBy { get; set; } = string.Empty;
    public double SampleLength { get; set; }                 // m
    public double SampleWeight { get; set; }                 // kg
    
    // Test status
    public QualityStatus Status { get; set; } = QualityStatus.InQueue;
    public DateTime? TestingStarted { get; set; }
    public DateTime? TestingCompleted { get; set; }
    public string? AssignedTechnician { get; set; }
    public string? QualityInspector { get; set; }
    
    // Test results
    public List<QualityTestResult> TestResults { get; set; } = new();
    public double OverallQualityScore { get; set; }
    public bool PassedAllTests { get; set; }
    public List<string> FailureReasons { get; set; } = new();
    public List<string> QualityNotes { get; set; } = new();
    
    // ASTM compliance
    public bool ASTMCompliant { get; set; }
    public string ComplianceNotes { get; set; } = string.Empty;
    public DateTime? CertificationDate { get; set; }
    public string? CertificationNumber { get; set; }
    
    // Customer specifications
    public string CustomerOrderId { get; set; } = string.Empty;
    public bool CustomerSpecCompliant { get; set; } = true;
    public Dictionary<string, string> CustomerRequirements { get; set; } = new();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class QualityTestResult
{
    public string TestId { get; set; } = Guid.NewGuid().ToString();
    public QualityTestType TestType { get; set; }
    public string TestName { get; set; } = string.Empty;
    public TestResult Result { get; set; } = TestResult.Pending;
    
    // Test execution details
    public DateTime TestDate { get; set; } = DateTime.UtcNow;
    public string TestEquipmentId { get; set; } = string.Empty;
    public string TechnicianId { get; set; } = string.Empty;
    public string TestProcedure { get; set; } = string.Empty;
    public TimeSpan TestDuration { get; set; }
    
    // Measured values
    public Dictionary<string, double> MeasuredValues { get; set; } = new();
    public Dictionary<string, string> TestParameters { get; set; } = new();
    
    // Specifications and limits
    public Dictionary<string, double> RequiredMinValues { get; set; } = new();
    public Dictionary<string, double> RequiredMaxValues { get; set; } = new();
    public Dictionary<string, string> AcceptanceCriteria { get; set; } = new();
    
    // Results analysis
    public bool PassedAllCriteria { get; set; }
    public List<string> FailedCriteria { get; set; } = new();
    public double ConfidenceLevel { get; set; } = 95.0;        // % statistical confidence
    public string TestNotes { get; set; } = string.Empty;
    public List<string> Attachments { get; set; } = new();     // Test charts, photos, etc.
    
    // Calibration and traceability
    public DateTime EquipmentCalibrationDate { get; set; }
    public string CalibrationCertificate { get; set; } = string.Empty;
    public string TraceabilityCode { get; set; } = string.Empty;
}

public class TensileTestResult : QualityTestResult
{
    public double YieldStrength { get; set; }                  // MPa
    public double TensileStrength { get; set; }                // MPa
    public double ElongationAtBreak { get; set; }              // %
    public double ElongationUniformly { get; set; }            // %
    public double ReductionInArea { get; set; }                // %
    public double ModulusOfElasticity { get; set; }            // GPa
    public double YieldToTensileRatio { get; set; }            // Ratio
    public string FractureType { get; set; } = string.Empty;   // Cup-cone, shear, etc.
    public double CrossSectionalArea { get; set; }             // mm²
    public double GaugeLength { get; set; }                    // mm
}

public class BendTestResult : QualityTestResult
{
    public double BendAngle { get; set; }                      // degrees (typically 180°)
    public double BendRadius { get; set; }                     // mm
    public double PinDiameter { get; set; }                    // mm
    public bool CompletedWithoutCracking { get; set; }
    public List<string> CrackLocations { get; set; } = new();
    public double MaxCrackLength { get; set; }                 // mm
    public string CrackType { get; set; } = string.Empty;     // Surface, internal, etc.
    public bool ASTMBendCompliant { get; set; }
}

public class DimensionalTestResult : QualityTestResult
{
    public double NominalDiameter { get; set; }                // mm
    public double ActualDiameter { get; set; }                 // mm
    public double DiameterTolerance { get; set; }              // mm
    public double CrossSectionalArea { get; set; }             // mm²
    public double Ovality { get; set; }                        // mm max-min diameter
    
    // Rib measurements
    public double RibHeight { get; set; }                      // mm
    public double RibSpacing { get; set; }                     // mm
    public double RibAngle { get; set; }                       // degrees
    public double RelativeRibArea { get; set; }                // % of cross-section
    public bool RibbingCompliant { get; set; }
    
    // Length and straightness
    public double ActualLength { get; set; }                   // m
    public double Straightness { get; set; }                   // mm deviation per meter
    public double WeightPerMeter { get; set; }                 // kg/m
    
    // Surface condition
    public double SurfaceRoughness { get; set; }               // μm Ra
    public int SurfaceDefectCount { get; set; }
    public List<string> SurfaceDefectTypes { get; set; } = new();
}

public class ChemicalAnalysisResult : QualityTestResult
{
    public double CarbonContent { get; set; }                  // % C
    public double ManganeseContent { get; set; }               // % Mn
    public double PhosphorusContent { get; set; }              // % P
    public double SulfurContent { get; set; }                  // % S
    public double SiliconContent { get; set; }                 // % Si
    public double NitrogenContent { get; set; }                // % N
    public double ChromiumContent { get; set; }                // % Cr
    public double NickelContent { get; set; }                  // % Ni
    public double CopperContent { get; set; }                  // % Cu
    public double VanadiumContent { get; set; }                // % V
    
    // Analysis method
    public string AnalysisMethod { get; set; } = "OES";        // OES, XRF, ICP, etc.
    public string SamplePreparation { get; set; } = string.Empty;
    public int NumberOfReadings { get; set; } = 3;
    public double StandardDeviation { get; set; }
    
    // Compliance verification
    public bool CarbonCompliant { get; set; }
    public bool ManganeseCompliant { get; set; }
    public bool PhosphorusCompliant { get; set; }
    public bool SulfurCompliant { get; set; }
    public bool ChemicalCompositionCompliant { get; set; }
}

public class QualityControlStation
{
    public string StationId { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string StationType { get; set; } = string.Empty;    // Incoming, In-Process, Final
    
    // Operational status
    public string OperationalStatus { get; set; } = "Active";  // Active, Maintenance, Offline
    public List<QualityTestType> AvailableTests { get; set; } = new();
    public int CurrentWorkload { get; set; }
    public int MaxCapacity { get; set; }
    public double Utilization { get; set; }                    // %
    
    // Equipment and capabilities
    public List<QualityEquipment> Equipment { get; set; } = new();
    public List<string> CertifiedTechnicians { get; set; } = new();
    public Dictionary<QualityTestType, TimeSpan> TypicalTestTimes { get; set; } = new();
    
    // Performance metrics
    public double ThroughputRate { get; set; }                 // samples per hour
    public double QualityAccuracy { get; set; }                // % correct results
    public double EquipmentUptime { get; set; }                // %
    public double AverageTestTime { get; set; }                // minutes
    
    // Current queue
    public List<string> QueuedSamples { get; set; } = new();
    public TimeSpan EstimatedQueueTime { get; set; }
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class QualityEquipment
{
    public string EquipmentId { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;
    public string EquipmentType { get; set; } = string.Empty;  // Tensile machine, spectrometer, etc.
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    
    // Operational status
    public bool IsOperational { get; set; } = true;
    public string MaintenanceStatus { get; set; } = "Good";
    public DateTime LastMaintenance { get; set; }
    public DateTime NextMaintenanceDue { get; set; }
    
    // Calibration status
    public DateTime LastCalibration { get; set; }
    public DateTime NextCalibrationDue { get; set; }
    public string CalibrationCertificate { get; set; } = string.Empty;
    public bool CalibrationValid { get; set; } = true;
    
    // Capabilities
    public List<QualityTestType> SupportedTests { get; set; } = new();
    public Dictionary<string, double> Accuracy { get; set; } = new();      // Test parameter -> accuracy %
    public Dictionary<string, double> RepeatabilityStats { get; set; } = new();
    
    // Usage statistics
    public int TestsPerformed { get; set; }
    public double UtilizationRate { get; set; }                // %
    public TimeSpan TotalOperatingTime { get; set; }
    public List<string> RecentIssues { get; set; } = new();
}

public class QualityPerformanceMetrics
{
    public string StationId { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; }
    public TimeSpan CalculationPeriod { get; set; }
    
    // Testing volume and throughput
    public int TotalSamplesTested { get; set; }
    public int TotalTestsPerformed { get; set; }
    public double TestingThroughputRate { get; set; }           // tests per hour
    public double SampleProcessingRate { get; set; }            // samples per hour
    
    // Quality metrics
    public double OverallPassRate { get; set; }                // %
    public double ASTMComplianceRate { get; set; }              // %
    public double CustomerSpecComplianceRate { get; set; }      // %
    public double FirstPassYield { get; set; }                 // % pass on first test
    
    // Test-specific pass rates
    public Dictionary<QualityTestType, double> PassRateByTest { get; set; } = new();
    public Dictionary<RebarGrade, double> PassRateByGrade { get; set; } = new();
    public Dictionary<RebarSize, double> PassRateBySize { get; set; } = new();
    
    // Efficiency metrics
    public double EquipmentUtilization { get; set; }           // %
    public double StationUtilization { get; set; }             // %
    public double TechnicianUtilization { get; set; }          // %
    public TimeSpan AverageTestTime { get; set; }
    public TimeSpan AverageQueueTime { get; set; }
    
    // Quality trends
    public double QualityScoreTrend { get; set; }              // % change
    public double DefectRateTrend { get; set; }                // defects per 1000 samples
    public List<string> TopFailureModes { get; set; } = new();
    public Dictionary<string, int> DefectsByCategory { get; set; } = new();
    
    // Cost and productivity
    public double TestingCostPerSample { get; set; }           // $
    public double ProductivityIndex { get; set; }              // samples/technician/hour
    public double QualityROI { get; set; }                     // $ saved per $ spent on QC
    public int RetestCount { get; set; }
    public double RetestRate { get; set; }                     // %
}

public class QualityAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string StationId { get; set; } = string.Empty;
    public string? SampleId { get; set; }
    public string? BatchId { get; set; }
    public RebarGrade? AffectedGrade { get; set; }
    public RebarSize? AffectedSize { get; set; }
    
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical
    public string Description { get; set; } = string.Empty;
    
    // Test-related information
    public QualityTestType? TestType { get; set; }
    public string? TestEquipmentId { get; set; }
    public double? TestValue { get; set; }
    public double? SpecificationLimit { get; set; }
    public double? Deviation { get; set; }
    
    // Compliance issues
    public bool ASTMNonCompliance { get; set; }
    public bool CustomerSpecNonCompliance { get; set; }
    public List<string> FailedCriteria { get; set; } = new();
    
    // Equipment status
    public bool? EquipmentOperational { get; set; }
    public bool? CalibrationValid { get; set; }
    public DateTime? LastCalibrationCheck { get; set; }
    
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionAction { get; set; }
    public string? QualityInspector { get; set; }
    
    public static class AlertTypes
    {
        public const string TestFailure = "TEST_FAILURE";
        public const string ASTMNonCompliance = "ASTM_NON_COMPLIANCE";
        public const string CustomerSpecFailure = "CUSTOMER_SPEC_FAILURE";
        public const string EquipmentMalfunction = "EQUIPMENT_MALFUNCTION";
        public const string CalibrationExpired = "CALIBRATION_EXPIRED";
        public const string QualityTrendNegative = "QUALITY_TREND_NEGATIVE";
        public const string DefectRateHigh = "DEFECT_RATE_HIGH";
        public const string TestTimeExceeded = "TEST_TIME_EXCEEDED";
        public const string SampleContamination = "SAMPLE_CONTAMINATION";
        public const string ChemicalCompositionOff = "CHEMICAL_COMPOSITION_OFF";
        public const string DimensionalDeviation = "DIMENSIONAL_DEVIATION";
        public const string MechanicalPropertyFailure = "MECHANICAL_PROPERTY_FAILURE";
        public const string QueueBacklogHigh = "QUEUE_BACKLOG_HIGH";
    }
    
    public string GetRecommendedAction()
    {
        return AlertType switch
        {
            AlertTypes.TestFailure => "Review test procedure, verify sample preparation, check equipment calibration",
            AlertTypes.ASTMNonCompliance => "Stop production, segregate non-compliant material, review process parameters",
            AlertTypes.CustomerSpecFailure => "Contact customer for deviation approval, consider rework options",
            AlertTypes.EquipmentMalfunction => "Stop testing, inspect equipment, contact maintenance team",
            AlertTypes.CalibrationExpired => "Stop testing, schedule immediate calibration, verify recent test results",
            AlertTypes.QualityTrendNegative => "Investigate process changes, increase sampling frequency, review procedures",
            AlertTypes.DefectRateHigh => "Increase upstream process controls, review material handling procedures",
            AlertTypes.TestTimeExceeded => "Optimize test procedures, check equipment performance, review workload",
            AlertTypes.SampleContamination => "Review sampling procedures, check sample handling, retrain personnel",
            AlertTypes.ChemicalCompositionOff => "Check ladle metallurgy process, verify analysis equipment, review heat treatment",
            AlertTypes.DimensionalDeviation => "Check rolling mill setup, verify measurement equipment, review specifications",
            AlertTypes.MechanicalPropertyFailure => "Review heat treatment parameters, check chemical composition, verify test procedure",
            AlertTypes.QueueBacklogHigh => "Increase testing capacity, optimize workflow, consider overtime scheduling",
            _ => "Contact quality control supervisor for detailed investigation"
        };
    }
}

// ASTM specification templates
public class ASTMSpecification
{
    public ASTMStandard Standard { get; set; }
    public RebarGrade Grade { get; set; }
    public RebarSize Size { get; set; }
    
    // Mechanical property requirements
    public double MinYieldStrength { get; set; }               // MPa
    public double MinTensileStrength { get; set; }             // MPa
    public double MinElongation { get; set; }                  // %
    public double MaxYieldToTensileRatio { get; set; }         // Ratio
    
    // Chemical composition limits
    public double MaxCarbon { get; set; }                      // %
    public double MaxManganese { get; set; }                   // %
    public double MaxPhosphorus { get; set; }                  // %
    public double MaxSulfur { get; set; }                      // %
    public double MaxSilicon { get; set; }                     // %
    
    // Dimensional requirements
    public double NominalDiameter { get; set; }                // mm
    public double DiameterTolerance { get; set; }              // ±mm
    public double MinRibHeight { get; set; }                   // mm
    public double MaxRibSpacing { get; set; }                  // mm
    public double MinRelativeRibArea { get; set; }             // %
    
    // Bend test requirements
    public double BendTestAngle { get; set; } = 180.0;         // degrees
    public double BendPinDiameter { get; set; }                // times bar diameter
    
    public static ASTMSpecification GetSpecification(ASTMStandard standard, RebarGrade grade, RebarSize size)
    {
        var spec = new ASTMSpecification
        {
            Standard = standard,
            Grade = grade,
            Size = size,
            NominalDiameter = (double)size,
            DiameterTolerance = 0.5 // Standard tolerance ±0.5mm
        };

        // Set mechanical properties based on grade
        (spec.MinYieldStrength, spec.MinTensileStrength) = grade switch
        {
            RebarGrade.Grade40 => (275, 420),
            RebarGrade.Grade60 => (420, 620),
            RebarGrade.Grade75 => (520, 690),
            RebarGrade.Grade80 => (550, 720),
            _ => (420, 620)
        };

        // Elongation requirements (minimum % in 200mm gauge length)
        spec.MinElongation = grade switch
        {
            RebarGrade.Grade40 => 12.0,
            RebarGrade.Grade60 => 9.0,
            RebarGrade.Grade75 => 7.0,
            RebarGrade.Grade80 => 6.0,
            _ => 9.0
        };

        // Chemical composition limits (A615 standard)
        if (standard == ASTMStandard.A615)
        {
            spec.MaxCarbon = 0.30;
            spec.MaxManganese = 1.50;
            spec.MaxPhosphorus = 0.040;
            spec.MaxSulfur = 0.050;
            spec.MaxSilicon = 0.50;
        }
        else if (standard == ASTMStandard.A706) // Seismic applications
        {
            spec.MaxCarbon = 0.25;
            spec.MaxManganese = 1.35;
            spec.MaxPhosphorus = 0.035;
            spec.MaxSulfur = 0.045;
            spec.MaxYieldToTensileRatio = 0.85;
        }

        // Ribbing requirements
        var diameter = (double)size;
        spec.MinRibHeight = diameter * 0.043;     // 4.3% of diameter minimum
        spec.MaxRibSpacing = diameter * 0.7;      // 70% of diameter maximum
        spec.MinRelativeRibArea = 0.055;          // 5.5% minimum

        // Bend test requirements
        spec.BendPinDiameter = size <= RebarSize.Size10 ? 3.0 : 4.0; // 3d for ≤#10, 4d for >#10

        return spec;
    }
}