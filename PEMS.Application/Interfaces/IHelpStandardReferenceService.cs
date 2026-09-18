using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Help Phase 4 — Increment 3A»: مدیریت Standard Reference - روی همان HelpArticle/
/// IHelpService موجود ساخته شده (نه یک زیرسیستم موازی)، فقط رکورد ساختاریافته‌ی کمکی
/// (HelpStandardReference) را هم به‌صورت هم‌زمان مدیریت می‌کند.
/// </summary>
public interface IHelpStandardReferenceService
{
    Task<IReadOnlyList<HelpStandardReferenceMatrixRowDto>> GetMatrixAsync(bool includeUnpublished = true);

    // طبق الگوی IHelpVisualService.GetVisualsForArticleAsync - برای رندر صفحه‌ی عمومی مقاله
    Task<HelpStandardReferenceMatrixRowDto?> GetByArticleIdAsync(int helpArticleId);

    Task<HelpStandardReferenceAdminEditDto?> GetForEditAsync(int helpStandardReferenceId);

    Task<int> CreateAsync(HelpStandardReferenceAdminEditDto dto, int? actorEmployeeId);

    Task UpdateAsync(int helpStandardReferenceId, HelpStandardReferenceAdminEditDto dto);

    Task SetPublishedAsync(int helpStandardReferenceId, bool isPublished);

    Task DeleteAsync(int helpStandardReferenceId);
}
