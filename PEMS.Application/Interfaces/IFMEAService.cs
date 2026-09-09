using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 4» بند ۱۳/۳۵/۳۶: چرخه‌عمر FMEA - DRAFT قابل‌ویرایش توسط هر Editor؛
/// Submit فقط وضعیت را به UNDER_REVIEW می‌برد؛ Approve فقط توسط کاربر دارای مجوز Approve
/// مجاز است و آن نسخه را غیرقابل‌ویرایش می‌کند (نسخه‌ی بعدی باید یک FMEAAnalysis جدید با
/// Revision بالاتر باشد که نسخه‌ی قبلی را SUPERSEDED می‌کند - هرگز Overwrite مخرب).
/// </summary>
public interface IFMEAService
{
    Task<List<FMEAAnalysisDto>> GetForEquipmentClassAsync(int equipmentClassId);
    Task<FMEAAnalysisDto?> GetByIdAsync(int fmeaAnalysisId);

    Task<int> CreateDraftAsync(int equipmentClassId, string title, string? description, string preparedBy);

    Task<int> AddItemAsync(int fmeaAnalysisId, int failureModeId, int? functionalFailureId,
        int? failureCauseId, int? failureMechanismId, int? failureEffectId, int? failureConsequenceId,
        string? detectionMethod, string? existingControl, string? recommendedAction,
        int? severity, int? occurrence, int? detectability,
        int? maintenanceStrategyTypeId, int? standardReferenceId);

    Task<bool> SubmitForReviewAsync(int fmeaAnalysisId, string reviewedBy);
    Task<bool> ApproveAsync(int fmeaAnalysisId, string approvedBy);
    Task<bool> RejectAsync(int fmeaAnalysisId, string reviewedBy);
}
