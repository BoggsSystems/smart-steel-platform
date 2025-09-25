using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CuttingStraighteningService.Models;

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

public enum RebarLength
{
    Length20ft = 20,  // 20 feet (6.1m)
    Length30ft = 30,  // 30 feet (9.1m)
    Length40ft = 40,  // 40 feet (12.2m)
    Length60ft = 60   // 60 feet (18.3m)
}

public enum ProcessingStatus
{
    InQueue,              // Waiting for processing
    Straightening,        // In straightening process
    Cutting,              // In cutting process
    QualityCheck,         // Under quality inspection
    Bundling,             // Ready for bundling
    Completed,            // Processing complete
    Rejected,             // Failed quality standards
    Rework                // Requires reprocessing
}

public enum CuttingMethod
{
    FlyingShear,          // High-speed flying shear
    StationaryShear,      // Stationary guillotine shear
    Sawing,               // Abrasive or cold saw cutting
    TorchCutting,         // Plasma or oxy-fuel cutting
    LaserCutting          // Precision laser cutting (specialty)
}

public enum StraighteningMethod
{
    RollerStraightener,   // Multi-roller straightening
    RotaryStraightener,   // Rotary straightening machine
    StrechStraightening,  // Stretch straightening
    HydraulicStraightening, // Hydraulic press straightening
    HeatStraightening     // Heat-assisted straightening
}

public class RebarBar
{
    public string BarId { get; set; } = Guid.NewGuid().ToString();
    public string HeatNumber { get; set; } = string.Empty;
    public string CustomerOrderId { get; set; } = string.Empty;
    
    // Bar specifications
    public RebarGrade Grade { get; set; }
    public RebarSize Size { get; set; }
    public RebarLength TargetLength { get; set; }
    public double NominalDiameter { get; set; }               // mm
    public double ActualLength { get; set; }                  // m
    public double Weight { get; set; }                        // kg
    
    // Processing status
    public ProcessingStatus Status { get; set; } = ProcessingStatus.InQueue;
    public DateTime ProcessingStarted { get; set; }
    public DateTime? ProcessingCompleted { get; set; }
    public string ProcessingLineId { get; set; } = string.Empty;
    
    // Source information
    public string SourceBilletId { get; set; } = string.Empty;
    public string RollingMillId { get; set; } = string.Empty;
    public string HeatTreatmentBatchId { get; set; } = string.Empty;
    public DateTime ProducedAt { get; set; }
    
    // Quality measurements (pre-processing)
    public double InitialStraightness { get; set; }           // mm deviation per meter
    public double InitialLength { get; set; }                 // m
    public double InitialWeight { get; set; }                 // kg
    public bool HasSurfaceDefects { get; set; }
    public List<string> SurfaceDefects { get; set; } = new();
    
    // Quality measurements (post-processing)
    public double FinalStraightness { get; set; }             // mm deviation per meter
    public double FinalLength { get; set; }                   // m
    public double FinalWeight { get; set; }                   // kg
    public double LengthTolerance { get; set; }               // mm deviation from target
    public bool PassedQualityCheck { get; set; }
    
