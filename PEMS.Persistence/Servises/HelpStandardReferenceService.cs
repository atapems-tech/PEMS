using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// ==============================================================================
/// طبق «PEMS Help Phase 4 — Increment 3A»: به‌جای تکرار منطق ساخت/ویرایش/اعتبارسنجیِ مقاله
/// (که IHelpService از قبل به‌درستی انجام می‌دهد - الزامی‌بودن هر دو زبان، یکتاییِ Code، ...)،
/// این سرویس مستقیماً IHelpService را برای بخش «مقاله» فراخوانی می‌کند و فقط رکورد ساختاریافته‌ی
/// HelpStandardReference را روی همان مقاله مدیریت می‌کند. ArticleType همیشه صراحتاً
/// "StandardReference" ست می‌شود - این سرویس تنها راه ساخت این نوع مقاله است (فرم عمومی مقاله
/// این نوع را در فهرستش ندارد - طبق کامنت _HelpArticleForm.cshtml).
/// ==============================================================================
/// </summary>
public class HelpStandardReferenceService : IHelpStandardReferenceService
{
    private const string ArticleTypeStandardReference = "StandardReference";

    private static readonly string[] ValidApplicabilityStatuses =
        { "Applicable", "PotentiallyApplicable", "ContextDependent", "ReferenceOnly", "NotVerified", "NotApplicable" };

    private static readonly string[] ValidVerificationStatuses =
        { "OfficialSourceVerified", "PublisherMetadataVerified", "RepositoryMappingVerified", "ExpertReviewRequired", "NotVerified" };

    private static readonly string[] ValidImplementationStatuses =
        { "NotImplemented", "PartiallyRelated", "ConceptuallyMapped", "ImplementedButNotComplianceVerified", "ComplianceEvidenceAvailable", "NotApplicable", "NotVerified" };

    private readonly PEMSDbContext _context;
    private readonly IHelpService _helpService;

    public HelpStandardReferenceService(PEMSDbContext context, IHelpService helpService)
    {
        _context = context;
        _helpService = helpService;
    }

    public async Task<IReadOnlyList<HelpStandardReferenceMatrixRowDto>> GetMatrixAsync(bool includeUnpublished = true)
    {
        var query = _context.HelpStandardReferences
            .Include(s => s.Article!).ThenInclude(a => a.Translations)
            .AsQueryable();

        if (!includeUnpublished) query = query.Where(s => s.Article!.IsPublished);

        var rows = await query.OrderBy(s => s.Identifier).ToListAsync();

        return rows.Select(s =>
        {
            var en = s.Article!.Translations.FirstOrDefault(t => t.LanguageCode == "en");
            var fa = s.Article!.Translations.FirstOrDefault(t => t.LanguageCode == "fa");

            return new HelpStandardReferenceMatrixRowDto
            {
                HelpStandardReferenceId = s.HelpStandardReferenceId,
                HelpArticleId = s.HelpArticleId,
                ArticleCode = s.Article.Code,
                IsPublished = s.Article.IsPublished,
                Identifier = s.Identifier,
                TitleEn = en?.Title ?? "",
                TitleFa = fa?.Title ?? "",
                IssuingOrganization = s.IssuingOrganization,
                EditionOrYear = s.EditionOrYear,
                OfficialReferenceUrl = s.OfficialReferenceUrl,
                RelatedModule = s.RelatedModule,
                ApplicabilityStatus = s.ApplicabilityStatus,
                VerificationStatus = s.VerificationStatus,
                PEMSImplementationStatus = s.PEMSImplementationStatus,
                EvidencePath = s.EvidencePath,
                Notes = s.Notes
            };
        }).ToList();
    }

    public async Task<HelpStandardReferenceMatrixRowDto?> GetByArticleIdAsync(int helpArticleId)
    {
        var s = await _context.HelpStandardReferences
            .Include(x => x.Article!).ThenInclude(a => a.Translations)
            .FirstOrDefaultAsync(x => x.HelpArticleId == helpArticleId);

        if (s == null) return null;

        var en = s.Article!.Translations.FirstOrDefault(t => t.LanguageCode == "en");
        var fa = s.Article!.Translations.FirstOrDefault(t => t.LanguageCode == "fa");

        return new HelpStandardReferenceMatrixRowDto
        {
            HelpStandardReferenceId = s.HelpStandardReferenceId,
            HelpArticleId = s.HelpArticleId,
            ArticleCode = s.Article.Code,
            IsPublished = s.Article.IsPublished,
            Identifier = s.Identifier,
            TitleEn = en?.Title ?? "",
            TitleFa = fa?.Title ?? "",
            IssuingOrganization = s.IssuingOrganization,
            EditionOrYear = s.EditionOrYear,
            OfficialReferenceUrl = s.OfficialReferenceUrl,
            RelatedModule = s.RelatedModule,
            ApplicabilityStatus = s.ApplicabilityStatus,
            VerificationStatus = s.VerificationStatus,
            PEMSImplementationStatus = s.PEMSImplementationStatus,
            EvidencePath = s.EvidencePath,
            Notes = s.Notes
        };
    }

