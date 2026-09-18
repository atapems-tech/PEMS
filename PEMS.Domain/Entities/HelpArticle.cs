using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// ==============================================================================
/// طبق «PEMS Bilingual Help & Knowledge System» بخش ۷ («Do not create unnecessary entities
/// merely to increase complexity. Use a pragmatic model»): سند درخواست ۱۵ نوع Entity را مثال
/// می‌زند (HelpFieldDefinition/HelpActionDefinition/HelpFaq/HelpGlossaryTerm/HelpBusinessRule/
/// HelpWorkflowGuide/HelpRoleGuide/...) - همه‌ی این‌ها از نظر داده دقیقاً یک شکل دارند (عنوان +
/// خلاصه + متن + کلیدواژه، به دو زبان)؛ تفاوتشان فقط «این مقاله درباره‌ی چه چیزی است» و
/// «کجای برنامه به آن وصل می‌شود». به‌جای ۹+ جدول تکراری، یک HelpArticle با ArticleType (نوع
/// محتوا) و HelpKey اختیاری (محل اتصال Context-Aware) همان نقش را می‌گیرد - دقیقاً همان اصلی
/// که در این ریپو برای PersonnelType/LossCategory/... به‌جای Enum/جدول جدا استفاده شده: رشته‌ی
/// ساده + اعتبارسنجی سمت سرور.
///
/// تاریخچه‌ی نسخه (bullet «Version»/«Revision history» سند): به‌جای یک HelpRevision موازی،
/// از AuditLog موجود پروژه استفاده می‌شود - چون HelpArticle/HelpArticleTranslation از BaseEntity
/// ارث می‌برند، PEMSDbContext.SaveChangesAsync هر تغییر را خودکار و رایگان در AuditLog ثبت
/// می‌کند (دقیقاً همان مکانیزم هر Entity دیگر پروژه).
/// ==============================================================================
/// </summary>
public class HelpArticle : BaseEntity
{
    public int HelpArticleId { get; set; }

    // شناسه‌ی پایدار، یکتا، مستقل از زبان - طبق بخش ۵ سند («Stable, Unique, Human-readable,
    // Independent of localized text, Safe to use in code»). هم برای HelpKey های Context-Aware
    // هم برای مقالات مستقل (FAQ/Glossary/...) که به یک صفحه‌ی خاص وصل نیستند استفاده می‌شود.
    public string Code { get; set; } = string.Empty;

    public int HelpCategoryId { get; set; }
    public HelpCategory? Category { get; set; }

    // Module | Page | Field | Action | Workflow | Faq | Glossary | Troubleshooting | RoleGuide
    public string ArticleType { get; set; } = "Module";

    // طبق بخش ۵ سند: کلید پایدار Context-Aware مثل "EngineeringDocuments.Details" یا
    // "EngineeringDocuments.Details.Field.DocumentNo". Null یعنی این مقاله مستقیماً به یک
    // صفحه/فیلد/اکشن وصل نیست (مثلاً یک مقاله‌ی Glossary یا FAQ عمومی) - فقط از طریق Help
    // Center (فهرست/جستجو) قابل‌دسترس است.
    public string? HelpKey { get; set; }

    // طبق بخش ۹ سند («optional role relevance»/«optional permission relevance») - PEMS نقش را
    // به‌صورت رشته‌ی ثابت (SystemRoles.*) مدل می‌کند، نه یک جدول Permission جدا برای Help؛ این‌جا
    // هم از همون الگوی رشته‌ی کاما-جدا استفاده شده (دقیقاً مثل WorkflowStage.ResponsibleRole).
    // Null/خالی یعنی مرتبط با همه‌ی نقش‌ها.
    public string? RelevantRoles { get; set; }

    public int DisplayOrder { get; set; }

    // پیش‌نویس/منتشرشده - طبق بخش ۸ سند. مقاله‌ی منتشرنشده فقط برای مدیران Help قابل‌مشاهده است.
    public bool IsPublished { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? ModifiedDate { get; set; }

    public ICollection<HelpArticleTranslation> Translations { get; set; } = new List<HelpArticleTranslation>();

    public ICollection<HelpRelatedArticle> RelatedArticles { get; set; } = new List<HelpRelatedArticle>();

    // طبق «PEMS Help System — Phase 3»
    public ICollection<HelpVisual> Visuals { get; set; } = new List<HelpVisual>();
}
