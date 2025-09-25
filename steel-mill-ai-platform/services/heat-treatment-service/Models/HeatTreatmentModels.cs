using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HeatTreatmentService.Models;

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

public enum HeatTreatmentType
{
    Quenching,           // Rapid cooling for hardness
    Tempering,           // Reheating after quench for toughness
    QuenchAndTemper,     // Combined process
    Normalization,       // Air cooling for grain refinement
    Annealing,           // Slow cooling for stress relief
    AustempAtmosphering  // Isothermal treatment for specific properties
}

public enum TreatmentStatus
{
    Queued,              // Waiting for treatment
    Heating,             // Temperature ramping up
    Soaking,             // Holding at target temperature
    Quenching,           // Rapid cooling phase
    Tempering,           // Tempering heat treatment
    AirCooling,          // Controlled air cooling
    Completed,           // Treatment cycle complete
    QualityCheck,        // Under mechanical testing
    Approved,            // Quality approved
    Rejected             // Failed quality requirements
}

public enum CoolingMedium
{
    Water,               // Standard water quench
    Oil,                 // Oil quench for larger sections
    PolymerSolution,     // Controlled cooling rate
    Air,                 // Air cooling/normalization
    Salt,                // Isothermal quenching
    CryogenicNitrogen    // Sub-zero treatment
}

public class RebarBatch
{
    public string BatchId { get; set; } = Guid.NewGuid().ToString();
    public string HeatNumber { get; set; } = string.Empty;
    public RebarGrade Grade { get; set; }
    public RebarSize Size { get; set; }
    public int BarCount { get; set; }
    public double TotalWeight { get; set; }        // kg
    public double AverageLength { get; set; }      // m
    
    // Source tracking
    public string RollingMillId { get; set; } = string.Empty;
    public DateTime RollingCompleted { get; set; }
    public string CustomerOrderId { get; set; } = string.Empty;
    
    // Heat treatment specification
    public HeatTreatmentType TreatmentType { get; set; }
    public TreatmentStatus Status { get; set; } = TreatmentStatus.Queued;
    public DateTime? TreatmentStarted { get; set; }
    public DateTime? TreatmentCompleted { get; set; }
    public string TreatmentFurnaceId { get; set; } = string.Empty;
    
    // Target mechanical properties
    public double TargetYieldStrength { get; set; }    // MPa
    public double TargetTensileStrength { get; set; }   // MPa
    public double TargetElongation { get; set; }        // %
    public double TargetHardness { get; set; }          // HRC/HRB
    
    // Actual achieved properties (post-treatment)
    public double? ActualYieldStrength { get; set; }
    public double? ActualTensileStrength { get; set; }
    public double? ActualElongation { get; set; }
    public double? ActualHardness { get; set; }
    
