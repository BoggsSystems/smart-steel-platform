using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BundlingService.Models;

// Rebar specifications and enumerations
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

public enum RebarLength
{
    Length20ft = 20,  // 20 feet (6.1m)
    Length30ft = 30,  // 30 feet (9.1m)
    Length40ft = 40,  // 40 feet (12.2m)
    Length60ft = 60   // 60 feet (18.3m)
}

public enum BundleStatus
{
    Creating,         // Bundle being assembled
    QualityCheck,     // Under quality inspection
    Tying,           // Bundle tying in progress
    Tagging,         // Tag and documentation generation
    Ready,           // Ready for shipping
    Shipped,         // Bundle shipped
    Rejected         // Failed quality control
}

public class RebarBar
{
    public string BarId { get; set; } = string.Empty;
    public string HeatNumber { get; set; } = string.Empty;
    public RebarGrade Grade { get; set; }
    public RebarSize Size { get; set; }
    public RebarLength Length { get; set; }
    public double ActualLength { get; set; } // Actual measured length
    public double Weight { get; set; }       // kg
    public double Diameter { get; set; }     // mm
    
    // Quality properties
    public bool PassedTensileTest { get; set; }
    public bool PassedBendTest { get; set; }
    public double SurfaceQuality { get; set; } // 0-100 score
    public double Straightness { get; set; }   // deviation in mm
    public bool HasSurfaceDefects { get; set; }
    
    // Traceability
    public DateTime ProducedAt { get; set; }
    public string CasterId { get; set; } = string.Empty;
    public string RollingMillId { get; set; } = string.Empty;
    public string QualityInspectorId { get; set; } = string.Empty;
}

public class RebarBundle
{
    public string BundleId { get; set; } = Guid.NewGuid().ToString();
    public string CustomerOrderId { get; set; } = string.Empty;
    public string BundlingLineId { get; set; } = string.Empty;
    
    // Bundle specifications
    public RebarGrade Grade { get; set; }
    public RebarSize Size { get; set; }
    public RebarLength Length { get; set; }
    public int BarCount { get; set; }
    public double TotalWeight { get; set; } // kg
    public double CalculatedWeight { get; set; } // Expected weight
    public double WeightTolerance { get; set; } = 2.0; // %
    
    // Bundle composition
    public List<RebarBar> Bars { get; set; } = new();
    public HashSet<string> HeatNumbers { get; set; } = new(); // All heat numbers in bundle
    
    // Quality control
    public bool PassedQualityCheck { get; set; }
    public double QualityScore { get; set; }
    public string QualityInspectorId { get; set; } = string.Empty;
    public DateTime? QualityCheckedAt { get; set; }
    public List<string> QualityIssues { get; set; } = new();
    
    // Bundle status and tracking
    public BundleStatus Status { get; set; } = BundleStatus.Creating;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string OperatorId { get; set; } = string.Empty;
    
    // Physical bundle properties
    public double BundleDiameter { get; set; } // mm
    public string TyingMaterial { get; set; } = "steel_wire"; // steel_wire, straps
    public int TyingPoints { get; set; } = 2; // Number of tie points
    public bool IsStraight { get; set; } = true;
    
    // Documentation
    public string BundleTag { get; set; } = string.Empty;
    public string MillTestCertificate { get; set; } = string.Empty;
    public string BarcodeData { get; set; } = string.Empty;
    public string QRCodeData { get; set; } = string.Empty;
    
    // Shipping information
    public string ShippingDestination { get; set; } = string.Empty;
    public string ShippingInstructions { get; set; } = string.Empty;
    public DateTime? RequestedDelivery { get; set; }
    