    public async Task<HelpStandardReferenceAdminEditDto?> GetForEditAsync(int helpStandardReferenceId)
    {
        var standard = await _context.HelpStandardReferences
            .Include(s => s.Article!).ThenInclude(a => a.Translations)
            .Include(s => s.Article!).ThenInclude(a => a.RelatedArticles)
            .FirstOrDefaultAsync(s => s.HelpStandardReferenceId == helpStandardReferenceId);

        if (standard == null) return null;

        var article = standard.Article!;
        var en = article.Translations.FirstOrDefault(t => t.LanguageCode == "en");
        var fa = article.Translations.FirstOrDefault(t => t.LanguageCode == "fa");

        return new HelpStandardReferenceAdminEditDto
        {
            HelpStandardReferenceId = standard.HelpStandardReferenceId,
            HelpArticleId = article.HelpArticleId,
            Code = article.Code,
            HelpCategoryId = article.HelpCategoryId,
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
            RelatedArticleIds = article.RelatedArticles.Select(r => r.RelatedHelpArticleId).ToList(),
            Identifier = standard.Identifier,
            IssuingOrganization = standard.IssuingOrganization,
            EditionOrYear = standard.EditionOrYear,
            OfficialReferenceUrl = standard.OfficialReferenceUrl,
            RelatedModule = standard.RelatedModule,
            ApplicabilityStatus = standard.ApplicabilityStatus,
            VerificationStatus = standard.VerificationStatus,
            PEMSImplementationStatus = standard.PEMSImplementationStatus,
            EvidencePath = standard.EvidencePath,
            Notes = standard.Notes
        };
    }