    // Processing parameters used
    public StraighteningMethod? StraighteningMethod { get; set; }
    public CuttingMethod? CuttingMethod { get; set; }
    public double StraighteningForce { get; set; }            // kN
    public int StraighteningPasses { get; set; }
    public double CuttingSpeed { get; set; }                  // m/min
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CuttingStraighteningLineStatus
{
    public string LineId { get; set; } = string.Empty;
    public string LineName { get; set; } = string.Empty;
    public string OperationalStatus { get; set; } = "Idle"; // Idle, Active, Setup, Maintenance, Emergency
    
    // Current production
    public RebarGrade? CurrentGrade { get; set; }
    public RebarSize? CurrentSize { get; set; }
    public RebarLength? CurrentLength { get; set; }
    public string CurrentHeatNumber { get; set; } = string.Empty;
    public string CurrentOrderId { get; set; } = string.Empty;
    
    // Line configuration
    public StraighteningMethod StraighteningMethod { get; set; } = StraighteningMethod.RollerStraightener;
    public CuttingMethod CuttingMethod { get; set; } = CuttingMethod.FlyingShear;
    public int NumberOfStraighteningRollers { get; set; } = 9;
    public double MaxProcessingSpeed { get; set; } = 120.0;   // m/min
    
    // Current processing parameters
    public double ProcessingSpeed { get; set; }               // m/min
    public double StraighteningForce { get; set; }            // kN
    public double RollerPressure { get; set; }                // MPa
    public double CuttingForce { get; set; }                  // kN
    public double MeasuredLength { get; set; }                // m (last bar processed)
    
    // Quality metrics
    public double StraightnessAccuracy { get; set; }          // % within tolerance
    public double LengthAccuracy { get; set; }                // % within tolerance
    public double SurfaceQuality { get; set; }                // 0-100 score
    public double OverallQualityScore { get; set; }           // 0-100
    
    // Production metrics
    public double ThroughputRate { get; set; }                // bars per hour
    public int BarsProcessed { get; set; }                    // current shift
    public double TotalLength { get; set; }                   // meters processed
    public double YieldRate { get; set; }                     // % good bars
    
    // Equipment status
    public bool StraighteningRollersOperational { get; set; } = true;
    public bool CuttingShearOperational { get; set; } = true;
    public bool LengthMeasurementOperational { get; set; } = true;
    public bool ConveyorSystemOperational { get; set; } = true;
    public bool QualityInspectionOperational { get; set; } = true;
    
    // Energy and efficiency
    public double PowerConsumption { get; set; }              // kW
    public double EnergyPerBar { get; set; }                  // kWh/bar
    public double HydraulicPressure { get; set; }             // bar
    public double OverallEfficiency { get; set; }             // %
    
    // Queue status
    public int BarsInQueue { get; set; }
    public TimeSpan EstimatedQueueTime { get; set; }
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class CuttingStraighteningTelemetry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string LineId { get; set; } = string.Empty;
    public string? CurrentBarId { get; set; }
    
    // Current bar being processed
    public RebarGrade? Grade { get; set; }
    public RebarSize? Size { get; set; }
    public RebarLength? TargetLength { get; set; }
    public string CurrentHeatNumber { get; set; } = string.Empty;
    
    // Processing parameters
    public double ProcessingSpeed { get; set; }               // m/min
    public StraighteningMethod StraighteningMethod { get; set; }
    public CuttingMethod CuttingMethod { get; set; }
    
    // Straightening measurements
    public double StraighteningForce { get; set; }            // kN
    public List<double> RollerForces { get; set; } = new();   // Individual roller forces
    public List<double> RollerPositions { get; set; } = new(); // Roller positions (mm)
    public double RollerPressure { get; set; }                // MPa
    public int StraighteningPasses { get; set; }
    public double InitialBow { get; set; }                     // mm initial deviation
    public double FinalBow { get; set; }                      // mm final deviation
    public double StraighteningEfficiency { get; set; }       // % improvement
    
    // Cutting measurements
    public double CuttingForce { get; set; }                  // kN
    public double CuttingSpeed { get; set; }                  // m/min
    public double BladePosition { get; set; }                 // mm
    public double CutLength { get; set; }                     // m
    public double LengthDeviation { get; set; }               // mm from target
    public double CutQuality { get; set; }                    // 0-100 score
    
    // Length measurement
    public double MeasuredLength { get; set; }                // m
    public double TargetLengthValue { get; set; }             // m
    public double LengthTolerance { get; set; }               // mm
    public bool LengthWithinTolerance { get; set; }
    
    // Quality measurements
    public double StraightnessDeviation { get; set; }         // mm per meter
    public double DiameterMeasurement { get; set; }           // mm
    public double WeightMeasurement { get; set; }             // kg
    public int SurfaceDefectCount { get; set; }
    public double SurfaceQualityScore { get; set; }           // 0-100
    public bool QualityCheckPassed { get; set; }
    
    // Environmental conditions
    public double AmbientTemperature { get; set; }            // °C
    public double Humidity { get; set; }                      // %
    public double BarTemperature { get; set; }                // °C
    
    // Equipment performance
    public double MotorCurrent { get; set; }                  // A
    public double HydraulicPressure { get; set; }             // bar
    public List<double> VibrationLevels { get; set; } = new(); // mm/s RMS per station
    public double PowerConsumption { get; set; }              // kW
    public double EnergyPerBar { get; set; }                  // kWh
    
    // Process efficiency
    public double CycleTime { get; set; }                     // seconds per bar
    public double ProcessingEfficiency { get; set; }          // %
    public double MaterialYield { get; set; }                 // %
    public double OverallEquipmentEffectiveness { get; set; } // %
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class CuttingStraighteningConfiguration
{
    public string LineId { get; set; } = string.Empty;
    public RebarGrade TargetGrade { get; set; }
    public RebarSize TargetSize { get; set; }
    public RebarLength TargetLength { get; set; }
    
    // Straightening configuration
    public StraighteningMethod StraighteningMethod { get; set; } = StraighteningMethod.RollerStraightener;
    public double MaxStraighteningForce { get; set; } = 1000.0; // kN
    public double StraightnessTolerance { get; set; } = 5.0;    // mm per meter
    public int MaxStraighteningPasses { get; set; } = 3;
    public List<double> RollerPositions { get; set; } = new();  // mm offset positions
    
    // Cutting configuration
    public CuttingMethod CuttingMethod { get; set; } = CuttingMethod.FlyingShear;
    public double TargetLengthValue { get; set; }              // m
    public double LengthTolerance { get; set; } = 25.0;        // mm
    public double MaxCuttingSpeed { get; set; } = 150.0;       // m/min
    public double CuttingForceLimit { get; set; } = 500.0;     // kN
    
    // Process parameters
    public double ProcessingSpeed { get; set; } = 60.0;        // m/min
    public bool AutomaticQualityCheck { get; set; } = true;
    public bool ContinuousProcessing { get; set; } = true;
    public double MinProcessingSpeed { get; set; } = 10.0;     // m/min
    public double MaxProcessingSpeed { get; set; } = 120.0;    // m/min
    
    // Quality thresholds
    public double MinStraightnessScore { get; set; } = 90.0;   // %
    public double MinLengthAccuracy { get; set; } = 95.0;      // %
    public double MinSurfaceQuality { get; set; } = 85.0;      // score
    public double MinOverallQuality { get; set; } = 90.0;      // %
    
    // Equipment limits
    public double MaxMotorCurrent { get; set; } = 200.0;       // A
    public double MaxHydraulicPressure { get; set; } = 350.0;  // bar
    public double MaxVibration { get; set; } = 15.0;           // mm/s RMS
    public double MaxPowerConsumption { get; set; } = 500.0;   // kW
    
    // Maintenance parameters
    public int BarsBeforeBladeMaintenance { get; set; } = 10000;
    public int BarsBeforeRollerMaintenance { get; set; } = 50000;
    public TimeSpan MaintenanceCheckInterval { get; set; } = TimeSpan.FromHours(8);
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}

public class CuttingStraighteningPerformanceMetrics
{
    public string LineId { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; }
    public TimeSpan CalculationPeriod { get; set; }
    
    // Production metrics
    public int TotalBarsProcessed { get; set; }
    public double TotalLength { get; set; }                    // meters
    public double TotalWeight { get; set; }                    // kg
    public double AverageThroughputRate { get; set; }          // bars/hour
    public double AverageProcessingSpeed { get; set; }         // m/min
    
    // Quality metrics
    public double OverallQualityScore { get; set; }            // 0-100
    public double StraightnessAccuracy { get; set; }           // % within tolerance
    public double LengthAccuracy { get; set; }                 // % within tolerance
    public double SurfaceQualityScore { get; set; }            // 0-100
    public double YieldRate { get; set; }                      // % good bars
    
    // Grade and size distribution
    public Dictionary<RebarGrade, int> BarsByGrade { get; set; } = new();
    public Dictionary<RebarSize, int> BarsBySize { get; set; } = new();
    public Dictionary<RebarLength, int> BarsByLength { get; set; } = new();
    
    // Efficiency metrics
    public double LineUtilization { get; set; }                // %
    public double EquipmentEffectiveness { get; set; }         // %
    public double EnergyEfficiency { get; set; }               // kWh/ton
    public double MaterialUtilization { get; set; }            // %
    public double CycleTimeEfficiency { get; set; }            // %
    
    // Equipment performance
    public double StraighteningEfficiency { get; set; }        // % bow reduction
    public double CuttingAccuracy { get; set; }                // % within tolerance
    public double EquipmentUptime { get; set; }                // %
    public double MaintenanceTime { get; set; }                // hours
    public double MeanTimeBetweenFailures { get; set; }        // hours
    
    // Defect analysis
    public int StraightnessDefects { get; set; }
    public int LengthDefects { get; set; }
    public int SurfaceDefects { get; set; }
    public int CuttingDefects { get; set; }
    public double DefectRate { get; set; }                     // defects per 1000 bars
    
    // Energy and cost metrics
    public double TotalEnergyConsumption { get; set; }         // kWh
    public double EnergyPerBar { get; set; }                   // kWh/bar
    public double EnergyPerTon { get; set; }                   // kWh/ton
    public double ProcessingCostPerBar { get; set; }           // $
    
    // Process stability
    public double StraighteningForceVariance { get; set; }     // kN std dev
    public double CuttingForceVariance { get; set; }           // kN std dev
    public double SpeedVariance { get; set; }                  // m/min std dev
    public double ProcessConsistency { get; set; }             // % stability
}

public class CuttingStraighteningAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string LineId { get; set; } = string.Empty;
    public string? BarId { get; set; }
    public RebarGrade? AffectedGrade { get; set; }
    public RebarSize? AffectedSize { get; set; }
    
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical
    public string Description { get; set; } = string.Empty;
    
    // Process measurements
    public double? StraighteningForceReading { get; set; }     // kN
    public double? CuttingForceReading { get; set; }           // kN
    public double? LengthDeviationReading { get; set; }        // mm
    public double? StraightnessReading { get; set; }           // mm/m
    public double? QualityScore { get; set; }                  // 0-100
    
    // Equipment status
    public bool? RollersOperational { get; set; }
    public bool? ShearOperational { get; set; }
    public bool? MeasurementSystemOperational { get; set; }
    public bool? ConveyorOperational { get; set; }
    
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionAction { get; set; }
    public string? OperatorId { get; set; }
    
    public static class AlertTypes
    {
        public const string ExcessiveStraighteningForce = "EXCESSIVE_STRAIGHTENING_FORCE";
        public const string StraightnessOutOfTolerance = "STRAIGHTNESS_OUT_OF_TOLERANCE";
        public const string LengthOutOfTolerance = "LENGTH_OUT_OF_TOLERANCE";
        public const string CuttingForceExceeded = "CUTTING_FORCE_EXCEEDED";
        public const string SurfaceQualityPoor = "SURFACE_QUALITY_POOR";
        public const string RollerWearExcessive = "ROLLER_WEAR_EXCESSIVE";
        public const string BladeWearExcessive = "BLADE_WEAR_EXCESSIVE";
        public const string VibrationHigh = "VIBRATION_HIGH";
        public const string ProcessingSpeedLow = "PROCESSING_SPEED_LOW";
        public const string QualityCheckFailure = "QUALITY_CHECK_FAILURE";
        public const string MeasurementSystemError = "MEASUREMENT_SYSTEM_ERROR";
        public const string ConveyorMalfunction = "CONVEYOR_MALFUNCTION";
        public const string HydraulicPressureLoss = "HYDRAULIC_PRESSURE_LOSS";
        public const string MotorOverload = "MOTOR_OVERLOAD";
        public const string EmergencyStop = "EMERGENCY_STOP";
    }
    
    public string GetRecommendedAction()
    {
        return AlertType switch
        {
            AlertTypes.ExcessiveStraighteningForce => "Reduce roller pressure, check material hardness, inspect for work hardening",
            AlertTypes.StraightnessOutOfTolerance => "Adjust roller positions, increase straightening passes, check for material defects",
            AlertTypes.LengthOutOfTolerance => "Calibrate length measurement system, adjust cutting position, check feed speed",
            AlertTypes.CuttingForceExceeded => "Check blade sharpness, reduce cutting speed, verify material properties",
            AlertTypes.SurfaceQualityPoor => "Clean rollers, check surface condition, adjust processing parameters",
            AlertTypes.RollerWearExcessive => "Schedule roller replacement, check alignment, verify maintenance schedule",
            AlertTypes.BladeWearExcessive => "Replace cutting blade, check cutting parameters, review material hardness",
            AlertTypes.VibrationHigh => "Check equipment alignment, inspect bearings, verify foundation integrity",
            AlertTypes.ProcessingSpeedLow => "Check for material buildup, verify hydraulic pressure, inspect drive system",
            AlertTypes.QualityCheckFailure => "Stop processing, inspect bars manually, calibrate measurement equipment",
            AlertTypes.MeasurementSystemError => "Calibrate measurement sensors, check connections, verify reference standards",
            AlertTypes.ConveyorMalfunction => "Inspect conveyor belt, check drive motors, verify safety systems",
            AlertTypes.HydraulicPressureLoss => "Check hydraulic system, inspect for leaks, verify pump operation",
            AlertTypes.MotorOverload => "Reduce processing load, check motor connections, inspect mechanical systems",
            AlertTypes.EmergencyStop => "Investigate safety system activation, check all interlocks, clear work area",
            _ => "Contact cutting/straightening supervisor for detailed analysis"
        };
    }
}

// Process specification for different grades and sizes
public class ProcessingSpecification
{
    public RebarGrade Grade { get; set; }
    public RebarSize Size { get; set; }
    public RebarLength Length { get; set; }
    