    // Calculate bundle specifications
    public static BundleSpecification GetBundleSpec(RebarGrade grade, RebarSize size, RebarLength length)
    {
        var diameter = (double)size;
        var lengthFt = (double)length;
        
        // Standard bundle specifications based on rebar size
        var maxBarsPerBundle = size switch
        {
            RebarSize.Size3 or RebarSize.Size4 => 60,
            RebarSize.Size5 or RebarSize.Size6 => 50,
            RebarSize.Size7 or RebarSize.Size8 => 40,
            RebarSize.Size9 or RebarSize.Size10 => 30,
            RebarSize.Size11 or RebarSize.Size14 => 20,
            RebarSize.Size18 => 15,
            _ => 40
        };
        
        // Weight per foot for different sizes (approximate)
        var weightPerFoot = size switch
        {
            RebarSize.Size3 => 0.376, // kg/ft
            RebarSize.Size4 => 0.668,
            RebarSize.Size5 => 1.043,
            RebarSize.Size6 => 1.502,
            RebarSize.Size7 => 2.044,
            RebarSize.Size8 => 2.670,
            RebarSize.Size9 => 3.400,
            RebarSize.Size10 => 4.303,
            RebarSize.Size11 => 5.313,
            RebarSize.Size14 => 7.650,
            RebarSize.Size18 => 13.60,
            _ => 1.0
        };
        
        var maxWeightPerBundle = 4500.0; // kg (5 ton max for crane handling)
        var calculatedMaxBars = (int)(maxWeightPerBundle / (weightPerFoot * lengthFt));
        var actualMaxBars = Math.Min(maxBarsPerBundle, calculatedMaxBars);
        
        return new BundleSpecification
        {
            Grade = grade,
            Size = size,
            Length = length,
            MaxBarsPerBundle = actualMaxBars,
            WeightPerBar = weightPerFoot * lengthFt,
            MaxBundleWeight = maxWeightPerBundle,
            StandardTyingPoints = lengthFt <= 20 ? 2 : (lengthFt <= 40 ? 3 : 4)
        };
    }
    
    public bool IsWithinWeightTolerance()
    {
        if (CalculatedWeight <= 0) return true;
        var deviation = Math.Abs(TotalWeight - CalculatedWeight) / CalculatedWeight * 100;
        return deviation <= WeightTolerance;
    }
    
    public bool IsQualityCompliant()
    {
        return Bars.All(b => b.PassedTensileTest && b.PassedBendTest && !b.HasSurfaceDefects) &&
               IsWithinWeightTolerance() &&
               QualityScore >= 85.0;
    }
}

public class BundleSpecification
{
    public RebarGrade Grade { get; set; }
    public RebarSize Size { get; set; }
    public RebarLength Length { get; set; }
    public int MaxBarsPerBundle { get; set; }
    public double WeightPerBar { get; set; }
    public double MaxBundleWeight { get; set; }
    public int StandardTyingPoints { get; set; }
}

public class RebarBundlingStatus
{
    public string BundlingLineId { get; set; } = string.Empty;
    public string LineName { get; set; } = string.Empty;
    public string OperationalState { get; set; } = "Idle"; // Idle, Active, Maintenance, Emergency
    
    // Current production
    public RebarBundle? CurrentBundle { get; set; }
    public RebarGrade CurrentGrade { get; set; } = RebarGrade.Grade60;
    public RebarSize CurrentSize { get; set; } = RebarSize.Size5;
    public RebarLength CurrentLength { get; set; } = RebarLength.Length40ft;
    
    // Line performance
    public double BundlingSpeed { get; set; }     // bundles per hour
    public double QualityPassRate { get; set; }   // %
    public double WeightAccuracy { get; set; }    // %
    public double TyingEfficiency { get; set; }   // %
    public double TaggingAccuracy { get; set; }   // %
    
    // Equipment status
    public bool CraneOperational { get; set; } = true;
    public bool TyingMachineOperational { get; set; } = true;
    public bool WeighingSystemOperational { get; set; } = true;
    public bool TagPrinterOperational { get; set; } = true;
    public bool BarcodeReaderOperational { get; set; } = true;
    
    // Production metrics
    public int BundlesCompleted { get; set; }
    public int BundlesRejected { get; set; }
    public double TotalTonnage { get; set; }
    public double AvgBundleWeight { get; set; }
    
    // Quality metrics
    public double OverallQualityScore { get; set; }
    public int QualityDefects { get; set; }
    public double CustomerCompliance { get; set; }
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class RebarBundlingTelemetry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BundlingLineId { get; set; } = string.Empty;
    public string? CurrentBundleId { get; set; }
    
    // Current operation
    public RebarGrade Grade { get; set; }
    public RebarSize Size { get; set; }
    public RebarLength Length { get; set; }
    
    // Equipment telemetry
    public double CraneLoadCapacity { get; set; }    // % of max capacity
    public double TyingMachineSpeed { get; set; }    // ties per minute
    public double WeighingAccuracy { get; set; }     // %
    public double ConveyorSpeed { get; set; }        // m/min
    
