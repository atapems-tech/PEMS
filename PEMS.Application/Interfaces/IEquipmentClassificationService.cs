using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 3 — Equipment Classification Completion» بند ۴/۹/۱۰: موتور طبقه‌بندی
/// قطعی و چندلایه (Exact/Alias/Pattern روی EquipmentType+Name) + گردش‌کار تایید انسانی. EXACT/HIGH
/// مستقیم اعمال می‌شوند (بند ۱۲)؛ MEDIUM/LOW هرگز Equipment.EquipmentClassId را تغییر نمی‌دهند -
/// فقط یک EquipmentClassificationSuggestion برای بازبینی می‌سازند. رکوردهایی که از قبل
/// EquipmentClassId دارند (صرف‌نظر از Source) هرگز توسط Backfill بازنویسی نمی‌شوند.
/// </summary>
public interface IEquipmentClassificationService
{
    Task<ClassificationDashboardDto> GetDashboardAsync();

    Task<ReviewQueuePageDto> GetReviewQueueAsync(int page, int pageSize, string statusFilter = "PENDING");

    Task<ReviewQueueItemDto?> GetPendingSuggestionForEquipmentAsync(int equipmentId);

    Task<ClassificationExplanationDto?> ExplainClassificationAsync(int equipmentId);

    Task<BackfillResultDto> BackfillAsync();

    Task<bool> AcceptSuggestionAsync(int suggestionId, string reviewedBy, string? comment);

    Task<bool> RejectSuggestionAsync(int suggestionId, string reviewedBy, string? comment);
}
