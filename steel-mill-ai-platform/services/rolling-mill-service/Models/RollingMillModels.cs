using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RollingMillService.Models;

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

public enum RibbingPattern
{
    StandardSpiral = 1,    // Standard ASTM spiral ribbing
    HighBond = 2,         // Enhanced bond pattern
    Deformed = 3,         // Traditional deformed pattern
    Custom = 4            // Customer-specific pattern
}

public enum RollingStand
{
    RoughingStand1,       // Initial size reduction
    RoughingStand2,       // Secondary reduction
    IntermediateStand1,   // Intermediate sizing
    IntermediateStand2,   // Pre-finishing
    FinishingStand,       // Final sizing and ribbing
    RibbingStand         // Dedicated ribbing application
}

public class RebarRollingStatus
{
    public string MillId { get; set; } = string.Empty;
    public string MillName { get; set; } = string.Empty;
    public string OperationalStatus { get; set; } = "Idle"; // Idle, Active, Setup, Maintenance, Emergency
    
    // Current production
    public RebarGrade CurrentGrade { get; set; } = RebarGrade.Grade60;
    public RebarSize CurrentSize { get; set; } = RebarSize.Size5;
    public RibbingPattern CurrentPattern { get; set; } = RibbingPattern.StandardSpiral;
    public string CurrentHeatNumber { get; set; } = string.Empty;
    
    // Rolling parameters
    public double BilletTemperature { get; set; }      // °C
    public double RollingSpeed { get; set; }           // m/min
    public double TotalReduction { get; set; }         // % size reduction
    public double FinalDiameter { get; set; }          // mm
    
    // Equipment status per stand
    public List<RollingStandStatus> RollingStands { get; set; } = new();
    
    // Quality metrics
    public double DimensionalAccuracy { get; set; }    // % within tolerance
    public double SurfaceQuality { get; set; }         // 0-100 score
    public double RibbingQuality { get; set; }         // Rib height consistency %
    public double StraightnessQuality { get; set; }    // mm deviation
    
    // Production metrics
    public double ThroughputRate { get; set; }         // tons/hour
    public int BarsRolled { get; set; }                // Current shift
    public double TotalTonnage { get; set; }           // Current shift
    public double YieldRate { get; set; }              // % good product
    
    // Energy and efficiency
    public double PowerConsumption { get; set; }       // kW
    public double EnergyPerTon { get; set; }           // kWh/ton
    public double OverallEfficiency { get; set; }      // %
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class RollingStandStatus
{
    public RollingStand StandType { get; set; }
    public int StandNumber { get; set; }
    public double MotorTorque { get; set; }            // Nm
    public double MotorCurrent { get; set; }           // A
    public double RollGap { get; set; }                // mm
    public double RollPressure { get; set; }           // MPa
    public double RollTemperature { get; set; }        // °C
    public double VibrationLevel { get; set; }         // mm/s RMS
    public bool IsOperational { get; set; } = true;
    public string MaintenanceStatus { get; set; } = "Good";
}

public class RebarRollingTelemetry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MillId { get; set; } = string.Empty;
    public RollingStand StandPosition { get; set; }
    public string CurrentHeatNumber { get; set; } = string.Empty;
    
    // Current production parameters
    public RebarGrade Grade { get; set; }
    public RebarSize Size { get; set; }
    public RibbingPattern Pattern { get; set; }
    
    // Billet/bar measurements
    public double InletDiameter { get; set; }          // mm - before this stand
    public double OutletDiameter { get; set; }         // mm - after this stand
    public double BilletTemperature { get; set; }      // °C
    public double BarTemperature { get; set; }         // °C after rolling
    public double RollingSpeed { get; set; }           // m/min
    
    // Rolling forces and power
    public double MotorTorque { get; set; }            // Nm
    public double MotorCurrent { get; set; }           // A
    public double RollForce { get; set; }              // kN
    public double RollGap { get; set; }                // mm
    public double RollPressure { get; set; }           // MPa
    public double PowerConsumption { get; set; }       // kW
    
