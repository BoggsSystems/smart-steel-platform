using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CastingService.Models;

// Rebar-specific enumerations and constants
public enum RebarGrade
{
    Grade40 = 40,    // 40 ksi yield strength (275 MPa)
    Grade60 = 60,    // 60 ksi yield strength (420 MPa) 
    Grade75 = 75,    // 75 ksi yield strength (520 MPa)
    Grade80 = 80     // 80 ksi yield strength (550 MPa)
}

public enum BilletSize
{
    Size100x100 = 100,  // 100mm x 100mm billet
    Size120x120 = 120,  // 120mm x 120mm billet  
    Size150x150 = 150,  // 150mm x 150mm billet
    Size160x160 = 160   // 160mm x 160mm billet
}

public static class RebarSpecifications
{
    public static readonly Dictionary<RebarGrade, ChemicalComposition> GradeSpecs = new()
    {
        { RebarGrade.Grade40, new ChemicalComposition { MaxCarbon = 0.31, MaxManganese = 1.50, MaxPhosphorus = 0.04, MaxSulfur = 0.05 } },
        { RebarGrade.Grade60, new ChemicalComposition { MaxCarbon = 0.30, MaxManganese = 1.50, MaxPhosphorus = 0.04, MaxSulfur = 0.05 } },
        { RebarGrade.Grade75, new ChemicalComposition { MaxCarbon = 0.28, MaxManganese = 1.50, MaxPhosphorus = 0.035, MaxSulfur = 0.04 } },
        { RebarGrade.Grade80, new ChemicalComposition { MaxCarbon = 0.25, MaxManganese = 1.50, MaxPhosphorus = 0.035, MaxSulfur = 0.04 } }
    };
}

public class ChemicalComposition
{
    public double MaxCarbon { get; set; }
    public double MaxManganese { get; set; }
    public double MaxPhosphorus { get; set; }
    public double MaxSulfur { get; set; }
}

public class RebarCastingStatus
{
    public string CasterId { get; set; } = string.Empty;
    public RebarGrade TargetGrade { get; set; } = RebarGrade.Grade60;
    public BilletSize BilletSize { get; set; } = BilletSize.Size120x120;
    public double TundishTemperature { get; set; }
    public double MoldTemperature { get; set; }
    public double CastingSpeed { get; set; }
    public double LiquidLevel { get; set; }
    public double SteelFlow { get; set; }
    public string CastingState { get; set; } = "Idle";
    
    // Rebar-specific properties
    public double CarbonContent { get; set; }
    public double ManganeseContent { get; set; }
    public double PhosphorusContent { get; set; }
    public double SulfurContent { get; set; }
    public double BilletWidth { get; set; }
    public double BilletLength { get; set; }
    public double SurfaceQualityIndex { get; set; }
    public int BilletsProduced { get; set; }
    public DateTime LastUpdated { get; set; }
    
    // Quality compliance check
    public bool IsChemicalCompositionCompliant()
    {
        var spec = RebarSpecifications.GradeSpecs[TargetGrade];
        return CarbonContent <= spec.MaxCarbon && 
               ManganeseContent <= spec.MaxManganese &&
               PhosphorusContent <= spec.MaxPhosphorus &&
               SulfurContent <= spec.MaxSulfur;
    }
}

public class RebarCastingTelemetry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CasterId { get; set; } = string.Empty;
    public RebarGrade CurrentGrade { get; set; }
    public BilletSize CurrentBilletSize { get; set; }
    
    // Standard casting telemetry
    public double TundishTemperature { get; set; }
    public double MoldTemperature { get; set; }
    public double MoldCoolingWaterFlow { get; set; }
    public double MoldCoolingWaterTemp { get; set; }
    public double CastingSpeed { get; set; }
    public double LiquidLevel { get; set; }
    public double SteelFlow { get; set; }
    public double VibrationLevel { get; set; }
    
    // Rebar-specific telemetry
    public double CarbonContent { get; set; }
    public double ManganeseContent { get; set; }
    public double PhosphorusContent { get; set; }
    public double SulfurContent { get; set; }
    public double BilletWidth { get; set; }
    public double BilletThickness { get; set; }
    public double BilletSurfaceTemp { get; set; }
    public double BilletCoolingRate { get; set; }
    public double SurfaceDefectCount { get; set; }
    public double BilletStraightness { get; set; } // Deviation from straight line
    public double CrackDetectionScore { get; set; } // AI-based crack detection
    public DateTime Timestamp { get; set; }
}

