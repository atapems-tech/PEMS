namespace PEMS.Application.DTOs;

// ============================================================================================
// طبق «PEMS Phase 4 — RCM/FMEA/FMECA Decision Intelligence»: همه‌ی DTO ها - هرگز Entity مستقیم
// EF افشا نمی‌شود.
// ============================================================================================

public class FunctionalRequirementDto
{
    public int FunctionalRequirementId { get; set; }
    public int EquipmentClassId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RequiredFunction { get; set; }
    public string? PerformanceRequirement { get; set; }
    public decimal? MinimumPerformance { get; set; }
    public decimal? MaximumPerformance { get; set; }
    public string? Unit { get; set; }
    public List<FunctionalFailureDto> FunctionalFailures { get; set; } = new();
}

public class FunctionalFailureDto
{
    public int FunctionalFailureId { get; set; }
    public int FunctionalRequirementId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FailureDefinition { get; set; }
}

public class FMEAItemDto
{
    public int FMEAItemId { get; set; }
    public int FMEAAnalysisId { get; set; }
    public int? FunctionalFailureId { get; set; }
    public string? FunctionalFailureTitle { get; set; }

    public int FailureModeId { get; set; }
    public string FailureModeName { get; set; } = string.Empty;
    public int? FailureCauseId { get; set; }
    public string? FailureCauseName { get; set; }
    public int? FailureMechanismId { get; set; }
    public string? FailureMechanismName { get; set; }
    public int? FailureEffectId { get; set; }
    public string? FailureEffectName { get; set; }
    public int? FailureConsequenceId { get; set; }
    public string? FailureConsequenceName { get; set; }

    public string? DetectionMethod { get; set; }
    public string? ExistingControl { get; set; }
    public string? RecommendedAction { get; set; }

    public int? Severity { get; set; }
    public int? Occurrence { get; set; }
    public int? Detectability { get; set; }
    public int? RiskPriorityNumber { get; set; }

    public int? MaintenanceStrategyTypeId { get; set; }
    public string? MaintenanceStrategyTypeName { get; set; }
    public int? StandardReferenceId { get; set; }
    public string? StandardReferenceCode { get; set; }
}

public class FMEAAnalysisDto
{
    public int FMEAAnalysisId { get; set; }
    public int EquipmentClassId { get; set; }
    public string EquipmentClassName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Revision { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? AnalysisDate { get; set; }
    public string? PreparedBy { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public List<FMEAItemDto> Items { get; set; } = new();
}

public class RCMDecisionDto
{
    public int RCMDecisionId { get; set; }
    public int RCMAnalysisId { get; set; }
    public int FMEAItemId { get; set; }
    public string FailureModeName { get; set; } = string.Empty;
    public string DecisionType { get; set; } = string.Empty;
    public int? MaintenanceStrategyTypeId { get; set; }
    public string? MaintenanceStrategyTypeName { get; set; }
    public int? RecommendedPMTemplateId { get; set; }
    public string? RecommendedPMTemplateName { get; set; }
    public string? Rationale { get; set; }
    public string ConsequenceCategory { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public class RCMAnalysisDto
{
    public int RCMAnalysisId { get; set; }
    public int EquipmentClassId { get; set; }
    public string EquipmentClassName { get; set; } = string.Empty;
    public int FMEAAnalysisId { get; set; }
    public int Revision { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PreparedBy { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public List<RCMDecisionDto> Decisions { get; set; } = new();
}

/// <summary>
/// طبق بند ۲۶-۲۸/۴۵-۴۶: وضعیت آماده‌بودن یک EquipmentClass برای FMEA/RCM - هرگز صرفاً به دلیل
/// وجود EquipmentClass یا وجود یک FMEA به‌عنوان READY اعلام نمی‌شود؛ ابعاد مختلف (Coverage/
/// Completeness/Approval/Applicability طبق بند ۴۶) جداگانه گزارش می‌شوند.
/// </summary>
public class EquipmentClassRcmFmeaReadinessDto
{
    public int EquipmentClassId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public int EquipmentCount { get; set; }
    public int FunctionalRequirementCount { get; set; }
    public int FunctionalFailureCount { get; set; }
    public int FailureModeCount { get; set; }
    public int FailureCauseCount { get; set; }
    public int FailureMechanismCount { get; set; }
    public int FailureEffectCount { get; set; }
    public int FailureConsequenceCount { get; set; }
    public int StandardReferenceCount { get; set; }
    public int MaintenanceActivityCount { get; set; }
    public int StrategyTypeCount { get; set; }
    public int PMTemplateCount { get; set; }
    public int JobPlanCount { get; set; }

    public int ApprovedFMEACount { get; set; }
    public int DraftFMEACount { get; set; }
    public int ApprovedRCMCount { get; set; }
    public int DraftRCMCount { get; set; }

    // READY, PARTIALLY_READY, INSUFFICIENT_DATA, NOT_APPLICABLE
    public string FMEAReadiness { get; set; } = "NOT_APPLICABLE";
    public string RCMReadiness { get; set; } = "NOT_APPLICABLE";
    public List<string> MissingDimensions { get; set; } = new();
}

public class RcmFmeaCoverageMatrixDto
{
    public List<EquipmentClassRcmFmeaReadinessDto> Classes { get; set; } = new();
    public int ClassesWithEquipment { get; set; }
    public int ClassesFmeaReady { get; set; }
    public int ClassesFmeaPartiallyReady { get; set; }
    public int ClassesRcmReady { get; set; }
    public int ClassesRcmPartiallyReady { get; set; }
    public int TotalApprovedFMEAs { get; set; }
    public int TotalDraftFMEAs { get; set; }
    public int TotalApprovedRCMs { get; set; }
    public int TotalDraftRCMs { get; set; }
}
