using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ReheatingFurnaceService.Models;

public enum RebarGrade
{
    Grade40 = 40,
    Grade60 = 60, 
    Grade75 = 75,
    Grade80 = 80
}

public enum BilletSize
{
    Size100x100 = 100,
    Size120x120 = 120,  
    Size150x150 = 150,
    Size160x160 = 160
}

public enum FurnaceZone
{
    ChargingZone,    // Billet entry
    PreheatingZone,  // Initial heating
    HeatingZone,     // Main heating
    SoakingZone,     // Temperature equalization
    DischargeZone    // Exit to rolling mill
}

public class ReheatingFurnaceStatus
{
    public string FurnaceId { get; set; } = string.Empty;
    public string FurnaceName { get; set; } = string.Empty;
    public string OperationalState { get; set; } = "Idle"; // Idle, Heating, Maintenance, Emergency
    
    // Zone temperatures (°C)
    public double ChargingZoneTemp { get; set; }
    public double PreheatingZoneTemp { get; set; }
    public double HeatingZoneTemp { get; set; }
    public double SoakingZoneTemp { get; set; }
    public double DischargeZoneTemp { get; set; }
    
    // Fuel system
    public double NaturalGasFlow { get; set; } // Nm³/h
    public double AirFlow { get; set; }        // Nm³/h
    public double OxygenEnrichment { get; set; } // %
    public double FuelPressure { get; set; }
    
    // Atmosphere control
    public double CO2Percentage { get; set; }
    public double COPercentage { get; set; }
    public double O2Percentage { get; set; }
    public double ExcessAirRatio { get; set; }
    
    // Process parameters
    public double FurnacePressure { get; set; }
    public double DraftFanSpeed { get; set; }
    public double BilletResidenceTime { get; set; } // minutes
    public double ThroughputRate { get; set; }      // billets/hour
    public int BilletsInFurnace { get; set; }
    public double EnergyConsumption { get; set; }   // kWh/ton
    
    // Burner status
    public int ActiveBurners { get; set; }
    public int TotalBurners { get; set; }
    public double BurnerEfficiency { get; set; }
    
    public DateTime LastUpdated { get; set; }
}

public class BilletHeatingSchedule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FurnaceId { get; set; } = string.Empty;
    public string BilletId { get; set; } = string.Empty;
    public string HeatId { get; set; } = string.Empty;
    
    public RebarGrade Grade { get; set; }
    public BilletSize Size { get; set; }
    public double BilletWeight { get; set; }
    
    // Heating profile
    public double TargetRollingTemp { get; set; } = 1200; // °C
    public double MaxHeatingRate { get; set; } = 100;     // °C/hour
    public double SoakingTime { get; set; } = 30;         // minutes
    
    // Schedule timing
    public DateTime ChargingTime { get; set; }
    public DateTime EstimatedDischargeTime { get; set; }
    public DateTime? ActualDischargeTime { get; set; }
    
    public string Status { get; set; } = "Scheduled"; // Scheduled, Charging, Heating, Soaking, Ready, Discharged
    public int FurnacePosition { get; set; }
    
    // Quality requirements
    public double MaxScaleFormation { get; set; } = 2.0; // mm
    public double UniformityTolerance { get; set; } = 50; // °C
    
    // Get optimal heating parameters by grade and size
    public static HeatingParameters GetOptimalParameters(RebarGrade grade, BilletSize size)
    {
        return grade switch
        {
            RebarGrade.Grade40 => new HeatingParameters 
            { 
                TargetTemp = 1150, HeatingRate = 120, SoakTime = 25, Grade = grade, Size = size 
            },
            RebarGrade.Grade60 => new HeatingParameters 
            { 
                TargetTemp = 1200, HeatingRate = 100, SoakTime = 30, Grade = grade, Size = size 
            },
            RebarGrade.Grade75 => new HeatingParameters 
            { 
                TargetTemp = 1220, HeatingRate = 90, SoakTime = 35, Grade = grade, Size = size 
            },
            RebarGrade.Grade80 => new HeatingParameters 
            { 
                TargetTemp = 1250, HeatingRate = 80, SoakTime = 40, Grade = grade, Size = size 
            },
            _ => new HeatingParameters 
            { 
                TargetTemp = 1200, HeatingRate = 100, SoakTime = 30, Grade = grade, Size = size 
            }
        };
    }
}