public class RebarMoldConfiguration
{
    public RebarGrade TargetGrade { get; set; }
    public BilletSize BilletSize { get; set; }
    public double TargetTemperature { get; set; }
    public double TargetCoolingRate { get; set; }
    public double TargetCastingSpeed { get; set; }
    public double MaxVibrationLevel { get; set; }
    public double OptimalSteelFlow { get; set; }
    
    // Rebar-specific mold parameters
    public double MoldTaper { get; set; } // Taper for billet extraction
    public double OptimalBilletWidth { get; set; }
    public double MaxSurfaceDefectThreshold { get; set; }
    public double CoolingWaterPressure { get; set; }
    public double MoldLifeExpectancy { get; set; } // Number of casts before replacement
    public int CurrentMoldCastCount { get; set; }
    
    // Get optimal parameters by grade
    public static RebarMoldConfiguration GetOptimalConfig(RebarGrade grade, BilletSize size)
    {
        return grade switch
        {
            RebarGrade.Grade40 => new RebarMoldConfiguration { TargetTemperature = 1520, TargetCastingSpeed = 1.8, BilletSize = size, TargetGrade = grade },
            RebarGrade.Grade60 => new RebarMoldConfiguration { TargetTemperature = 1540, TargetCastingSpeed = 1.6, BilletSize = size, TargetGrade = grade },
            RebarGrade.Grade75 => new RebarMoldConfiguration { TargetTemperature = 1560, TargetCastingSpeed = 1.4, BilletSize = size, TargetGrade = grade },
            RebarGrade.Grade80 => new RebarMoldConfiguration { TargetTemperature = 1580, TargetCastingSpeed = 1.2, BilletSize = size, TargetGrade = grade },
            _ => new RebarMoldConfiguration { TargetTemperature = 1540, TargetCastingSpeed = 1.6, BilletSize = size, TargetGrade = grade }
        };
    }
}

public class RebarCastingPerformanceMetrics
{
    public string CasterId { get; set; } = string.Empty;
    public RebarGrade Grade { get; set; }
    public BilletSize BilletSize { get; set; }
    
    // Standard performance metrics
    public double CastingEfficiency { get; set; }
    public double QualityIndex { get; set; }
    public double TemperatureStability { get; set; }
    public double CoolingEfficiency { get; set; }
    public double VibrationLevel { get; set; }
    public double ThroughputRate { get; set; }
    
    // Rebar-specific performance metrics
    public double ChemicalComplianceRate { get; set; } // % of billets meeting chemical specs
    public double DimensionalAccuracy { get; set; } // Billet size consistency
    public double SurfaceQualityScore { get; set; } // Surface defect rate
    public double BilletStraightnessIndex { get; set; } // Straightness quality
    public double YieldRatio { get; set; } // Usable billet percentage
    public double EnergyConsumptionPerTon { get; set; } // kWh per ton of billets
    public double WaterUsagePerTon { get; set; } // Cooling water consumption
    public double MoldWearRate { get; set; } // Mold deterioration rate
    
    // Production statistics
    public int TotalBilletsProduced { get; set; }
    public int DefectiveBilletsCount { get; set; }
    public double ProductionRate { get; set; } // Billets per hour
    public DateTime CalculatedAt { get; set; }
    
    // Calculate overall rebar quality score
    public double GetRebarQualityScore()
    {
        return (ChemicalComplianceRate * 0.3 + 
                DimensionalAccuracy * 0.25 + 
                SurfaceQualityScore * 0.25 + 
                BilletStraightnessIndex * 0.2) / 100.0;
    }
}

