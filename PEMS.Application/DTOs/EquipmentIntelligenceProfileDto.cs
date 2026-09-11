namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۹: نمای یکپارچه‌ی هوشمندی یک تجهیز - این DTO چیزی محاسبه نمی‌کند،
/// سرویس‌های موجود (Criticality/CriticalityAssessment/MaintenanceStrategy/IReliabilityService/
/// IRamWeibullService/RcmFmeaReadiness/FailureRecord/JobPlan/BadActors/Predictive/Condition)
/// را کنار هم می‌گذارد. هر معیار در دسترس‌نبود، دلیل صریح دارد - هرگز پنهان/صفر جایگزین نمی‌شود.
/// </summary>
public class EquipmentIntelligenceProfileDto
{
    // Identity
    public int EquipmentId { get; set; }
    public string TagNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? EquipmentClassName { get; set; }
    public int? EquipmentClassId { get; set; }
    public string? DisciplineName { get; set; }
    public int? ParentEquipmentId { get; set; }
    public string? ParentEquipmentTagNumber { get; set; }

    // Criticality
    public string? CurrentCriticalityClass { get; set; }
    public string? CurrentRiskLevel { get; set; }
    public string? LatestAssessmentStatus { get; set; }
    public string? LatestAssessmentFrameworkVersion { get; set; }
    public int LatestAssessmentEvidenceCount { get; set; }

    // Maintenance
    public int ActiveStrategyCount { get; set; }
    public int ActiveMaintenancePlanCount { get; set; }
    public int PreventiveWorkOrderCount { get; set; }
    public int CorrectiveWorkOrderCount { get; set; }
    public int EmergencyWorkOrderCount { get; set; }
    public int OpenBacklogWorkOrderCount { get; set; }

    // Reliability
    public string ReliabilityDataStatus { get; set; } = "INSUFFICIENT_DATA";
    public double? MTBFHours { get; set; }
    public double? MTTRHours { get; set; }
    public double? AvailabilityPercent { get; set; }
    public int FailureCount { get; set; }
    public decimal TotalDowntimeHours { get; set; }

    // Failure Intelligence
    public List<string> TopFailureModes { get; set; } = new();

    // RCM/FMEA (class-level, since FMEA/RCM are scoped by EquipmentClass)
    public string FMEAReadiness { get; set; } = "NOT_APPLICABLE";
    public string RCMReadiness { get; set; } = "NOT_APPLICABLE";

    // Safety
    public bool AnyLinkedJobPlanRequiresPermit { get; set; }
    public bool AnyLinkedJobPlanRequiresLOTO { get; set; }

    // Intelligence
    public bool IsBadActor { get; set; }
    public string WeibullDataStatus { get; set; } = "INSUFFICIENT_DATA";
    public string PredictiveStatus { get; set; } = "NOT_READY";
    public string ConditionMonitoringStatus { get; set; } = "NOT_READY";

    public List<string> UnavailableMetricReasons { get; set; } = new();
}
