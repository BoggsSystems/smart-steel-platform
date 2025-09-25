using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LadleMetallurgyService.Models;

// Rebar grade and chemistry specifications
public enum RebarGrade
{
    Grade40 = 40,    // 40 ksi yield strength (275 MPa)
    Grade60 = 60,    // 60 ksi yield strength (420 MPa) 
    Grade75 = 75,    // 75 ksi yield strength (520 MPa)
    Grade80 = 80     // 80 ksi yield strength (550 MPa)
}

public enum LadleTreatmentType
{
    Degassing,           // Vacuum degassing
    Desulfurization,     // Sulfur removal
    Deoxidation,        // Oxygen removal
    AlloyCorrection,    // Chemistry adjustment
    TemperatureControl,  // Heat management
    Homogenization      // Chemical mixing
}

public class TargetChemistry
{
    public RebarGrade Grade { get; set; }
    public double TargetCarbon { get; set; }
    public double TargetManganese { get; set; }
    public double TargetPhosphorus { get; set; }
    public double TargetSulfur { get; set; }
    public double TargetSilicon { get; set; }
    public double TargetNitrogen { get; set; }
    
    // Tolerance ranges
    public double CarbonTolerance { get; set; } = 0.02;
    public double ManganeseTolerance { get; set; } = 0.05;
    public double PhosphorusTolerance { get; set; } = 0.005;
    public double SulfurTolerance { get; set; } = 0.003;
    
    // ASTM specifications by grade
    public static TargetChemistry GetTargetForGrade(RebarGrade grade)
    {
        return grade switch
        {
            RebarGrade.Grade40 => new TargetChemistry 
            { 
                Grade = grade, TargetCarbon = 0.28, TargetManganese = 1.35, 
                TargetPhosphorus = 0.035, TargetSulfur = 0.04, TargetSilicon = 0.25 
            },
            RebarGrade.Grade60 => new TargetChemistry 
            { 
                Grade = grade, TargetCarbon = 0.26, TargetManganese = 1.40, 
                TargetPhosphorus = 0.035, TargetSulfur = 0.04, TargetSilicon = 0.30 
            },
            RebarGrade.Grade75 => new TargetChemistry 
            { 
                Grade = grade, TargetCarbon = 0.24, TargetManganese = 1.45, 
                TargetPhosphorus = 0.030, TargetSulfur = 0.035, TargetSilicon = 0.35 
            },
            RebarGrade.Grade80 => new TargetChemistry 
            { 
                Grade = grade, TargetCarbon = 0.22, TargetManganese = 1.50, 
                TargetPhosphorus = 0.030, TargetSulfur = 0.035, TargetSilicon = 0.40 
            },
            _ => new TargetChemistry 
            { 
                Grade = RebarGrade.Grade60, TargetCarbon = 0.26, TargetManganese = 1.40, 
                TargetPhosphorus = 0.035, TargetSulfur = 0.04, TargetSilicon = 0.30 
            }
        };
    }
}

public class LadleStatus
{
    public string LadleId { get; set; } = string.Empty;
    public string HeatId { get; set; } = string.Empty;
    public RebarGrade TargetGrade { get; set; } = RebarGrade.Grade60;
    public string TreatmentStation { get; set; } = string.Empty;
    public string CurrentState { get; set; } = "Idle"; // Idle, Receiving, Treating, Ready, Casting
    
    // Steel composition
    public double SteelTemperature { get; set; }
    public double SteelWeight { get; set; }
    public double CurrentCarbon { get; set; }
    public double CurrentManganese { get; set; }
    public double CurrentPhosphorus { get; set; }
    public double CurrentSulfur { get; set; }
    public double CurrentSilicon { get; set; }
    public double CurrentNitrogen { get; set; }
    public double CurrentOxygen { get; set; }
    
    // Treatment parameters
    public double ArgonFlowRate { get; set; }
    public double VacuumPressure { get; set; }
    public double StirringPower { get; set; }
    public double WireFeeding { get; set; } // For wire injection treatments
    public bool IsHomogenized { get; set; }
    
    // Quality metrics
    public double ChemistryAccuracy { get; set; }
    public double TemperatureStability { get; set; }
    public double TreatmentEfficiency { get; set; }
    public DateTime LastUpdated { get; set; }
    
    // Check if chemistry is within specification
    public bool IsChemistryOnTarget()
    {
        var target = TargetChemistry.GetTargetForGrade(TargetGrade);
        return Math.Abs(CurrentCarbon - target.TargetCarbon) <= target.CarbonTolerance &&
               Math.Abs(CurrentManganese - target.TargetManganese) <= target.ManganeseTolerance &&
               CurrentPhosphorus <= target.TargetPhosphorus + target.PhosphorusTolerance &&
               CurrentSulfur <= target.TargetSulfur + target.SulfurTolerance;
    }
}