    // Straightening parameters
    public StraighteningMethod RecommendedStraighteningMethod { get; set; }
    public double MaxStraighteningForce { get; set; }           // kN
    public double StraightnessTolerance { get; set; }          // mm per meter
    public List<double> RollerSequence { get; set; } = new();  // Roller positions in mm
    
    // Cutting parameters
    public CuttingMethod RecommendedCuttingMethod { get; set; }
    public double OptimalCuttingSpeed { get; set; }             // m/min
    public double LengthTolerance { get; set; }                 // mm
    public double CuttingForceLimit { get; set; }               // kN
    
    // Processing parameters
    public double OptimalProcessingSpeed { get; set; }          // m/min
    public double MinProcessingSpeed { get; set; }              // m/min
    public double MaxProcessingSpeed { get; set; }              // m/min
    
    // Quality requirements
    public double MinStraightnessAccuracy { get; set; }         // %
    public double MinLengthAccuracy { get; set; }               // %
    public double MinSurfaceQuality { get; set; }               // score
    
    public static ProcessingSpecification GetStandardSpec(RebarGrade grade, RebarSize size, RebarLength length)
    {
        var diameter = (double)size;
        var lengthMeters = (double)length * 0.3048; // Convert feet to meters
        
        var spec = new ProcessingSpecification
        {
            Grade = grade,
            Size = size,
            Length = length
        };

        // Straightening method based on size
        spec.RecommendedStraighteningMethod = size switch
        {
            RebarSize.Size3 or RebarSize.Size4 => StraighteningMethod.RollerStraightener,
            RebarSize.Size5 or RebarSize.Size6 or RebarSize.Size7 => StraighteningMethod.RollerStraightener,
            RebarSize.Size8 or RebarSize.Size9 or RebarSize.Size10 => StraighteningMethod.RotaryStraightener,
            RebarSize.Size11 or RebarSize.Size14 => StraighteningMethod.HydraulicStraightening,
            RebarSize.Size18 => StraighteningMethod.HydraulicStraightening,
            _ => StraighteningMethod.RollerStraightener
        };

        // Straightening force based on size and grade
        var baseForce = Math.Pow(diameter, 1.5) * 2.0; // Base force calculation
        var gradeMultiplier = grade switch
        {
            RebarGrade.Grade40 => 0.8,
            RebarGrade.Grade60 => 1.0,
            RebarGrade.Grade75 => 1.2,
            RebarGrade.Grade80 => 1.3,
            _ => 1.0
        };
        spec.MaxStraighteningForce = baseForce * gradeMultiplier;

        // Cutting method based on size
        spec.RecommendedCuttingMethod = size switch
        {
            <= RebarSize.Size8 => CuttingMethod.FlyingShear,
            <= RebarSize.Size11 => CuttingMethod.StationaryShear,
            _ => CuttingMethod.Sawing
        };

        // Processing speeds
        spec.OptimalProcessingSpeed = diameter <= 25 ? 80.0 : 60.0; // m/min
        spec.MinProcessingSpeed = 15.0;
        spec.MaxProcessingSpeed = 120.0;
        spec.OptimalCuttingSpeed = spec.OptimalProcessingSpeed * 1.1; // Slightly faster

        // Tolerances
        spec.StraightnessTolerance = 5.0; // mm per meter
        spec.LengthTolerance = length switch
        {
            RebarLength.Length20ft => 13.0, // ±0.5 inch
            RebarLength.Length30ft => 19.0, // ±0.75 inch
            RebarLength.Length40ft => 25.0, // ±1.0 inch
            RebarLength.Length60ft => 38.0, // ±1.5 inch
            _ => 25.0
        };

        // Quality requirements
        spec.MinStraightnessAccuracy = 95.0; // %
        spec.MinLengthAccuracy = 98.0; // %
        spec.MinSurfaceQuality = 90.0; // score

        // Cutting force limit
        spec.CuttingForceLimit = Math.PI * Math.Pow(diameter / 2, 2) * 500 / 1000; // Based on cross-sectional area and material strength

        return spec;
    }
}