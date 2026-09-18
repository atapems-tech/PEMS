using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Bilingual Help & Knowledge System». همه‌ی کوئری‌های عمومی (غیر-ادمین) فقط
/// IsPublished=true را می‌بینند - انتشار محتوا یک تصمیم صریح ادمین است، نه پیش‌فرض. ترجمه‌ی
/// درخواستی اگر موجود نبود، به زبان دیگر Fallback می‌شود (بخش ۱۷ سند: «Language fallback
/// behavior») تا کاربر هرگز صفحه‌ی کاملاً خالی نبیند - ولی این باید استثنا باشد، نه قاعده،
/// چون هر مقاله‌ی Seed شده از ابتدا هر دو ترجمه را دارد.
/// </summary>
public class HelpService : IHelpService
{
    private readonly PEMSDbContext _context;
    private readonly IRichTextSanitizerService _sanitizer;

    private static readonly string[] SupportedLanguages = { "en", "fa" };

    public HelpService(PEMSDbContext context, IRichTextSanitizerService sanitizer)
    {
        _context = context;
        _sanitizer = sanitizer;
    }

    private static string NormalizeLanguage(string languageCode) =>
        SupportedLanguages.Contains(languageCode) ? languageCode : "en";

    public async Task<IReadOnlyList<HelpCategoryDto>> GetCategoriesAsync(string languageCode)
    {
        var lang = NormalizeLanguage(languageCode);

        var categories = await _context.HelpCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        var counts = await _context.HelpArticles
            .Where(a => a.IsPublished)
            .GroupBy(a => a.HelpCategoryId)
            .Select(g => new { HelpCategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.HelpCategoryId, x => x.Count);

        return categories.Select(c => new HelpCategoryDto
        {
            HelpCategoryId = c.HelpCategoryId,
            Code = c.Code,
            Name = lang == "fa" ? c.NameFa : c.NameEn,
            IconCssClass = c.IconCssClass,
            ArticleCount = counts.GetValueOrDefault(c.HelpCategoryId, 0)
        }).ToList();
    }

    public async Task<IReadOnlyList<HelpArticleSummaryDto>> GetArticlesByCategoryAsync(
        int helpCategoryId, string languageCode, bool includeUnpublished = false)
    {
        var lang = NormalizeLanguage(languageCode);

        var query = _context.HelpArticles
            .Include(a => a.Category)
            .Include(a => a.Translations)
            .Where(a => a.HelpCategoryId == helpCategoryId);

        if (!includeUnpublished) query = query.Where(a => a.IsPublished);

        var articles = await query.OrderBy(a => a.DisplayOrder).ToListAsync();

        return articles.Select(a => ToSummaryDto(a, lang)).Where(x => x != null).Cast<HelpArticleSummaryDto>().ToList();
    }

    public async Task<HelpArticleDetailDto?> GetArticleByCodeAsync(string code, string languageCode, bool includeUnpublished = false)
    {
        var lang = NormalizeLanguage(languageCode);

        var query = _context.HelpArticles
            .Include(a => a.Category)
            .Include(a => a.Translations)
            .Include(a => a.RelatedArticles).ThenInclude(r => r.RelatedArticle!).ThenInclude(r => r.Translations)
            .Where(a => a.Code == code);

        if (!includeUnpublished) query = query.Where(a => a.IsPublished);

        var article = await query.FirstOrDefaultAsync();
        return article == null ? null : ToDetailDto(article, lang);
    }

    public async Task<HelpArticleDetailDto?> GetArticleByContextAsync(string helpKey, string languageCode)
    {
        var lang = NormalizeLanguage(languageCode);

        var article = await _context.HelpArticles
            .Include(a => a.Category)
            .Include(a => a.Translations)
            .Include(a => a.RelatedArticles).ThenInclude(r => r.RelatedArticle!).ThenInclude(r => r.Translations)
            .Where(a => a.HelpKey == helpKey && a.IsPublished)
            // طبق بخش ۵ سند: اگر برای یک HelpKey هم Page-level هم Field/Action-level باشد،
            // مقاله‌ی سطح صفحه برای دکمه‌ی اصلی راهنما در اولویت است
            .OrderBy(a => a.ArticleType == "Page" ? 0 : 1)
            .ThenBy(a => a.DisplayOrder)
            .FirstOrDefaultAsync();

        return article == null ? null : ToDetailDto(article, lang);
    }

    public async Task<IReadOnlyList<HelpArticleSummaryDto>> SearchAsync(
        string query, string languageCode, int page = 1, int pageSize = 20)
    {
        var lang = NormalizeLanguage(languageCode);
        var term = (query ?? string.Empty).Trim().ToLower();
        if (term.Length == 0) return Array.Empty<HelpArticleSummaryDto>();

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        // طبق بخش ۶.۴ سند: جستجوی دیتابیسیِ ساده کافی است (نه یک موتور جستجوی جدید) - روی
        // عنوان/خلاصه/کلیدواژه‌ی همان زبانِ درخواستی. طبق بخش ۶.۴: تطبیق بین‌زبانی هم پشتیبانی
        // می‌شود - اگر در زبان جاری نتیجه‌ای نبود، ترجمه‌ی زبان دیگر هم بررسی می‌شود (کاربر
        // فارسی‌زبان که "Work Order" تایپ می‌کند باید نتیجه بگیرد).
        var matches = await _context.HelpArticleTranslations
            .Include(t => t.Article!).ThenInclude(a => a!.Category)
            .Where(t => t.Article!.IsPublished &&
                        (t.Title.ToLower().Contains(term) ||
                         t.Summary.ToLower().Contains(term) ||
                         (t.SearchKeywords != null && t.SearchKeywords.ToLower().Contains(term))))
            .ToListAsync();

        // اولویت: نتایج همون زبان جاری اول، بعد نتایج زبان دیگر (بدون تکرار مقاله)
        var ordered = matches
            .OrderByDescending(t => t.LanguageCode == lang)
            .GroupBy(t => t.HelpArticleId)
            .Select(g => g.OrderByDescending(t => t.LanguageCode == lang).First())
            .OrderBy(t => t.Article!.DisplayOrder)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();

        return ordered.Select(t => new HelpArticleSummaryDto
        {
            HelpArticleId = t.HelpArticleId,
            Code = t.Article!.Code,
            ArticleType = t.Article.ArticleType,
            HelpKey = t.Article.HelpKey,
            HelpCategoryId = t.Article.HelpCategoryId,
            CategoryCode = t.Article.Category?.Code ?? "",
            CategoryName = lang == "fa" ? t.Article.Category?.NameFa ?? "" : t.Article.Category?.NameEn ?? "",
            Title = t.Title,
            Summary = t.Summary,
            IsPublished = t.Article.IsPublished,
            DisplayOrder = t.Article.DisplayOrder,
            RelevantRoles = t.Article.RelevantRoles
        }).ToList();
    }

    // ============ Admin ============

    public async Task<IReadOnlyList<HelpArticleSummaryDto>> GetAllForAdminAsync(
        string languageCode, int? helpCategoryId, string? articleType, bool? isPublished)
    {
        var lang = NormalizeLanguage(languageCode);

        var query = _context.HelpArticles
            .Include(a => a.Category)
            .Include(a => a.Translations)
            .AsQueryable();

        if (helpCategoryId.HasValue) query = query.Where(a => a.HelpCategoryId == helpCategoryId.Value);
        if (!string.IsNullOrWhiteSpace(articleType)) query = query.Where(a => a.ArticleType == articleType);
        if (isPublished.HasValue) query = query.Where(a => a.IsPublished == isPublished.Value);

        var articles = await query.OrderBy(a => a.HelpCategoryId).ThenBy(a => a.DisplayOrder).ToListAsync();

        return articles.Select(a => ToSummaryDto(a, lang, allowFallback: true)!).ToList();
    }

    public async Task<HelpArticleAdminEditDto?> GetForEditAsync(int helpArticleId)
    {
        var article = await _context.HelpArticles
            .Include(a => a.Translations)
            .FirstOrDefaultAsync(a => a.HelpArticleId == helpArticleId);

        if (article == null) return null;

        var en = article.Translations.FirstOrDefault(t => t.LanguageCode == "en");
        var fa = article.Translations.FirstOrDefault(t => t.LanguageCode == "fa");

        var relatedIds = await _context.HelpRelatedArticles
            .Where(r => r.HelpArticleId == helpArticleId)
            .Select(r => r.RelatedHelpArticleId)
            .ToListAsync();

        return new HelpArticleAdminEditDto
        {
            HelpArticleId = article.HelpArticleId,
            Code = article.Code,
            HelpCategoryId = article.HelpCategoryId,
            ArticleType = article.ArticleType,
            HelpKey = article.HelpKey,
            RelevantRoles = article.RelevantRoles,
            DisplayOrder = article.DisplayOrder,
            IsPublished = article.IsPublished,
            TitleEn = en?.Title ?? "",
            SummaryEn = en?.Summary ?? "",
            ContentEn = en?.Content ?? "",
            SearchKeywordsEn = en?.SearchKeywords,
            TitleFa = fa?.Title ?? "",
            SummaryFa = fa?.Summary ?? "",
            ContentFa = fa?.Content ?? "",
            SearchKeywordsFa = fa?.SearchKeywords,
            RelatedArticleIds = relatedIds
        };
    }

    public async Task<int> CreateArticleAsync(HelpArticleAdminEditDto dto, int? actorEmployeeId)
    {
        ValidateBilingualCompleteness(dto);

        var duplicateCode = await _context.HelpArticles.AnyAsync(a => a.Code == dto.Code);
        if (duplicateCode)
            throw new InvalidOperationException($"کد مقاله «{dto.Code}» قبلاً استفاده شده است.");

        var article = new HelpArticle
        {
            Code = dto.Code.Trim(),
            HelpCategoryId = dto.HelpCategoryId,
            ArticleType = dto.ArticleType,
            HelpKey = string.IsNullOrWhiteSpace(dto.HelpKey) ? null : dto.HelpKey.Trim(),
            RelevantRoles = dto.RelevantRoles,
            DisplayOrder = dto.DisplayOrder,
            IsPublished = dto.IsPublished
        };

        article.Translations.Add(BuildTranslation("en", dto.TitleEn, dto.SummaryEn, dto.ContentEn, dto.SearchKeywordsEn));
        article.Translations.Add(BuildTranslation("fa", dto.TitleFa, dto.SummaryFa, dto.ContentFa, dto.SearchKeywordsFa));

        _context.HelpArticles.Add(article);
        await _context.SaveChangesAsync();

        await SyncRelatedArticlesAsync(article.HelpArticleId, dto.RelatedArticleIds);

        return article.HelpArticleId;
    }

    public async Task UpdateArticleAsync(int helpArticleId, HelpArticleAdminEditDto dto)
    {
        ValidateBilingualCompleteness(dto);

        var article = await _context.HelpArticles
            .Include(a => a.Translations)
            .FirstOrDefaultAsync(a => a.HelpArticleId == helpArticleId);

        if (article == null) throw new InvalidOperationException("مقاله یافت نشد.");

        var duplicateCode = await _context.HelpArticles
            .AnyAsync(a => a.HelpArticleId != helpArticleId && a.Code == dto.Code);
        if (duplicateCode)
            throw new InvalidOperationException($"کد مقاله «{dto.Code}» قبلاً برای مقاله‌ی دیگری استفاده شده است.");

        article.Code = dto.Code.Trim();
        article.HelpCategoryId = dto.HelpCategoryId;
        article.ArticleType = dto.ArticleType;
        article.HelpKey = string.IsNullOrWhiteSpace(dto.HelpKey) ? null : dto.HelpKey.Trim();
        article.RelevantRoles = dto.RelevantRoles;
        article.DisplayOrder = dto.DisplayOrder;
        article.IsPublished = dto.IsPublished;
        article.ModifiedDate = DateTime.Now;

        UpsertTranslation(article, "en", dto.TitleEn, dto.SummaryEn, dto.ContentEn, dto.SearchKeywordsEn);
        UpsertTranslation(article, "fa", dto.TitleFa, dto.SummaryFa, dto.ContentFa, dto.SearchKeywordsFa);

        await _context.SaveChangesAsync();

        await SyncRelatedArticlesAsync(helpArticleId, dto.RelatedArticleIds);
    }

    public async Task SetPublishedAsync(int helpArticleId, bool isPublished)
    {
        var article = await _context.HelpArticles.FirstOrDefaultAsync(a => a.HelpArticleId == helpArticleId);
        if (article == null) throw new InvalidOperationException("مقاله یافت نشد.");

        article.IsPublished = isPublished;
        article.ModifiedDate = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteArticleAsync(int helpArticleId)
    {
        var article = await _context.HelpArticles
            .Include(a => a.Translations)
            .Include(a => a.RelatedArticles)
            .FirstOrDefaultAsync(a => a.HelpArticleId == helpArticleId);

        if (article == null) return;

        var incomingRelations = await _context.HelpRelatedArticles
            .Where(r => r.RelatedHelpArticleId == helpArticleId)
            .ToListAsync();

        _context.HelpRelatedArticles.RemoveRange(incomingRelations);
        _context.HelpRelatedArticles.RemoveRange(article.RelatedArticles);
        _context.HelpArticleTranslations.RemoveRange(article.Translations);
        _context.HelpArticles.Remove(article);

        await _context.SaveChangesAsync();
    }

    // ============ Helpers ============

    private static void ValidateBilingualCompleteness(HelpArticleAdminEditDto dto)
    {
        // طبق بخش ۳ سند («NON-NEGOTIABLE»): یک مقاله نمی‌تواند فقط یک زبان داشته باشد - این
        // قانون همین‌جا، در لایه‌ی سرویس، اجرا می‌شود (نه فقط یک قرارداد مستندسازی)
        if (string.IsNullOrWhiteSpace(dto.Code))
            throw new InvalidOperationException("کد مقاله الزامی است.");
        if (string.IsNullOrWhiteSpace(dto.TitleEn) || string.IsNullOrWhiteSpace(dto.SummaryEn) || string.IsNullOrWhiteSpace(dto.ContentEn))
            throw new InvalidOperationException("نسخه‌ی انگلیسی (عنوان/خلاصه/متن) الزامی است - این سیستم راهنما دوزبانه است، نه انگلیسی با ترجمه‌ی بعدی.");
        if (string.IsNullOrWhiteSpace(dto.TitleFa) || string.IsNullOrWhiteSpace(dto.SummaryFa) || string.IsNullOrWhiteSpace(dto.ContentFa))
            throw new InvalidOperationException("نسخه‌ی فارسی (عنوان/خلاصه/متن) الزامی است - این سیستم راهنما دوزبانه است، نه فارسی با ترجمه‌ی بعدی.");
    }

    private HelpArticleTranslation BuildTranslation(string lang, string title, string summary, string content, string? keywords) =>
        new()
        {
            LanguageCode = lang,
            Title = title.Trim(),
            Summary = summary.Trim(),
            Content = _sanitizer.Sanitize(content),
            SearchKeywords = keywords
        };

    private void UpsertTranslation(HelpArticle article, string lang, string title, string summary, string content, string? keywords)
    {
        var translation = article.Translations.FirstOrDefault(t => t.LanguageCode == lang);
        if (translation == null)
        {
            article.Translations.Add(BuildTranslation(lang, title, summary, content, keywords));
            return;
        }

        translation.Title = title.Trim();
        translation.Summary = summary.Trim();
        translation.Content = _sanitizer.Sanitize(content);
        translation.SearchKeywords = keywords;
    }

    private async Task SyncRelatedArticlesAsync(int helpArticleId, List<int> relatedArticleIds)
    {
        var existing = await _context.HelpRelatedArticles.Where(r => r.HelpArticleId == helpArticleId).ToListAsync();
        _context.HelpRelatedArticles.RemoveRange(existing);

        foreach (var relatedId in relatedArticleIds.Distinct().Where(id => id != helpArticleId))
        {
            _context.HelpRelatedArticles.Add(new HelpRelatedArticle
            {
                HelpArticleId = helpArticleId,
                RelatedHelpArticleId = relatedId
            });
        }

        await _context.SaveChangesAsync();
    }

    private static HelpArticleSummaryDto? ToSummaryDto(HelpArticle article, string lang, bool allowFallback = true)
    {
        var translation = article.Translations.FirstOrDefault(t => t.LanguageCode == lang)
            ?? (allowFallback ? article.Translations.FirstOrDefault() : null);

        if (translation == null) return null;

        return new HelpArticleSummaryDto
        {
            HelpArticleId = article.HelpArticleId,
            Code = article.Code,
            ArticleType = article.ArticleType,
            HelpKey = article.HelpKey,
            HelpCategoryId = article.HelpCategoryId,
            CategoryCode = article.Category?.Code ?? "",
            CategoryName = lang == "fa" ? article.Category?.NameFa ?? "" : article.Category?.NameEn ?? "",
            Title = translation.Title,
            Summary = translation.Summary,
            IsPublished = article.IsPublished,
            DisplayOrder = article.DisplayOrder,
            RelevantRoles = article.RelevantRoles
        };
    }

    private static HelpArticleDetailDto ToDetailDto(HelpArticle article, string lang)
    {
        var translation = article.Translations.FirstOrDefault(t => t.LanguageCode == lang)
            ?? article.Translations.First();

        var related = article.RelatedArticles
            .Select(r => r.RelatedArticle)
            .Where(r => r != null && r.IsPublished)
            .Select(r => ToSummaryDto(r!, lang))
            .Where(x => x != null)
            .Cast<HelpArticleSummaryDto>()
            .ToList();

        return new HelpArticleDetailDto
        {
            HelpArticleId = article.HelpArticleId,
            Code = article.Code,
            ArticleType = article.ArticleType,
            HelpKey = article.HelpKey,
            HelpCategoryId = article.HelpCategoryId,
            CategoryCode = article.Category?.Code ?? "",
            CategoryName = lang == "fa" ? article.Category?.NameFa ?? "" : article.Category?.NameEn ?? "",
            Title = translation.Title,
            Summary = translation.Summary,
            IsPublished = article.IsPublished,
            DisplayOrder = article.DisplayOrder,
            RelevantRoles = article.RelevantRoles,
            LanguageCode = translation.LanguageCode,
            Content = translation.Content,
            SearchKeywords = translation.SearchKeywords,
            ModifiedDate = article.ModifiedDate ?? article.CreatedDate,
            RelatedArticles = related
        };
    }
}