    // Process measurements
    public int BarsInCurrentBundle { get; set; }
    public double CurrentBundleWeight { get; set; }
    public double WeightDeviation { get; set; }      // % from expected
    public double BundleCompactness { get; set; }    // how tight the bundle
    public double TyingTension { get; set; }         // N/m
    
    // Quality measurements
    public double StraightnessCheck { get; set; }    // bundle straightness score
    public int VisualDefectsCount { get; set; }
    public double BarcodeReadSuccess { get; set; }   // %
    public double TagPrintQuality { get; set; }      // %
    
    // Environmental conditions
    public double AmbientTemperature { get; set; }
    public double Humidity { get; set; }
    public double DustLevel { get; set; }
    
    // Performance metrics
    public double LineUtilization { get; set; }      // %
    public double ThroughputRate { get; set; }       // bars per hour
    public double OverallEfficiency { get; set; }    // %
    public double EnergyConsumption { get; set; }    // kWh
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class BundlingConfiguration
{
    public string BundlingLineId { get; set; } = string.Empty;
    public RebarGrade TargetGrade { get; set; }
    public RebarSize TargetSize { get; set; }
    public RebarLength TargetLength { get; set; }
    
    // Bundle specifications
    public int MaxBarsPerBundle { get; set; }
    public double TargetBundleWeight { get; set; }
    public double WeightTolerance { get; set; } = 2.0; // %
    public int TyingPoints { get; set; }
    public string TyingMaterial { get; set; } = "steel_wire";
    
    // Quality thresholds
    public double MinQualityScore { get; set; } = 85.0;
    public double MaxWeightDeviation { get; set; } = 3.0; // %
    public double MinStraightnessScore { get; set; } = 90.0;
    
    // Process parameters
    public double TargetBundlingSpeed { get; set; } = 8.0; // bundles per hour
    public double TyingTension { get; set; } = 500.0; // N
    public bool AutomaticQualityCheck { get; set; } = true;
    public bool RequireOperatorApproval { get; set; } = false;
    
    // Documentation settings
    public bool GenerateQRCode { get; set; } = true;
    public bool GenerateBarcode { get; set; } = true;
    public bool PrintMillTestCertificate { get; set; } = true;
    public string TagTemplate { get; set; } = "standard_rebar_tag";
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}

public class BundlingPerformanceMetrics
{
    public string BundlingLineId { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; }
    public TimeSpan CalculationPeriod { get; set; }
    
    // Production metrics
    public int TotalBundlesCompleted { get; set; }
    public int TotalBarsProcessed { get; set; }
    public double TotalTonnage { get; set; }
    public double AverageBundlingRate { get; set; } // bundles per hour
    public double LineUtilization { get; set; } // %
    
    // Quality metrics
    public double QualityPassRate { get; set; } // %
    public double WeightAccuracy { get; set; } // %
    public double TyingQuality { get; set; } // %
    public double TaggingAccuracy { get; set; } // %
    public double CustomerCompliance { get; set; } // %
    
    // Efficiency metrics
    public double OverallEfficiency { get; set; } // %
    public double EquipmentUptime { get; set; } // %
    public double MaterialUtilization { get; set; } // %
    public double LaborEfficiency { get; set; } // %
    
    // Grade-specific metrics
    public Dictionary<RebarGrade, int> BundlesByGrade { get; set; } = new();
    public Dictionary<RebarSize, int> BundlesBySize { get; set; } = new();
    public Dictionary<RebarLength, int> BundlesByLength { get; set; } = new();
    
    // Cost metrics
    public double TyingMaterialCost { get; set; } // per ton
    public double LaborCostPerBundle { get; set; }
    public double EnergyConsumptionPerTon { get; set; } // kWh/ton
    public double MaintenanceCostPerTon { get; set; }
    
    // Defect analysis
    public int WeightDeviationDefects { get; set; }
    public int TyingDefects { get; set; }
    public int TaggingDefects { get; set; }
    public int StraightnessDefects { get; set; }
    public double DefectRate { get; set; } // defects per 1000 bundles
}

public class RebarBundlingAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BundlingLineId { get; set; } = string.Empty;
    public string? BundleId { get; set; }
    public RebarGrade? AffectedGrade { get; set; }
    public RebarSize? AffectedSize { get; set; }
    
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical
    public string Description { get; set; } = string.Empty;
    
    // Measurement readings
    public double? WeightReading { get; set; }
    public double? WeightDeviation { get; set; }
    public double? QualityScore { get; set; }
    public double? TyingTension { get; set; }
    public int? BarCount { get; set; }
    