    // Mechanical measurements
    public double VibrationX { get; set; }             // mm/s RMS
    public double VibrationY { get; set; }             // mm/s RMS
    public double VibrationZ { get; set; }             // mm/s RMS
    public double RollWear { get; set; }               // mm diameter loss
    
    // Ribbing-specific measurements (for ribbing stands)
    public double RibHeight { get; set; }              // mm
    public double RibSpacing { get; set; }             // mm
    public double RibAngle { get; set; }               // degrees
    public double RibConsistency { get; set; }         // % uniformity
    public double SurfaceRoughness { get; set; }       // μm Ra
    
    // Quality measurements
    public double DimensionalTolerance { get; set; }   // mm deviation from target
    public double Ovality { get; set; }                // % roundness deviation
    public double Straightness { get; set; }           // mm/m deviation
    public double SurfaceDefects { get; set; }         // defects per meter
    
    // Process conditions
    public double CoolingWaterFlow { get; set; }       // L/min
    public double CoolingWaterTemp { get; set; }       // °C
    public double AmbientTemperature { get; set; }     // °C
    public double Humidity { get; set; }               // %
    
    // Performance indicators
    public double PassReduction { get; set; }          // % reduction this stand
    public double CumulativeReduction { get; set; }    // % total reduction
    public double RollingEfficiency { get; set; }      // %
    public double MaterialYield { get; set; }          // %
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class RebarRollingConfiguration
{
    public string MillId { get; set; } = string.Empty;
    public RebarGrade TargetGrade { get; set; }
    public RebarSize TargetSize { get; set; }
    public RibbingPattern RibbingPattern { get; set; }
    
    // Rolling schedule configuration
    public List<RollingPassConfig> RollingSchedule { get; set; } = new();
    
    // Target final dimensions
    public double TargetDiameter { get; set; }         // mm
    public double TargetRibHeight { get; set; }        // mm (typically 0.6-1.2mm)
    public double TargetRibSpacing { get; set; }       // mm
    public double TargetRibAngle { get; set; }         // degrees (typically 45-75°)
    
    // Process parameters
    public double BilletEntryTemp { get; set; } = 1050; // °C
    public double FinishingTemp { get; set; } = 950;    // °C
    public double RollingSpeed { get; set; } = 8.0;     // m/s
    public double TotalReduction { get; set; } = 85;    // % from billet to final
    
    // Quality tolerances
    public double DiameterTolerance { get; set; } = 0.5; // mm
    public double RibHeightTolerance { get; set; } = 0.1; // mm
    public double StraightnessTolerance { get; set; } = 5; // mm per meter
    public double OvalityLimit { get; set; } = 2.0;      // %
    
    // Equipment limits
    public double MaxRollForce { get; set; } = 5000;     // kN
    public double MaxTorque { get; set; } = 50000;       // Nm
    public double MaxMotorCurrent { get; set; } = 800;   // A
    public double MaxVibration { get; set; } = 10;       // mm/s RMS
    
    // Cooling configuration
    public bool CoolingEnabled { get; set; } = true;
    public double CoolingWaterFlow { get; set; } = 100; // L/min
    public double CoolingWaterTemp { get; set; } = 25;  // °C
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}

public class RollingPassConfig
{
    public RollingStand Stand { get; set; }
    public int PassNumber { get; set; }
    public double EntryDiameter { get; set; }    // mm
    public double ExitDiameter { get; set; }     // mm
    public double ReductionRatio { get; set; }   // %
    public double RollGap { get; set; }          // mm
    public double DraftAngle { get; set; }       // degrees
    public double RollingForce { get; set; }     // kN estimated
    public bool IsRibbingPass { get; set; }      // true for ribbing stands
}

public class RebarRollingPerformanceMetrics
{
    public string MillId { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; }
    public TimeSpan CalculationPeriod { get; set; }
    
