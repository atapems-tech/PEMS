using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// ==============================================================================
/// طبق «PEMS Help System — Phase 3»: یک Entity واحد و پراگماتیک برای همه‌ی انواع محتوای
/// بصریِ راهنما (Workflow Diagram/Process Flowchart/Lifecycle Diagram/Decision Tree/
/// Step Guide/Entity Relationship) - نه ۶ جدول جدا. دقیقاً همان اصلی که HelpArticle خودش در
/// فاز ۱ برای ArticleType استفاده کرد: یک فیلد نوع + یک تعریف ساختاریافته، نه Entity تکراری
/// به‌ازای هر نوع.
///
/// دو حالت تعریف، بسته به VisualType:
///   ۱) WorkflowDiagram: SourceWorkflowModule/SourceWorkflowCategory را نگه می‌دارد - خودِ
///      نمودار هرگز در دیتابیس ذخیره نمی‌شود؛ در لحظه‌ی نمایش از روی جداول واقعیِ موتور
///      گردش‌کار (WorkflowStage/WorkflowTransition) ساخته می‌شود (IHelpWorkflowDiagramService) -
///      یعنی هرگز نمی‌تواند از گردش‌کار واقعی عقب بیفتد یا با آن ناسازگار شود.
///   ۲) بقیه‌ی انواع (دستی، چون معادل موتور گردش‌کاری در دیتابیس ندارند): DefinitionJson یک
///      گراف ساده‌ی Node/Edge با برچسب دوزبانه‌ی درون‌خطی (نه یک جدول ترجمه‌ی جدا - طبق بخش ۷
///      سند «Prefer a maintainable approach that avoids duplicating the entire graph»).
/// ==============================================================================
/// </summary>
public class HelpVisual : BaseEntity
{
    public int HelpVisualId { get; set; }

    public int HelpArticleId { get; set; }
    public HelpArticle? Article { get; set; }

    // WorkflowDiagram | ProcessFlowchart | LifecycleDiagram | DecisionTree | StepGuide | EntityRelationship
    public string VisualType { get; set; } = "WorkflowDiagram";

    public int DisplayOrder { get; set; }

    public bool IsPublished { get; set; }

    // فقط برای VisualType=WorkflowDiagram - Module/Category واقعیِ WorkflowDefinition (طبق
    // بخش ۸ سند: «Workflow Graph Adapter» - منبع حقیقت، نه یک کپی دستی)
    public string? SourceWorkflowModule { get; set; }
    public string? SourceWorkflowCategory { get; set; }

    // فقط برای انواع دستی (غیر WorkflowDiagram) - JSON ساختاریافته‌ی { nodes: [...], edges: [...] }
    // با برچسب دوزبانه‌ی درون‌خطی روی هر Node/Edge؛ هرگز HTML خام نیست - فقط متن ساده که با
    // اعتبارسنجی/Encode خودکار Razor نمایش داده می‌شود (بدون نیاز به Sanitizer جداگانه، چون
    // اصلاً HTML وارد نمی‌شود)
    public string? DefinitionJson { get; set; }

    public string CaptionEn { get; set; } = "";
    public string CaptionFa { get; set; } = "";

    public string? LegendEn { get; set; }
    public string? LegendFa { get; set; }

    // طبق بخش ۱۲ سند (Accessibility): توضیح متنیِ مستقلِ هر نمودار برای صفحه‌خوان‌ها - جدا از
    // Caption (که کوتاه است)، این می‌تواند کامل‌تر باشد
    public string AccessibilityDescriptionEn { get; set; } = "";
    public string AccessibilityDescriptionFa { get; set; } = "";

    // طبق بخش ۴ سند («Optional related HelpKey») - لینک اختیاری به یک HelpKey دیگر (مثلاً از
    // یک ERD به صفحه‌ی جزئیات همان موجودیت)
    public string? RelatedHelpKey { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? ModifiedDate { get; set; }
}