    // Quality verification
    public bool PassedMechanicalTesting { get; set; }
    public List<string> QualityIssues { get; set; } = new();
    public string QualityInspectorId { get; set; } = string.Empty;
    public DateTime? QualityCheckedAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class HeatTreatmentFurnaceStatus
{
    public string FurnaceId { get; set; } = string.Empty;
    public string FurnaceName { get; set; } = string.Empty;
    public string OperationalStatus { get; set; } = "Idle"; // Idle, Heating, Cooling, Maintenance, Emergency
    
    // Current batch information
    public string? CurrentBatchId { get; set; }
    public RebarGrade? CurrentGrade { get; set; }
    public RebarSize? CurrentSize { get; set; }
    public HeatTreatmentType? CurrentTreatment { get; set; }
    
    // Temperature control
    public double CurrentTemperature { get; set; }     // °C
    public double TargetTemperature { get; set; }      // °C
    public double HeatingRate { get; set; }            // °C/min
    public double CoolingRate { get; set; }            // °C/min
    public bool TemperatureStable { get; set; }
    
    // Furnace atmosphere control
    public double OxygenLevel { get; set; }            // %
    public double CarbonPotential { get; set; }        // %
    public double NitrogenFlow { get; set; }           // L/min
    public bool AtmosphereControlled { get; set; }
    
    // Quench system status
    public CoolingMedium QuenchMedium { get; set; } = CoolingMedium.Water;
    public double QuenchTemperature { get; set; }      // °C
    public double QuenchFlowRate { get; set; }         // L/min
    public bool QuenchSystemReady { get; set; } = true;
    
    // Equipment status
    public bool HeatingElementsOperational { get; set; } = true;
    public bool FansOperational { get; set; } = true;
    public bool ThermocoupleFunctional { get; set; } = true;
    public bool SafetySystemsActive { get; set; } = true;
    
    // Capacity and loading
    public double MaxCapacity { get; set; } = 5000;    // kg
    public double CurrentLoad { get; set; }            // kg
    public double LoadUtilization { get; set; }        // %
    public int BarsInFurnace { get; set; }
    
    // Energy and efficiency
    public double PowerConsumption { get; set; }       // kW
    public double EnergyPerTon { get; set; }           // kWh/ton
    public double ThermalEfficiency { get; set; }      // %
    
    // Cycle timing
    public DateTime? CycleStartTime { get; set; }
    public TimeSpan? EstimatedCycleTime { get; set; }
    public TimeSpan? RemainingTime { get; set; }
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class HeatTreatmentTelemetry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FurnaceId { get; set; } = string.Empty;
    public string? CurrentBatchId { get; set; }
    
    // Current treatment phase
    public HeatTreatmentType TreatmentType { get; set; }
    public TreatmentStatus TreatmentPhase { get; set; }
    public RebarGrade? Grade { get; set; }
    public RebarSize? Size { get; set; }
    
    // Temperature measurements
    public double FurnaceTemperature { get; set; }     // °C
    public double LoadTemperature { get; set; }        // °C (measured on rebar)
    public double QuenchTemperature { get; set; }      // °C
    public double AmbientTemperature { get; set; }     // °C
    public List<double> ZoneTemperatures { get; set; } = new(); // Multi-zone furnaces
    
    // Temperature control
    public double TargetTemperature { get; set; }      // °C
    public double TemperatureDeviation { get; set; }   // °C from target
    public double HeatingRate { get; set; }            // °C/min
    public double CoolingRate { get; set; }            // °C/min
    public double TemperatureUniformity { get; set; }  // °C variation across load
    
    // Atmosphere control
    public double OxygenLevel { get; set; }            // %
    public double CarbonMonoxideLevel { get; set; }    // ppm
    public double CarbonPotential { get; set; }        // %
    public double NitrogenFlow { get; set; }           // L/min
    public double AtmospherePressure { get; set; }     // Pa
    
    // Energy and power
    public double PowerConsumption { get; set; }       // kW
    public double EnergyConsumption { get; set; }      // kWh cumulative
    public double ThermalEfficiency { get; set; }      // %
    public double FuelConsumption { get; set; }        // L/h or m³/h
    
    // Mechanical systems
    public double FanSpeed { get; set; }               // RPM
    public double QuenchPumpPressure { get; set; }     // bar
    public double QuenchFlowRate { get; set; }         // L/min
    public bool ConveyorOperational { get; set; } = true;
    
    // Process timing
    public TimeSpan ElapsedTime { get; set; }          // Time in current phase
    public TimeSpan TotalCycleTime { get; set; }       // Total time since start
    public TimeSpan RemainingTime { get; set; }        // Estimated remaining time
    
    // Quality indicators
    public double CoolingRate { get; set; }            // °C/s during quench
    public double SoakTimeCompleted { get; set; }      // minutes at target temp
    public double TemperatureOvershoot { get; set; }   // °C maximum overshoot
    public double ProcessDeviation { get; set; }       // % from ideal process
    
    // Safety and alarms
    public bool OverTemperatureAlarm { get; set; }
    public bool UnderTemperatureAlarm { get; set; }
    public bool AtmosphereAlarm { get; set; }
    public bool QuenchSystemAlarm { get; set; }
    public bool EmergencyStop { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class HeatTreatmentConfiguration
{
    public string FurnaceId { get; set; } = string.Empty;
    public RebarGrade TargetGrade { get; set; }
    public RebarSize TargetSize { get; set; }
    public HeatTreatmentType TreatmentType { get; set; }
    
    // Temperature profile
    public List<TemperatureStep> TemperatureProfile { get; set; } = new();
    public double MaxHeatingRate { get; set; } = 5.0;          // °C/min
    public double MaxCoolingRate { get; set; } = 50.0;         // °C/min for quench
    public double TemperatureTolerance { get; set; } = 10.0;   // ±°C
    
    // Atmosphere control
    public bool ControlledAtmosphere { get; set; } = true;
    public double TargetCarbonPotential { get; set; } = 0.8;   // %
    public double NitrogenFlowRate { get; set; } = 50.0;       // L/min
    public double MaxOxygenLevel { get; set; } = 0.1;          // %
    
    // Quenching parameters
    public CoolingMedium QuenchMedium { get; set; } = CoolingMedium.Water;
    public double QuenchTemperature { get; set; } = 25.0;      // °C
    public double QuenchFlowRate { get; set; } = 100.0;        // L/min
    public double QuenchDelayTime { get; set; } = 5.0;         // seconds
    
    // Process timing
    public TimeSpan MaxCycleTime { get; set; } = TimeSpan.FromHours(8);
    public TimeSpan SoakTime { get; set; } = TimeSpan.FromMinutes(30);
    public bool AutomaticProgression { get; set; } = true;
    
    // Safety limits
    public double MaxSafeTemperature { get; set; } = 1200.0;   // °C
    public double MinSafeTemperature { get; set; } = 20.0;     // °C
    public double MaxPressure { get; set; } = 5.0;             // bar
    public bool RequireOperatorConfirmation { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}

public class TemperatureStep
{
    public int StepNumber { get; set; }
    public string StepName { get; set; } = string.Empty;       // "Heat-up", "Soak", "Quench", "Temper"
    public double TargetTemperature { get; set; }              // °C
    public TimeSpan Duration { get; set; }                     // Time at temperature
    public double RampRate { get; set; }                       // °C/min to reach target
    public bool HoldTemperature { get; set; } = true;          // Maintain temperature during duration
    public string AtmosphereControl { get; set; } = "Neutral"; // Neutral, Carburizing, Decarburizing
}

public class HeatTreatmentPerformanceMetrics
{
    public string FurnaceId { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; }
    public TimeSpan CalculationPeriod { get; set; }
    
    // Production metrics
    public int BatchesCompleted { get; set; }
    public int BarsProcessed { get; set; }
    public double TotalTonnage { get; set; }                // tons
    public double ThroughputRate { get; set; }              // tons/hour
    public double FurnaceUtilization { get; set; }          // %
    
    // Quality metrics
    public double QualityPassRate { get; set; }             // %
    public double MechanicalPropertyCompliance { get; set; } // %
    public double HardnessConsistency { get; set; }         // % within spec
    public double OverallQualityScore { get; set; }         // 0-100
    
    // Grade-specific production
    public Dictionary<RebarGrade, double> ProductionByGrade { get; set; } = new();
    public Dictionary<RebarSize, int> BatchesBySize { get; set; } = new();
    public Dictionary<HeatTreatmentType, int> TreatmentsByType { get; set; } = new();
    
    // Energy and efficiency
    public double TotalEnergyConsumption { get; set; }       // kWh
    public double EnergyPerTon { get; set; }                 // kWh/ton
    public double ThermalEfficiency { get; set; }            // %
    public double FuelConsumption { get; set; }              // L or m³
    
    // Process performance
    public double AverageCycleTime { get; set; }             // hours
    public double TemperatureAccuracy { get; set; }          // % within tolerance
    public double ProcessConsistency { get; set; }           // % deviation
    public double CoolingRateAccuracy { get; set; }          // % within spec
    
    // Defect analysis
    public int OverheatingDefects { get; set; }
    public int UnderheatingDefects { get; set; }
    public int CoolingDefects { get; set; }
    public int AtmosphereDefects { get; set; }
    public double DefectRate { get; set; }                   // defects per 1000 bars
    
    // Equipment performance
    public double EquipmentUptime { get; set; }              // %
    public double MaintenanceTime { get; set; }              // hours
    public int UnplannedDowntime { get; set; }               // occurrences
    public double MeanTimeBetweenFailures { get; set; }      // hours
}

public class HeatTreatmentAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FurnaceId { get; set; } = string.Empty;
    public string? BatchId { get; set; }
    public RebarGrade? AffectedGrade { get; set; }
    public RebarSize? AffectedSize { get; set; }
    
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical
    public string Description { get; set; } = string.Empty;
    
    // Temperature-related readings
    public double? TemperatureReading { get; set; }          // °C
    public double? TemperatureDeviation { get; set; }        // °C from target
    public double? CoolingRateReading { get; set; }          // °C/s
    
    // Atmosphere readings
    public double? OxygenLevel { get; set; }                 // %
    public double? CarbonPotential { get; set; }             // %
    public double? AtmospherePressure { get; set; }          // Pa
    
    // Equipment status
    public bool? HeatingElementsOperational { get; set; }
    public bool? QuenchSystemOperational { get; set; }
    public bool? FansOperational { get; set; }
    public bool? SafetySystemsActive { get; set; }
    
    // Quality measurements
    public double? HardnessReading { get; set; }             // HRC/HRB
    public double? StrengthReading { get; set; }             // MPa
    public double? QualityScore { get; set; }                // 0-100
    
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionAction { get; set; }
    public string? OperatorId { get; set; }
    
    public static class AlertTypes
    {
        public const string OverTemperature = "OVER_TEMPERATURE";
        public const string UnderTemperature = "UNDER_TEMPERATURE";
        public const string TemperatureInstability = "TEMPERATURE_INSTABILITY";
        public const string QuenchSystemFailure = "QUENCH_SYSTEM_FAILURE";
        public const string AtmosphereDeviation = "ATMOSPHERE_DEVIATION";
        public const string HeatingElementFailure = "HEATING_ELEMENT_FAILURE";
        public const string ThermocoupleMalfunction = "THERMOCOUPLE_MALFUNCTION";
        public const string CycleTimeExceeded = "CYCLE_TIME_EXCEEDED";
        public const string MechanicalPropertyFailure = "MECHANICAL_PROPERTY_FAILURE";
        public const string HardnessOutOfSpec = "HARDNESS_OUT_OF_SPEC";
        public const string CoolingRateDeviation = "COOLING_RATE_DEVIATION";
        public const string EnergyConsumptionHigh = "ENERGY_CONSUMPTION_HIGH";
        public const string FurnaceOverloaded = "FURNACE_OVERLOADED";
        public const string EmergencyShutdown = "EMERGENCY_SHUTDOWN";
    }
    
    public string GetRecommendedAction()
    {
        return AlertType switch
        {
            AlertTypes.OverTemperature => "Reduce heating rate, check temperature controller, verify thermocouple accuracy",
            AlertTypes.UnderTemperature => "Increase heating power, check heating elements, verify gas supply",
            AlertTypes.TemperatureInstability => "Check PID controller tuning, inspect heating elements, verify airflow",
            AlertTypes.QuenchSystemFailure => "Check quench pump, verify water supply, inspect spray nozzles",
            AlertTypes.AtmosphereDeviation => "Adjust gas flow rates, check atmosphere probe, verify sealing",
            AlertTypes.HeatingElementFailure => "Stop heating, inspect elements, schedule replacement",
            AlertTypes.ThermocoupleMalfunction => "Verify thermocouple connections, check for damage, recalibrate",
            AlertTypes.CycleTimeExceeded => "Review process parameters, check equipment status, optimize heating rate",
            AlertTypes.MechanicalPropertyFailure => "Review treatment temperature and time, check cooling rate",
            AlertTypes.HardnessOutOfSpec => "Adjust quench temperature, verify cooling medium, check tempering parameters",
            AlertTypes.CoolingRateDeviation => "Check quench flow rate, verify cooling medium temperature, inspect agitation",
            AlertTypes.EnergyConsumptionHigh => "Optimize temperature profile, check insulation, review load efficiency",
            AlertTypes.FurnaceOverloaded => "Reduce batch size, optimize loading pattern, verify capacity limits",
            AlertTypes.EmergencyShutdown => "Investigate safety system activation, check all interlocks, inspect equipment",
            _ => "Contact heat treatment supervisor for detailed analysis"
        };
    }
}

// Heat treatment specification for different grades
public class HeatTreatmentSpec
{
    public RebarGrade Grade { get; set; }
    public RebarSize Size { get; set; }
    public HeatTreatmentType TreatmentType { get; set; }
    
    // Temperature specifications
    public double AustenitizingTemperature { get; set; }     // °C
    public double TemperingTemperature { get; set; }         // °C
    public TimeSpan SoakTime { get; set; }
    public TimeSpan TemperTime { get; set; }
    
    // Cooling specifications
    public CoolingMedium QuenchMedium { get; set; }
    public double QuenchTemperature { get; set; }            // °C
    public double MinCoolingRate { get; set; }               // °C/s
    public double MaxCoolingRate { get; set; }               // °C/s
    
    // Target mechanical properties
    public double TargetYieldStrength { get; set; }          // MPa
    public double TargetTensileStrength { get; set; }         // MPa
    public double TargetElongation { get; set; }             // %
    public double TargetHardness { get; set; }               // HRC/HRB
    
    public static HeatTreatmentSpec GetStandardSpec(RebarGrade grade, RebarSize size, HeatTreatmentType treatmentType)
    {
        var spec = new HeatTreatmentSpec
        {
            Grade = grade,
            Size = size,
            TreatmentType = treatmentType
        };

        // Set parameters based on grade
        (spec.TargetYieldStrength, spec.TargetTensileStrength) = grade switch
        {
            RebarGrade.Grade40 => (275, 420),
            RebarGrade.Grade60 => (420, 620),
            RebarGrade.Grade75 => (520, 690),
            RebarGrade.Grade80 => (550, 720),
            _ => (420, 620)
        };

        // Heat treatment temperatures (typical for carbon steel rebar)
        spec.AustenitizingTemperature = treatmentType switch
        {
            HeatTreatmentType.Quenching or HeatTreatmentType.QuenchAndTemper => 900,
            HeatTreatmentType.Normalization => 850,
            HeatTreatmentType.Annealing => 750,
            _ => 850
        };

        spec.TemperingTemperature = grade switch
        {
            RebarGrade.Grade40 => 650,   // Lower strength, higher toughness
            RebarGrade.Grade60 => 600,   // Balanced properties
            RebarGrade.Grade75 => 550,   // Higher strength
            RebarGrade.Grade80 => 500,   // Maximum strength
            _ => 600
        };

        // Timing based on size
        var diameter = (double)size;
        var sizeTimeMultiplier = Math.Max(1.0, diameter / 20.0); // Scale with diameter
        
        spec.SoakTime = TimeSpan.FromMinutes(15 * sizeTimeMultiplier);
        spec.TemperTime = TimeSpan.FromMinutes(30 * sizeTimeMultiplier);

        // Cooling parameters
        spec.QuenchMedium = diameter > 32 ? CoolingMedium.Oil : CoolingMedium.Water;
        spec.QuenchTemperature = 25;
        spec.MinCoolingRate = 10;   // °C/s
        spec.MaxCoolingRate = 100;  // °C/s

        // Target properties
        spec.TargetElongation = 9.0; // ASTM minimum for rebar
        spec.TargetHardness = grade switch
        {
            RebarGrade.Grade40 => 85,    // HRB
            RebarGrade.Grade60 => 95,    // HRB
            RebarGrade.Grade75 => 22,    // HRC
            RebarGrade.Grade80 => 28,    // HRC
            _ => 95
        };

        return spec;
    }
}