public class LadleMetallurgyTelemetry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string LadleId { get; set; } = string.Empty;
    public string HeatId { get; set; } = string.Empty;
    public RebarGrade TargetGrade { get; set; }
    
    // Steel measurements
    public double SteelTemperature { get; set; }
    public double SteelWeight { get; set; }
    
    // Real-time chemistry analysis
    public double Carbon { get; set; }
    public double Manganese { get; set; }
    public double Phosphorus { get; set; }
    public double Sulfur { get; set; }
    public double Silicon { get; set; }
    public double Aluminum { get; set; }
    public double Nitrogen { get; set; }
    public double Oxygen { get; set; }
    
    // Treatment process parameters
    public double ArgonFlowRate { get; set; }
    public double VacuumLevel { get; set; }
    public double StirringIntensity { get; set; }
    public double WireFeedRate { get; set; }
    public double PowerConsumption { get; set; }
    
    // Alloy additions
    public double FerroManganeseAdded { get; set; }
    public double FerroSiliconAdded { get; set; }
    public double AluminumAdded { get; set; }
    public double CarbonAdded { get; set; }
    public double LimeAdded { get; set; }
    
    // Environmental conditions
    public double AmbientTemperature { get; set; }
    public double TreatmentTime { get; set; }
    public DateTime Timestamp { get; set; }
}

public class TreatmentRecipe
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public RebarGrade TargetGrade { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public List<LadleTreatmentType> TreatmentSequence { get; set; } = new();
    
    // Treatment parameters
    public double ArgonFlowRate { get; set; }
    public double VacuumPressure { get; set; }
    public double TreatmentDuration { get; set; } // minutes
    public double StirringIntensity { get; set; }
    
    // Chemistry corrections
    public Dictionary<string, double> AlloyAdditions { get; set; } = new();
    public double TargetTemperature { get; set; }
    public double MaxTemperatureDrop { get; set; }
    
    // Quality targets
    public double MinHomogenizationTime { get; set; }
    public double MaxOxygenContent { get; set; }
    public double MaxNitrogenContent { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}

public class LadleMetallurgyPerformanceMetrics
{
    public string LadleId { get; set; } = string.Empty;
    public string HeatId { get; set; } = string.Empty;
    public RebarGrade Grade { get; set; }
    public DateTime ProcessedAt { get; set; }
    
    // Chemistry performance
    public double ChemistryAccuracy { get; set; }
    public double TemperatureLoss { get; set; }
    public double TreatmentTime { get; set; }
    public double AlloyConsumption { get; set; }
    public double EnergyConsumption { get; set; }
    
    // Quality metrics
    public double SulfurRemovalEfficiency { get; set; }
    public double OxygenRemovalEfficiency { get; set; }
    public double HomogenizationIndex { get; set; }
    public double YieldRatio { get; set; }
    
    // Process efficiency
    public double StationUtilization { get; set; }
    public double ProcessCycleTime { get; set; }
    public double CostPerTon { get; set; }
    public bool MetQualityTargets { get; set; }
    
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

public class LadleMetallurgyAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string LadleId { get; set; } = string.Empty;
    public string HeatId { get; set; } = string.Empty;
    public RebarGrade AffectedGrade { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string Description { get; set; } = string.Empty;
    
    // Process readings
    public double? TemperatureReading { get; set; }
    public double? CarbonDeviation { get; set; }
    public double? SulfurLevel { get; set; }
    public double? OxygenLevel { get; set; }
    public double? VacuumReading { get; set; }
    
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionAction { get; set; }
    
    // Rebar-specific alert types
    public static class AlertTypes
    {
        public const string ChemistryOutOfSpec = "CHEMISTRY_OUT_OF_SPEC";
        public const string TemperatureDropExcessive = "TEMPERATURE_DROP_EXCESSIVE";
        public const string SulfurRemovalInefficient = "SULFUR_REMOVAL_INEFFICIENT";
        public const string VacuumLevelLow = "VACUUM_LEVEL_LOW";
        public const string HomogenizationIncomplete = "HOMOGENIZATION_INCOMPLETE";
        public const string AlloyConsumptionHigh = "ALLOY_CONSUMPTION_HIGH";
        public const string TreatmentTimeExceeded = "TREATMENT_TIME_EXCEEDED";
        public const string OxygenLevelHigh = "OXYGEN_LEVEL_HIGH";
    }
    
    public string GetRecommendedAction()
    {
        return AlertType switch
        {
            AlertTypes.ChemistryOutOfSpec => "Adjust alloy additions, increase treatment time",
            AlertTypes.TemperatureDropExcessive => "Reduce treatment time, check ladle preheating",
            AlertTypes.SulfurRemovalInefficient => "Increase lime addition, extend desulfurization",
            AlertTypes.VacuumLevelLow => "Check vacuum system, inspect seals",
            AlertTypes.HomogenizationIncomplete => "Extend stirring time, increase argon flow",
            AlertTypes.AlloyConsumptionHigh => "Review recipe, check addition timing",
            AlertTypes.TreatmentTimeExceeded => "Optimize process parameters, check equipment",
            AlertTypes.OxygenLevelHigh => "Increase deoxidation, add aluminum",
            _ => "Contact metallurgy engineer for analysis"
        };
    }
}

// Production planning and scheduling
public class HeatSchedule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string HeatId { get; set; } = string.Empty;
    public string LadleId { get; set; } = string.Empty;
    public RebarGrade TargetGrade { get; set; }
    public double SteelWeight { get; set; }
    
    // Scheduling
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledTreatmentComplete { get; set; }
    public DateTime ScheduledCastingStart { get; set; }
    public string CustomerOrder { get; set; } = string.Empty;
    public int Priority { get; set; } = 5; // 1-10 scale
    
    // Status tracking
    public string Status { get; set; } = "Scheduled"; // Scheduled, InProgress, Completed, Delayed
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualComplete { get; set; }
    public double QualityAchieved { get; set; }
    public string Notes { get; set; } = string.Empty;
}