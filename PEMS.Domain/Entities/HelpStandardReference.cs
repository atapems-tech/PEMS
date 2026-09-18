using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// ==============================================================================
/// طبق «PEMS Help Phase 4 — Increment 3A» بخش ۵/۶/۱۹ («Prefer the smallest viable additive
/// model» / «Stop and report... if a proposed new model duplicates existing functionality»):
/// این تنها Entity واقعاً جدیدِ این افزایش است. Concept و DisciplineGuide نیازی به Entity
/// جدید نداشتند - عیناً یک HelpArticle دیگر با ArticleType متفاوت هستند (محتوای دوزبانه‌ی آزاد،
/// بدون فیلد ساختاریافته‌ی اضافه). اما «Standard Reference» واقعاً فیلدهای ساختاریافته و
/// قابل‌پرس‌وجو دارد که در HTML آزاد HelpArticleTranslation.Content جا نمی‌گیرند: یک شناسه‌ی
/// یکتا (Identifier)، وضعیت‌های کنترل‌شده (ApplicabilityStatus/VerificationStatus/
/// PEMSImplementationStatus) که برای ماتریس استانداردها باید Query/Filter شوند، و یک لینک
/// رسمی. ذخیره‌ی این‌ها به‌صورت متن آزاد داخل HTML یعنی نه یکتایی قابل‌اجرا، نه فیلتر واقعی -
/// دقیقاً همان دلیلی که سند خودش می‌گوید «Avoid storing uncontrolled JSON or arbitrary HTML as
/// the primary knowledge structure» برای این بخش.
///
/// رابطه با HelpArticle: یک‌به‌یک، اختیاری (فقط وقتی HelpArticle.ArticleType=StandardReference
/// است مقدار دارد). محتوای روایی/دوزبانه (What it is / Why it matters / PEMS relevance /
/// Current coverage / Outside scope) همچنان در HelpArticleTranslation.Content ذخیره می‌شود -
/// دقیقاً همان مکانیزم بقیه‌ی مقالات، بدون تکرار.
/// ==============================================================================
/// </summary>
public class HelpStandardReference : BaseEntity
{
    public int HelpStandardReferenceId { get; set; }

    public int HelpArticleId { get; set; }
    public HelpArticle? Article { get; set; }

    // شناسه‌ی رسمی و یکتا - مثلاً "ISO 14224", "API 510", "IEC 61511"
    public string Identifier { get; set; } = string.Empty;

    public string IssuingOrganization { get; set; } = string.Empty;

    // متن آزاد - مثلاً "2016", "2015 (referenced in code); 2026 edition now current per ISO"
    public string? EditionOrYear { get; set; }

    public string? OfficialReferenceUrl { get; set; }

    // متن آزاد - مثلاً "Reliability & Maintenance", "Quality" - طبق تصمیم مستندشده در سند:
    // نه FK به جدول Discipline واقعی پروژه (آن جدول دیسیپلین‌های مهندسیِ سازمانی/پروژه‌ای
    // است - مثل مکانیک/برق - نه دسته‌بندی موضوعیِ استانداردها؛ اتصال این دو مفهومِ متفاوت به
    // هم یک رابطه‌ی جعلی می‌ساخت)
    public string? RelatedModule { get; set; }

    // Applicable | PotentiallyApplicable | ContextDependent | ReferenceOnly | NotVerified | NotApplicable
    public string ApplicabilityStatus { get; set; } = "NotVerified";

    // ==============================================================================
    // طبق «PEMS Help Phase 4 — Increment 3A.1» بخش ۶: این ۵ مقدار دقیقاً چه چیزی را تایید
    // می‌کنند - نه یک بُعد جدید، فقط توضیح روشنِ همان مدل موجود:
    //   OfficialSourceVerified     - عنوانِ رسمی و URL مستقیماً از سایت خودِ سازمانِ صادرکننده
    //                                 (iso.org / sae.org / api.org / iec.ch) در همین افزایش
    //                                 با WebSearch تایید شده - بالاترین سطح این ۵ مورد.
    //   PublisherMetadataVerified  - عنوان/سازمان/URL از نتایج جست‌وجوی وب (شامل صفحات ناشرانِ
    //                                 ثانویه مثل ANSI/webstore.iec.ch) تایید شده، نه لزوماً
    //                                 مستقیماً از دامنه‌ی اصلیِ سازمان.
    //   RepositoryMappingVerified - فقط ارتباط این استاندارد با یک قابلیت PEMS در برابر کدِ
    //                                 واقعیِ مخزن تایید شده - نه خودِ متادیتای استاندارد.
    //   ExpertReviewRequired      - نیازمند بازبینیِ یک متخصصِ دیسیپلین پیش از اتکای عملیاتی.
    //   NotVerified                - پیش‌فرض؛ هیچ‌کدام از موارد بالا انجام نشده.
    // OfficialSourceVerified | PublisherMetadataVerified | RepositoryMappingVerified | ExpertReviewRequired | NotVerified
    // ==============================================================================
    public string VerificationStatus { get; set; } = "NotVerified";

    // NotImplemented | PartiallyRelated | ConceptuallyMapped | ImplementedButNotComplianceVerified |
    // ComplianceEvidenceAvailable | NotApplicable | NotVerified
    // طبق بخش ۱۱ سند: "ComplianceEvidenceAvailable" فقط وقتی استفاده می‌شود که شاهد رسمیِ
    // واقعی در مخزن وجود داشته باشد - نه چون یک مقاله به این استاندارد لینک شده.
    public string PEMSImplementationStatus { get; set; } = "NotImplemented";

    // مسیر فایل/کلاس واقعی در مخزن که این نگاشت را توجیه می‌کند - مثلاً
    // "PEMS.Domain/Entities/RcmFmea.cs (FMEAItem.RiskPriorityNumber)"
    public string? EvidencePath { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? ModifiedDate { get; set; }
}