    // Production metrics
    public double TotalTonnage { get; set; }            // tons produced
    public int BarsRolled { get; set; }                 // number of bars
    public double ThroughputRate { get; set; }          // tons/hour
    public double AverageBarWeight { get; set; }        // kg/bar
    public double ProductionEfficiency { get; set; }    // % of target
    
    // Quality metrics
    public double OverallQualityScore { get; set; }     // 0-100
    public double DimensionalAccuracy { get; set; }     // % within tolerance
    public double RibbingQuality { get; set; }          // % rib consistency
    public double SurfaceQuality { get; set; }          // 0-100 score
    public double YieldRate { get; set; }               // % good product
    
    // Grade-specific production
    public Dictionary<RebarGrade, double> TonnageByGrade { get; set; } = new();
    public Dictionary<RebarSize, int> BarsBySize { get; set; } = new();
    public Dictionary<RibbingPattern, double> ProductionByPattern { get; set; } = new();
    
    // Energy and efficiency
    public double TotalEnergyConsumption { get; set; }  // kWh
    public double EnergyPerTon { get; set; }            // kWh/ton
    public double PeakPowerDemand { get; set; }         // kW
    public double PowerEfficiency { get; set; }         // %
    
    // Equipment performance
    public double EquipmentUtilization { get; set; }    // % uptime
    public double MaintenanceTime { get; set; }         // hours
    public double AvgVibrationLevel { get; set; }       // mm/s RMS
    public double RollWearRate { get; set; }            // mm/1000 tons
    
    // Quality defects analysis
    public int DimensionalDefects { get; set; }
    public int SurfaceDefects { get; set; }
    public int RibbingDefects { get; set; }
    public int StraightnessDefects { get; set; }
    public double DefectRate { get; set; }              // defects per 1000 bars
    
    // Process stability
    public double TemperatureVariance { get; set; }     // °C std dev
    public double SpeedVariance { get; set; }           // m/min std dev
    public double ForceVariance { get; set; }           // kN std dev
    public double ProcessStability { get; set; }        // % consistency
}

public class RebarRollingAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MillId { get; set; } = string.Empty;
    public RollingStand? AffectedStand { get; set; }
    public string? CurrentHeatNumber { get; set; }
    public RebarGrade? AffectedGrade { get; set; }
    public RebarSize? AffectedSize { get; set; }
    
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical
    public string Description { get; set; } = string.Empty;
    
    // Process measurements
    public double? VibrationReading { get; set; }       // mm/s RMS
    public double? TemperatureReading { get; set; }     // °C
    public double? ForceReading { get; set; }           // kN
    public double? DimensionReading { get; set; }       // mm
    public double? QualityScore { get; set; }           // 0-100
    
    // Equipment status
    public bool? MotorOperational { get; set; }
    public bool? CoolingSystemOperational { get; set; }
    public bool? GuideOperational { get; set; }
    
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionAction { get; set; }
    public string? OperatorId { get; set; }
    
    public static class AlertTypes
    {
        public const string HighVibration = "HIGH_VIBRATION";
        public const string OverTemperature = "OVER_TEMPERATURE";
        public const string ExcessiveForce = "EXCESSIVE_FORCE";
        public const string DimensionalDeviation = "DIMENSIONAL_DEVIATION";
        public const string RibbingQualityIssue = "RIBBING_QUALITY_ISSUE";
        public const string MotorOverload = "MOTOR_OVERLOAD";
        public const string CoolingSystemFailure = "COOLING_SYSTEM_FAILURE";
        public const string RollWearExcessive = "ROLL_WEAR_EXCESSIVE";
        public const string SurfaceDefects = "SURFACE_DEFECTS";
        public const string StraightnessIssue = "STRAIGHTNESS_ISSUE";
        public const string ProductionRateBelow = "PRODUCTION_RATE_BELOW";
        public const string QualityScoreBelow = "QUALITY_SCORE_BELOW";
    }
    