public class HeatingParameters
{
    public double TargetTemp { get; set; }
    public double HeatingRate { get; set; }
    public double SoakTime { get; set; }
    public RebarGrade Grade { get; set; }
    public BilletSize Size { get; set; }
}

public class ReheatingFurnaceTelemetry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FurnaceId { get; set; } = string.Empty;
    
    // Zone temperatures
    public double ChargingZoneTemp { get; set; }
    public double PreheatingZoneTemp { get; set; }
    public double HeatingZoneTemp { get; set; }
    public double SoakingZoneTemp { get; set; }
    public double DischargeZoneTemp { get; set; }
    
    // Billet temperatures by position
    public Dictionary<int, double> BilletTemperatures { get; set; } = new();
    public Dictionary<int, string> BilletPositions { get; set; } = new(); // Position -> BilletId mapping
    
    // Combustion system
    public double NaturalGasFlow { get; set; }
    public double CombustionAirFlow { get; set; }
    public double ExhaustGasTemp { get; set; }
    public double FuelConsumption { get; set; } // Nm³/h
    public double ThermalEfficiency { get; set; }
    
    // Atmosphere analysis
    public double CO2Content { get; set; }
    public double COContent { get; set; }
    public double O2Content { get; set; }
    public double NOxEmissions { get; set; }
    public double SOxEmissions { get; set; }
    
    // Process control
    public double FurnacePressure { get; set; }
    public double DraftControl { get; set; }
    public double WalkingBeamCycle { get; set; }
    public double PushingForce { get; set; }
    
    // Energy metrics
    public double SpecificEnergyConsumption { get; set; } // kWh/ton
    public double HeatRecoveryEfficiency { get; set; }
    
    // Scale formation monitoring
    public double ScaleThickness { get; set; }
    public double ScaleFormationRate { get; set; }
    
    public DateTime Timestamp { get; set; }
}

public class BilletTrackingData
{
    public string BilletId { get; set; } = string.Empty;
    public string FurnaceId { get; set; } = string.Empty;
    public int CurrentPosition { get; set; }
    public FurnaceZone CurrentZone { get; set; }
    
    // Thermal history
    public List<TemperatureReading> TemperatureHistory { get; set; } = new();
    public double CurrentSurfaceTemp { get; set; }
    public double CoreTemperature { get; set; }
    public double TemperatureUniformity { get; set; } // Surface to core difference
    
    // Quality tracking
    public double ScaleThickness { get; set; }
    public double OxidationLoss { get; set; } // kg
    public bool HasDefects { get; set; }
    public List<string> DefectTypes { get; set; } = new();
    
    // Timing
    public DateTime ChargedAt { get; set; }
    public DateTime? EstimatedReadyTime { get; set; }
    public TimeSpan ResidenceTime => DateTime.UtcNow - ChargedAt;
    
    public DateTime LastUpdated { get; set; }
}

public class TemperatureReading
{
    public DateTime Timestamp { get; set; }
    public double Temperature { get; set; }
    public FurnaceZone Zone { get; set; }
}

public class ReheatingFurnacePerformanceMetrics
{
    public string FurnaceId { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; }
    public TimeSpan CalculationPeriod { get; set; } // Metrics calculation window
    
    // Production metrics
    public int BilletsHeated { get; set; }
    public double TotalTonnage { get; set; }
    public double AverageThroughput { get; set; } // billets/hour
    public double FurnaceUtilization { get; set; } // %
    
    // Energy efficiency
    public double SpecificEnergyConsumption { get; set; } // kWh/ton
    public double ThermalEfficiency { get; set; }
    public double HeatRecoveryRate { get; set; }
    public double FuelConsumptionRate { get; set; } // Nm³/ton
    
    // Quality metrics
    public double TemperatureUniformityIndex { get; set; }
    public double ScaleFormationRate { get; set; } // mm/hour
    public double BilletQualityScore { get; set; } // 0-100
    public double RejectionRate { get; set; } // %
    
    // Process stability
    public double TemperatureStability { get; set; }
    public double PressureStability { get; set; }
    public double CombustionStability { get; set; }
    
    // Environmental metrics
    public double NOxEmissionRate { get; set; } // mg/Nm³
    public double CO2EmissionRate { get; set; } // kg/ton
    public double WaterConsumption { get; set; } // L/ton
    
