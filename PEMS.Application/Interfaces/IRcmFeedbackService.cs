using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 7» بند ۲۰: حلقه‌ی بازخورد RCM - فقط شواهد تولید می‌کند و توصیه‌ی بازبینی
/// می‌سازد؛ هرگز خودش MaintenanceStrategy/MaintenancePlan/PMTemplate/RCMDecision/Criticality
/// را تغییر نمی‌دهد. مسیر تغییر واقعی (در صورت پذیرفتن توصیه) همچنان از طریق ابزارهای موجود
/// (مثلاً ویرایش دستی MaintenanceStrategy یا RcmOperationalBridgeService فاز ۶.۱) توسط همان
/// مهندس انجام می‌شود - این سرویس فقط ورودی تصمیم را روشن می‌کند.
/// </summary>
public interface IRcmFeedbackService
{
    Task<GenerateEvidenceResultDto> GenerateEvidenceAsync(int windowDays = 90, int downtimeThresholdHours = 24);

    Task<List<RcmReviewRecommendationDto>> GetRecommendationsAsync(string? status = null);

    Task<(bool Success, string? Error)> ReviewRecommendationAsync(int recommendationId, bool accepted, string reviewedBy, string? notes);
}
