using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 7.6 — Engineering Criticality Assessment & Controlled Maintenance
/// Activation»: گردش‌کار مهندسی DRAFT→UNDER_REVIEW→APPROVED→SUPERSEDED برای ارزیابی
/// بحرانی‌بودن تجهیز - عیناً از FMEAService (فاز ۴) الگوبرداری شده. تنها در لحظه‌ی Approve،
/// نتیجه در EquipmentCriticality موجود منتشر می‌شود (بازاستفاده، نه بازسازی).
/// </summary>
public interface ICriticalityAssessmentService
{
    Task<PagedResult<CriticalityAssessmentListDto>> GetQueueAsync(
        string? status = null, int? equipmentClassId = null, int page = 1, int pageSize = 50);

    Task<CriticalityAssessmentDetailDto?> GetByIdAsync(int assessmentId);

    Task<CriticalityDashboardDto> GetDashboardAsync();

    Task<(bool Success, string? Error, int? Id)> CreateDraftAsync(int equipmentId, string? preparedBy, string? rationale);

    Task<(bool Success, string? Error)> SetDimensionScoresAsync(
        int assessmentId, int? safetyScore, int? productionScore, int? environmentScore, string? rationale);

    Task<(bool Success, string? Error)> AddEvidenceAsync(
        int assessmentId, string sourceType, string? sourceIdentifier, string? reference, string description, string? createdBy);

    Task<(bool Success, string? Error)> SubmitForReviewAsync(int assessmentId, string? reviewedBy);

    Task<(bool Success, string? Error)> ApproveAsync(int assessmentId, string? approvedBy);

    Task<(bool Success, string? Error)> RejectAsync(int assessmentId, string? reviewedBy);
}