    public string GetRecommendedAction()
    {
        return AlertType switch
        {
            AlertTypes.HighVibration => "Stop production, inspect bearings and roll alignment, check foundations",
            AlertTypes.OverTemperature => "Increase cooling water flow, check billet temperature, reduce rolling speed",
            AlertTypes.ExcessiveForce => "Check roll gap setting, verify material properties, inspect roll condition",
            AlertTypes.DimensionalDeviation => "Adjust roll gap, check roll wear, verify pass design",
            AlertTypes.RibbingQualityIssue => "Inspect ribbing rolls, check rib depth setting, verify pattern alignment",
            AlertTypes.MotorOverload => "Reduce rolling force, check lubrication, inspect motor connections",
            AlertTypes.CoolingSystemFailure => "Check water supply, inspect nozzles, verify flow rates",
            AlertTypes.RollWearExcessive => "Schedule roll change, inspect roll hardness, check lubrication",
            AlertTypes.SurfaceDefects => "Clean rolls, check cooling, inspect billet surface quality",
            AlertTypes.StraightnessIssue => "Adjust guide alignment, check roll parallelism, verify pass geometry",
            AlertTypes.ProductionRateBelow => "Optimize rolling speed, check equipment status, review pass schedule",
            AlertTypes.QualityScoreBelow => "Review all quality parameters, increase inspection frequency",
            _ => "Contact rolling mill supervisor for detailed analysis"
        };
    }
}

// Ribbing specification for different rebar patterns
public class RibbingSpecification
{
    public RibbingPattern Pattern { get; set; }
    public RebarSize Size { get; set; }
    public double NominalRibHeight { get; set; }        // mm
    public double RibHeightTolerance { get; set; }      // ±mm
    public double RibSpacing { get; set; }              // mm
    public double RibAngle { get; set; }                // degrees from bar axis
    public double RibArea { get; set; }                 // mm² per unit length
    public double RelativeRibArea { get; set; }         // % of nominal cross-section
    public string ASTMCompliance { get; set; } = "A615"; // ASTM standard
    
    public static RibbingSpecification GetStandardSpec(RebarSize size, RibbingPattern pattern)
    {
        var diameter = (double)size;
        
        // Standard ASTM A615 ribbing specifications
        var ribHeight = pattern switch
        {
            RibbingPattern.StandardSpiral => diameter * 0.043, // 4.3% of diameter
            RibbingPattern.HighBond => diameter * 0.055,       // 5.5% of diameter
            RibbingPattern.Deformed => diameter * 0.040,       // 4.0% of diameter
            _ => diameter * 0.043
        };
        
        var ribSpacing = pattern switch
        {
            RibbingPattern.StandardSpiral => diameter * 0.7,   // 70% of diameter
            RibbingPattern.HighBond => diameter * 0.6,         // 60% of diameter  
            RibbingPattern.Deformed => diameter * 0.8,         // 80% of diameter
            _ => diameter * 0.7
        };
        
        return new RibbingSpecification
        {
            Pattern = pattern,
            Size = size,
            NominalRibHeight = ribHeight,
            RibHeightTolerance = ribHeight * 0.15, // ±15%
            RibSpacing = ribSpacing,
            RibAngle = 60.0, // Standard spiral angle
            RelativeRibArea = pattern == RibbingPattern.HighBond ? 0.75 : 0.60,
            ASTMCompliance = "A615"
        };
    }
}

// Legacy compatibility - keep original models for backward compatibility
public class RollingMillStatus : RebarRollingStatus { }
public class RollingMillTelemetry : RebarRollingTelemetry { }
public class RollingConfiguration : RebarRollingConfiguration { }
public class PerformanceMetrics : RebarRollingPerformanceMetrics { }
public class MaintenanceAlert : RebarRollingAlert { }