    // Equipment status
    public bool? CraneOperational { get; set; }
    public bool? TyingMachineOperational { get; set; }
    public bool? WeighingSystemOperational { get; set; }
    
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionAction { get; set; }
    public string? OperatorId { get; set; }
    
    public static class AlertTypes
    {
        public const string WeightOutOfTolerance = "WEIGHT_OUT_OF_TOLERANCE";
        public const string QualityCheckFailed = "QUALITY_CHECK_FAILED";
        public const string TyingSystemFailure = "TYING_SYSTEM_FAILURE";
        public const string WeighingSystemError = "WEIGHING_SYSTEM_ERROR";
        public const string CraneOverload = "CRANE_OVERLOAD";
        public const string TagPrintingError = "TAG_PRINTING_ERROR";
        public const string BarcodeReadError = "BARCODE_READ_ERROR";
        public const string BundleStraightnessIssue = "BUNDLE_STRAIGHTNESS_ISSUE";
        public const string MaterialMismatch = "MATERIAL_MISMATCH";
        public const string ExcessiveDefects = "EXCESSIVE_DEFECTS";
        public const string ProductionRateBelow = "PRODUCTION_RATE_BELOW";
        public const string HeatNumberMismatch = "HEAT_NUMBER_MISMATCH";
    }
    
    public string GetRecommendedAction()
    {
        return AlertType switch
        {
            AlertTypes.WeightOutOfTolerance => "Check weighing system calibration, verify bar count",
            AlertTypes.QualityCheckFailed => "Inspect bars individually, separate non-conforming material",
            AlertTypes.TyingSystemFailure => "Stop production, inspect tying mechanism, call maintenance",
            AlertTypes.WeighingSystemError => "Recalibrate weighing system, verify load cells",
            AlertTypes.CraneOverload => "Reduce bundle size, check crane capacity settings",
            AlertTypes.TagPrintingError => "Check printer supplies, verify tag template",
            AlertTypes.BarcodeReadError => "Clean barcode scanner, verify lighting conditions",
            AlertTypes.BundleStraightnessIssue => "Adjust bundle alignment, check straightening equipment",
            AlertTypes.MaterialMismatch => "Verify grade/size sorting, check upstream processes",
            AlertTypes.ExcessiveDefects => "Review quality control procedures, increase inspection",
            AlertTypes.ProductionRateBelow => "Check equipment status, optimize workflow",
            AlertTypes.HeatNumberMismatch => "Verify traceability, separate affected bundles",
            _ => "Contact production supervisor for detailed analysis"
        };
    }
}

// Mill Test Certificate data structure
public class MillTestCertificate
{
    public string CertificateNumber { get; set; } = string.Empty;
    public string BundleId { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    
    // Material specifications
    public RebarGrade Grade { get; set; }
    public RebarSize Size { get; set; }
    public RebarLength Length { get; set; }
    public int BarCount { get; set; }
    public double TotalWeight { get; set; }
    
    // Chemical analysis (representative sample)
    public double CarbonContent { get; set; }
    public double ManganeseContent { get; set; }
    public double PhosphorusContent { get; set; }
    public double SulfurContent { get; set; }
    public double SiliconContent { get; set; }
    
    // Mechanical properties
    public double YieldStrength { get; set; } // MPa
    public double TensileStrength { get; set; } // MPa
    public double Elongation { get; set; } // %
    public double BendTestResult { get; set; } // degrees
    
    // Manufacturing details
    public List<string> HeatNumbers { get; set; } = new();
    public string SteelMillName { get; set; } = string.Empty;
    public string ProductionFacility { get; set; } = string.Empty;
    
    // Compliance certifications
    public bool ASTMCompliance { get; set; }
    public string ASTMStandard { get; set; } = "A615"; // A615, A706, etc.
    public bool CustomerSpecCompliance { get; set; }
    public string CustomerSpecification { get; set; } = string.Empty;
    
    // Authorized signatures
    public string QualityManagerSignature { get; set; } = string.Empty;
    public string CertifyingEngineer { get; set; } = string.Empty;
    public DateTime CertificationDate { get; set; } = DateTime.UtcNow;
    
    public string GenerateCertificateNumber()
    {
        return $"MTC-{DateTime.UtcNow:yyyyMMdd}-{BundleId.Substring(0, 8).ToUpper()}";
    }
}