    // Maintenance indicators
    public double RefractoryWearRate { get; set; }
    public double BurnerPerformanceIndex { get; set; }
    public double WalkingBeamEfficiency { get; set; }
    
    // Cost metrics
    public double FuelCostPerTon { get; set; }
    public double MaintenanceCostPerTon { get; set; }
    public double TotalOperatingCost { get; set; }
}

public class ReheatingFurnaceAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FurnaceId { get; set; } = string.Empty;
    public string BilletId { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string Description { get; set; } = string.Empty;
    
    // Temperature readings
    public double? ZoneTemperature { get; set; }
    public double? BilletTemperature { get; set; }
    public double? TemperatureDeviation { get; set; }
    
    // Process readings  
    public double? FuelFlow { get; set; }
    public double? AirFlow { get; set; }
    public double? PressureReading { get; set; }
    public double? EmissionLevel { get; set; }
    
    // Quality indicators
    public double? ScaleThickness { get; set; }
    public double? EnergyConsumption { get; set; }
    
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionAction { get; set; }
    
    public static class AlertTypes
    {
        public const string TemperatureDeviation = "TEMPERATURE_DEVIATION";
        public const string BilletOverheated = "BILLET_OVERHEATED";
        public const string BilletUnderheated = "BILLET_UNDERHEATED";
        public const string FuelPressureLow = "FUEL_PRESSURE_LOW";
        public const string CombustionInstability = "COMBUSTION_INSTABILITY";
        public const string ExcessiveScaleFormation = "EXCESSIVE_SCALE_FORMATION";
        public const string HighEnergyConsumption = "HIGH_ENERGY_CONSUMPTION";
        public const string EmissionLimitExceeded = "EMISSION_LIMIT_EXCEEDED";
        public const string BurnerMalfunction = "BURNER_MALFUNCTION";
        public const string WalkingBeamFailure = "WALKING_BEAM_FAILURE";
        public const string ResidenceTimeExceeded = "RESIDENCE_TIME_EXCEEDED";
    }
    
    public string GetRecommendedAction()
    {
        return AlertType switch
        {
            AlertTypes.TemperatureDeviation => "Check burner operation, adjust fuel/air ratio",
            AlertTypes.BilletOverheated => "Reduce heating rate, check temperature control",
            AlertTypes.BilletUnderheated => "Extend soaking time, increase zone temperature",
            AlertTypes.FuelPressureLow => "Check fuel supply system, inspect pressure regulators",
            AlertTypes.CombustionInstability => "Inspect burners, check air/fuel mixing",
            AlertTypes.ExcessiveScaleFormation => "Optimize atmosphere, consider protective atmosphere",
            AlertTypes.HighEnergyConsumption => "Inspect refractory condition, optimize heating profile",
            AlertTypes.EmissionLimitExceeded => "Adjust combustion parameters, check emission controls",
            AlertTypes.BurnerMalfunction => "Shutdown affected burner, schedule maintenance",
            AlertTypes.WalkingBeamFailure => "Switch to manual mode, contact maintenance team",
            AlertTypes.ResidenceTimeExceeded => "Expedite discharge, check material flow",
            _ => "Contact furnace engineer for detailed analysis"
        };
    }
}

// Optimal heating recipe for different rebar grades
public class HeatingRecipe
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RecipeName { get; set; } = string.Empty;
    public RebarGrade TargetGrade { get; set; }
    public BilletSize BilletSize { get; set; }
    
    // Temperature profile by zone
    public double ChargingZoneTarget { get; set; } = 800;
    public double PreheatingZoneTarget { get; set; } = 950;
    public double HeatingZoneTarget { get; set; } = 1150;
    public double SoakingZoneTarget { get; set; } = 1200;
    public double DischargeZoneTarget { get; set; } = 1200;
    
    // Timing parameters
    public double HeatingRate { get; set; } = 100; // °C/hour
    public double SoakingTime { get; set; } = 30;  // minutes
    public double TotalCycleTime { get; set; } = 180; // minutes
    
    // Atmosphere control
    public double TargetCO2 { get; set; } = 8.0;
    public double TargetCO { get; set; } = 0.5;
    public double TargetO2 { get; set; } = 2.0;
    
    // Energy optimization
    public double TargetSpecificEnergy { get; set; } = 1.8; // GJ/ton
    public double MaxScaleAllowance { get; set; } = 2.0;    // mm
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}