    public async Task<int> CreateAsync(HelpStandardReferenceAdminEditDto dto, int? actorEmployeeId)
    {
        Validate(dto);

        // طبق «PEMS Help Phase 4 — Increment 3A.1» بخش ۵ («Identifier normalization is
        // deterministic»): مقایسه‌ی یکتایی باید روی همان مقداری انجام شود که ذخیره می‌شود -
        // در غیر این‌صورت "  ISO 14224  " از این بررسی رد می‌شود (چون با مقدار trim-شده‌ی
        // ذخیره‌شده برابر نیست) و فقط توسط Unique Index سطح دیتابیس با یک SqlException خام،
        // نه پیام تاییدشده‌ی این متد، متوقف می‌شود.
        var normalizedIdentifier = NormalizeIdentifier(dto.Identifier);

        if (await _context.HelpStandardReferences.AnyAsync(x => x.Identifier == normalizedIdentifier))
            throw new InvalidOperationException($"استانداردی با شناسه‌ی «{normalizedIdentifier}» از قبل ثبت شده.");

        var articleId = await _helpService.CreateArticleAsync(ToArticleDto(dto), actorEmployeeId);

        _context.HelpStandardReferences.Add(new HelpStandardReference
        {
            HelpArticleId = articleId,
            Identifier = normalizedIdentifier,
            IssuingOrganization = dto.IssuingOrganization.Trim(),
            EditionOrYear = dto.EditionOrYear,
            OfficialReferenceUrl = dto.OfficialReferenceUrl,
            RelatedModule = dto.RelatedModule,
            ApplicabilityStatus = dto.ApplicabilityStatus,
            VerificationStatus = dto.VerificationStatus,
            PEMSImplementationStatus = dto.PEMSImplementationStatus,
            EvidencePath = dto.EvidencePath,
            Notes = dto.Notes,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        return articleId;
    }

    public async Task UpdateAsync(int helpStandardReferenceId, HelpStandardReferenceAdminEditDto dto)
    {
        Validate(dto);

        var standard = await _context.HelpStandardReferences.FirstOrDefaultAsync(x => x.HelpStandardReferenceId == helpStandardReferenceId);
        if (standard == null) throw new InvalidOperationException("Standard Reference یافت نشد.");

        var normalizedIdentifier = NormalizeIdentifier(dto.Identifier);

        if (await _context.HelpStandardReferences.AnyAsync(x => x.Identifier == normalizedIdentifier && x.HelpStandardReferenceId != helpStandardReferenceId))
            throw new InvalidOperationException($"استانداردی با شناسه‌ی «{normalizedIdentifier}» از قبل ثبت شده.");

        await _helpService.UpdateArticleAsync(standard.HelpArticleId, ToArticleDto(dto));

        standard.Identifier = normalizedIdentifier;
        standard.IssuingOrganization = dto.IssuingOrganization.Trim();
        standard.EditionOrYear = dto.EditionOrYear;
        standard.OfficialReferenceUrl = dto.OfficialReferenceUrl;
        standard.RelatedModule = dto.RelatedModule;
        standard.ApplicabilityStatus = dto.ApplicabilityStatus;
        standard.VerificationStatus = dto.VerificationStatus;
        standard.PEMSImplementationStatus = dto.PEMSImplementationStatus;
        standard.EvidencePath = dto.EvidencePath;
        standard.Notes = dto.Notes;
        standard.ModifiedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    public async Task SetPublishedAsync(int helpStandardReferenceId, bool isPublished)
    {
        var standard = await _context.HelpStandardReferences.FirstOrDefaultAsync(x => x.HelpStandardReferenceId == helpStandardReferenceId);
        if (standard == null) throw new InvalidOperationException("Standard Reference یافت نشد.");

        await _helpService.SetPublishedAsync(standard.HelpArticleId, isPublished);
    }

    public async Task DeleteAsync(int helpStandardReferenceId)
    {
        var standard = await _context.HelpStandardReferences.FirstOrDefaultAsync(x => x.HelpStandardReferenceId == helpStandardReferenceId);
        if (standard == null) return;

        // حذف خودِ مقاله کافی است - Cascade روی HelpArticleId رکورد Standard Reference را هم حذف می‌کند
        await _helpService.DeleteArticleAsync(standard.HelpArticleId);
    }

    // =========================
    // Helpers
    // =========================

    private static void Validate(HelpStandardReferenceAdminEditDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Identifier))
            throw new InvalidOperationException("شناسه‌ی استاندارد (Identifier) الزامی است.");

        if (string.IsNullOrWhiteSpace(dto.IssuingOrganization))
            throw new InvalidOperationException("سازمان صادرکننده (Issuing Organization) الزامی است.");

        if (!ValidApplicabilityStatuses.Contains(dto.ApplicabilityStatus))
            throw new InvalidOperationException($"ApplicabilityStatus نامعتبر است: {dto.ApplicabilityStatus}");

        if (!ValidVerificationStatuses.Contains(dto.VerificationStatus))
            throw new InvalidOperationException($"VerificationStatus نامعتبر است: {dto.VerificationStatus}");

        if (!ValidImplementationStatuses.Contains(dto.PEMSImplementationStatus))
            throw new InvalidOperationException($"PEMSImplementationStatus نامعتبر است: {dto.PEMSImplementationStatus}");

        // طبق بخش ۱۱ سند: "ComplianceEvidenceAvailable" فقط وقتی مجاز است که یک مسیر شاهدِ
        // واقعی در مخزن هم ثبت شده باشد - نه صرفاً یک ادعا بدون مرجع
        if (dto.PEMSImplementationStatus == "ComplianceEvidenceAvailable" && string.IsNullOrWhiteSpace(dto.EvidencePath))
            throw new InvalidOperationException("برای PEMSImplementationStatus=ComplianceEvidenceAvailable، ثبت EvidencePath (مسیر واقعی در مخزن) الزامی است.");

        // طبق «PEMS Help Phase 4 — Increment 3A.1» بخش ۱۰ (URL and Security Audit): این تنها
        // URL در کل مدل Help است که در یک ویژگی <a href> واقعی (نه صرفاً متن رمزگذاری‌شده)
        // رندر می‌شود - Razor مقدار را Encode می‌کند (از تزریق HTML جلوگیری می‌کند) اما Scheme
        // را اعتبارسنجی نمی‌کند؛ بدون این بررسی، یک مقدار "javascript:..." هم عیناً Encode و
        // به‌عنوان یک لینک قابل‌کلیک رندر می‌شد. فقط http/https مجاز است.
        if (!string.IsNullOrWhiteSpace(dto.OfficialReferenceUrl))
        {
            var isValidUrl = Uri.TryCreate(dto.OfficialReferenceUrl.Trim(), UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

            if (!isValidUrl)
                throw new InvalidOperationException("آدرس مرجع رسمی (Official Reference URL) باید یک URL مطلقِ http:// یا https:// معتبر باشد.");
        }
    }

    // طبق بخش ۵ سند («Identifier normalization is deterministic»): فقط فاصله‌های ابتدا/انتها و
    // رشته‌های فاصله‌ی داخلیِ تکراری نرمال می‌شوند - تفاوت بزرگی/کوچکی حروف از قبل توسط Collation
    // پیش‌فرض دیتابیس (SQL_Latin1_General_CP1_CI_AS - Case-Insensitive، تاییدشده با sqlcmd)
    // در سطح Unique Index و هر مقایسه‌ی EF-translated مدیریت می‌شود - نیازی به تکرار آن منطق در
    // C# نیست.
    private static string NormalizeIdentifier(string identifier) =>
        string.Join(" ", identifier.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static HelpArticleAdminEditDto ToArticleDto(HelpStandardReferenceAdminEditDto dto) => new()
    {
        HelpArticleId = dto.HelpArticleId,
        Code = dto.Code,
        HelpCategoryId = dto.HelpCategoryId,
        ArticleType = ArticleTypeStandardReference,
        HelpKey = null,
        RelevantRoles = null,
        DisplayOrder = dto.DisplayOrder,
        IsPublished = dto.IsPublished,
        TitleEn = dto.TitleEn,
        SummaryEn = dto.SummaryEn,
        ContentEn = dto.ContentEn,
        SearchKeywordsEn = dto.SearchKeywordsEn,
        TitleFa = dto.TitleFa,
        SummaryFa = dto.SummaryFa,
        ContentFa = dto.ContentFa,
        SearchKeywordsFa = dto.SearchKeywordsFa,
        RelatedArticleIds = dto.RelatedArticleIds
    };
}
