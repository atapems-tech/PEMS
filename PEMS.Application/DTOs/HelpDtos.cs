namespace PEMS.Application.DTOs;

public class HelpCategoryDto
{
    public int HelpCategoryId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? IconCssClass { get; set; }
    public int ArticleCount { get; set; }
}

public class HelpArticleSummaryDto
{
    public int HelpArticleId { get; set; }
    public string Code { get; set; } = "";
    public string ArticleType { get; set; } = "";
    public string? HelpKey { get; set; }
    public int HelpCategoryId { get; set; }
    public string CategoryCode { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public bool IsPublished { get; set; }
    public int DisplayOrder { get; set; }

    // طبق بخش ۹ سند: راهنما هرگز نباید محتوایی نشان دهد که کاربر مجاز به دیدنش نیست - این
    // فیلد فقط برای نمایش برچسب "Planned for role X" است، نه یک لایه‌ی امنیتی جایگزین RBAC
    public string? RelevantRoles { get; set; }
}

public class HelpArticleDetailDto : HelpArticleSummaryDto
{
    public string LanguageCode { get; set; } = "";
    public string Content { get; set; } = "";
    public string? SearchKeywords { get; set; }
    public DateTime ModifiedDate { get; set; }
    public List<HelpArticleSummaryDto> RelatedArticles { get; set; } = new();
}

/// <summary>
/// طبق بخش ۸ سند: فرم ادمین برای ساخت/ویرایش یک مقاله - هر دو ترجمه (fa/en) هم‌زمان، چون
/// «Persian and English Help must be implemented together from the beginning» حتی در تجربه‌ی
/// نویسندگی هم باید رعایت شود، نه فقط در محتوای نهایی.
/// </summary>
public class HelpArticleAdminEditDto
{
    public int HelpArticleId { get; set; }
    public string Code { get; set; } = "";
    public int HelpCategoryId { get; set; }
    public string ArticleType { get; set; } = "Module";
    public string? HelpKey { get; set; }
    public string? RelevantRoles { get; set; }
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
}
