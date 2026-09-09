using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 4» بند ۱۷-۲۰: RCMAnalysis فقط روی یک FMEAAnalysis (هر وضعیتی) سوار می‌شود -
/// اما تایید نهایی RCM مستقل از تایید FMEA است (بند ۲۸: «RCM Approved» هرگز صرفاً به دلیل
/// وجود FMEA اعلام نمی‌شود؛ سرویس Readiness این را بررسی می‌کند، نه این سرویس). RCMDecision
/// هرگز خودکار یک MaintenanceStrategy واقعی نمی‌سازد - فقط یک توصیه‌ی سطح کلاس است (بند ۳۱).
/// </summary>
public interface IRCMService
{
    Task<List<RCMAnalysisDto>> GetForEquipmentClassAsync(int equipmentClassId);
    Task<RCMAnalysisDto?> GetByIdAsync(int rcmAnalysisId);

    Task<int> CreateDraftAsync(int equipmentClassId, int fmeaAnalysisId, string preparedBy);

    Task<int> AddDecisionAsync(int rcmAnalysisId, int fmeaItemId, string decisionType,
        int? maintenanceStrategyTypeId, int? recommendedPMTemplateId, string? rationale,
        string consequenceCategory);

    Task<bool> ApproveDecisionAsync(int rcmDecisionId, string approvedBy);

    Task<bool> SubmitForReviewAsync(int rcmAnalysisId, string reviewedBy);
    Task<bool> ApproveAsync(int rcmAnalysisId, string approvedBy);
    Task<bool> RejectAsync(int rcmAnalysisId, string reviewedBy);
}
