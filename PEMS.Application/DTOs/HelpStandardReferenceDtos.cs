namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Help Phase 4 — Increment 3A»: فرم ادمین برای ساخت/ویرایش یک Standard Reference -
/// هم فیلدهای مقاله‌ی معمولی (چون یک HelpArticle با ArticleType=StandardReference هم می‌سازد/
/// ویرایش می‌کند - دقیقاً با استفاده از IHelpService موجود، نه منطق تکراری) و هم فیلدهای
/// ساختاریافته‌ی مخصوص استاندارد، در یک فرم واحد.
/// </summary>
public class HelpStandardReferenceAdminEditDto
{
    public int HelpStandardReferenceId { get; set; }
    public int HelpArticleId { get; set; }

    // ---- فیلدهای مقاله (از طریق IHelpService ساخته/ویرایش می‌شوند) ----
    public string Code { get; set; } = "";
    public int HelpCategoryId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }

    public string TitleEn { get; set; } = "";
    public string SummaryEn { get; set; } = "";
    public string ContentEn { get; set; } = "";
    public string? SearchKeywordsEn { get; set; }

    public string TitleFa { get; set; } = "";
    public string SummaryFa { get; set; } = "";
    public string ContentFa { get; set; } = "";
    public string? SearchKeywordsFa { get; set; }

    public List<int> RelatedArticleIds { get; set; } = new();

    // ---- فیلدهای ساختاریافته‌ی Standard Reference ----
    public string Identifier { get; set; } = "";
    public string IssuingOrganization { get; set; } = "";
    public string? EditionOrYear { get; set; }
    public string? OfficialReferenceUrl { get; set; }
    public string? RelatedModule { get; set; }
    public string ApplicabilityStatus { get; set; } = "NotVerified";
    public string VerificationStatus { get; set; } = "NotVerified";
    public string PEMSImplementationStatus { get; set; } = "NotImplemented";
    public string? EvidencePath { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// یک ردیف ماتریس استانداردها - برای صفحه‌ی ادمین و برای Export به CSV
/// </summary>
public class HelpStandardReferenceMatrixRowDto
{
    public int HelpStandardReferenceId { get; set; }
    public int HelpArticleId { get; set; }
    public string ArticleCode { get; set; } = "";
    public bool IsPublished { get; set; }

    public string Identifier { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public string TitleFa { get; set; } = "";
    public string IssuingOrganization { get; set; } = "";
    public string? EditionOrYear { get; set; }
    public string? OfficialReferenceUrl { get; set; }
    public string? RelatedModule { get; set; }
    public string ApplicabilityStatus { get; set; } = "";
    public string VerificationStatus { get; set; } = "";
    public string PEMSImplementationStatus { get; set; } = "";
    public string? EvidencePath { get; set; }
    public string? Notes { get; set; }
}