public class RebarCastingAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CasterId { get; set; } = string.Empty;
    public RebarGrade AffectedGrade { get; set; }
    public BilletSize AffectedBilletSize { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string Description { get; set; } = string.Empty;
    
    // Standard readings
    public double? TemperatureReading { get; set; }
    public double? VibrationReading { get; set; }
    public double? FlowReading { get; set; }
    
    // Rebar-specific readings
    public double? CarbonContentReading { get; set; }
    public double? BilletWidthDeviation { get; set; }
    public double? SurfaceDefectCount { get; set; }
    public double? MoldWearLevel { get; set; }
    public double? CrackDetectionScore { get; set; }
    
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
    
    // Rebar-specific alert types
    public static class AlertTypes
    {
        public const string ChemicalCompositionOutOfSpec = "CHEMICAL_OUT_OF_SPEC";
        public const string BilletSizeDeviation = "BILLET_SIZE_DEVIATION";
        public const string SurfaceDefectThresholdExceeded = "SURFACE_DEFECT_THRESHOLD";
        public const string MoldWearCritical = "MOLD_WEAR_CRITICAL";
        public const string CrackDetected = "CRACK_DETECTED";
        public const string BilletStraightnessIssue = "STRAIGHTNESS_ISSUE";
        public const string CoolingRateAbnormal = "COOLING_RATE_ABNORMAL";
        public const string QualityGradeFallback = "QUALITY_GRADE_FALLBACK";
    }
    
    // Get recommended action based on alert type
    public string GetRecommendedAction()
    {
        return AlertType switch
        {
            AlertTypes.ChemicalCompositionOutOfSpec => "Adjust ladle metallurgy, check alloy additions",
            AlertTypes.BilletSizeDeviation => "Inspect mold condition, adjust casting parameters",
            AlertTypes.SurfaceDefectThresholdExceeded => "Reduce casting speed, check mold lubrication",
            AlertTypes.MoldWearCritical => "Schedule mold replacement during next maintenance window",
            AlertTypes.CrackDetected => "Stop casting, inspect billet, check cooling pattern",
            AlertTypes.BilletStraightnessIssue => "Check support roller alignment, adjust withdrawal",
            AlertTypes.CoolingRateAbnormal => "Verify water flow rates, check cooling circuit",
            AlertTypes.QualityGradeFallback => "Review entire casting parameters for grade compliance",
            _ => "Contact engineering for detailed analysis"
        };
    }
}

// Additional rebar-specific models
public class BilletQualityInspection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BilletId { get; set; } = string.Empty;
    public string CasterId { get; set; } = string.Empty;
    public RebarGrade Grade { get; set; }
    public BilletSize Size { get; set; }
    
    // Chemical analysis results
    public double MeasuredCarbon { get; set; }
    public double MeasuredManganese { get; set; }
    public double MeasuredPhosphorus { get; set; }
    public double MeasuredSulfur { get; set; }
    public double MeasuredSilicon { get; set; }
    
    // Physical measurements
    public double ActualWidth { get; set; }
    public double ActualThickness { get; set; }
    public double ActualLength { get; set; }
    public double SurfaceRoughness { get; set; }
    public double Straightness { get; set; }
    public int SurfaceDefectCount { get; set; }
    public bool HasInternalCracks { get; set; }
    
    // Quality assessment
    public bool PassesChemicalSpec { get; set; }
    public bool PassesDimensionalSpec { get; set; }
    public bool PassesSurfaceSpec { get; set; }
    public string OverallGrade { get; set; } = "A"; // A, B, C, or Reject
    public string InspectorId { get; set; } = string.Empty;
    public DateTime InspectedAt { get; set; } = DateTime.UtcNow;
    
    // Calculate compliance score
    public double GetComplianceScore()
    {
        double score = 0;
        if (PassesChemicalSpec) score += 40;
        if (PassesDimensionalSpec) score += 30;
        if (PassesSurfaceSpec) score += 30;
        return score;
    }
}

public class RebarProductionSchedule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CasterId { get; set; } = string.Empty;
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public RebarGrade TargetGrade { get; set; }
    public BilletSize TargetBilletSize { get; set; }
    public int TargetBilletCount { get; set; }
    public double TargetTonnage { get; set; }
    public string CustomerOrderId { get; set; } = string.Empty;
    public string ProductionStatus { get; set; } = "Scheduled";
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public int ActualBilletCount { get; set; }
    public double ActualTonnage { get; set; }
    public double QualityAchieved { get; set; }
}