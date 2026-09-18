using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Bilingual Help & Knowledge System»: لایه‌ی سرویس مرکزی برای Help Center، راهنمای
/// Context-Aware، جستجو و مدیریت ادمین. هر متد languageCode می‌گیرد ("fa"/"en") - هیچ متنی
/// در این لایه Hard-code نمی‌شود، همه از HelpArticleTranslation می‌آید.
/// </summary>
public interface IHelpService
{
    Task<IReadOnlyList<HelpCategoryDto>> GetCategoriesAsync(string languageCode);

    Task<IReadOnlyList<HelpArticleSummaryDto>> GetArticlesByCategoryAsync(
        int helpCategoryId, string languageCode, bool includeUnpublished = false);

    Task<HelpArticleDetailDto?> GetArticleByCodeAsync(string code, string languageCode, bool includeUnpublished = false);

    /// <summary>
    /// طبق بخش ۵ سند (Context-Aware Resolution): اولین مقاله‌ی منتشرشده با این HelpKey را
    /// برمی‌گرداند - برای دکمه/پنل راهنمای زمینه‌ای روی خود صفحه. اگر چند مقاله (مثلاً یک Page
    /// + چند Field) با یک HelpKey مطابقت داشته باشند، ArticleType="Page" در اولویت است.
    /// </summary>
    Task<HelpArticleDetailDto?> GetArticleByContextAsync(string helpKey, string languageCode);

    Task<IReadOnlyList<HelpArticleSummaryDto>> SearchAsync(
        string query, string languageCode, int page = 1, int pageSize = 20);

    // ============ Admin ============

    Task<IReadOnlyList<HelpArticleSummaryDto>> GetAllForAdminAsync(
        string languageCode, int? helpCategoryId, string? articleType, bool? isPublished);

    Task<HelpArticleAdminEditDto?> GetForEditAsync(int helpArticleId);

    Task<int> CreateArticleAsync(HelpArticleAdminEditDto dto, int? actorEmployeeId);

    Task UpdateArticleAsync(int helpArticleId, HelpArticleAdminEditDto dto);

    Task SetPublishedAsync(int helpArticleId, bool isPublished);

    Task DeleteArticleAsync(int helpArticleId);
}
