namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Help System — Phase 3» بخش ۷ (Option B - گراف زبان‌خنثی + برچسب محلی‌شده):
/// هر Node/Edge برچسب هر دو زبان را هم‌زمان دارد - یک گراف، نه دو گراف تکراری.
/// </summary>
public class HelpVisualNodeDto
{
    public string Id { get; set; } = "";
    public string LabelEn { get; set; } = "";
    public string LabelFa { get; set; } = "";
    public string? IconCssClass { get; set; }
    // برای Decision Tree: فرزندان این گره (شاخه‌ها) - برای بقیه‌ی انواع استفاده نمی‌شود
    public List<string>? ChildNodeIds { get; set; }
}

public class HelpVisualEdgeDto
{
    public string FromNodeId { get; set; } = "";
    // Null یعنی این گذار به پایان/اتمام فرآیند می‌رسد (End)، نه به یک گره‌ی دیگر
    public string? ToNodeId { get; set; }
    public string LabelEn { get; set; } = "";
    public string LabelFa { get; set; } = "";
    // normal | reject | decision
    public string Kind { get; set; } = "normal";
}

public class HelpVisualDefinition
{
    public List<HelpVisualNodeDto> Nodes { get; set; } = new();
    public List<HelpVisualEdgeDto> Edges { get; set; } = new();
}

/// <summary>
/// خروجیِ آماده‌ی نمایش - چه از روی موتور واقعی گردش‌کار ساخته شده باشد (WorkflowDiagram)
/// چه از روی DefinitionJson دستی - View هرگز نباید تفاوت منبع را بداند.
/// </summary>
public class HelpVisualRenderDto
{
    public int HelpVisualId { get; set; }
    public string VisualType { get; set; } = "";
    public string LanguageCode { get; set; } = "en";
    public string Caption { get; set; } = "";
    public string? Legend { get; set; }
    public string AccessibilityDescription { get; set; } = "";
    public string? RelatedHelpKey { get; set; }

    public List<HelpVisualRenderNode> Nodes { get; set; } = new();
    public List<HelpVisualRenderEdge> Edges { get; set; } = new();

    // فقط برای WorkflowDiagram - برای شفافیت منبع (بخش ۱۶.E سند: «Accuracy Verification»)
    public string? SourceWorkflowModule { get; set; }
    public string? SourceWorkflowCategory { get; set; }
    public int? SourceWorkflowVersion { get; set; }
}

public class HelpVisualRenderNode
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string? IconCssClass { get; set; }
}

public class HelpVisualRenderEdge
{
    public string FromNodeId { get; set; } = "";
    public string? ToNodeId { get; set; }
    public string Label { get; set; } = "";
    public string Kind { get; set; } = "normal";
}

public class HelpVisualAdminEditDto
{
    public int HelpVisualId { get; set; }
    public int HelpArticleId { get; set; }
    public string VisualType { get; set; } = "WorkflowDiagram";
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }

    public string? SourceWorkflowModule { get; set; }
    public string? SourceWorkflowCategory { get; set; }

    // فرم ادمین این را به‌صورت متن ساده‌ی خط‌به‌خط می‌گیرد (نه JSON خام) - طبق بخش ۱۳ سند
    // («Do not allow unsafe arbitrary diagram code»). فرمت:
    //   NODE|id|labelEn|labelFa
    //   EDGE|fromId|toIdOrEmpty|labelEn|labelFa|kind
    public string? ManualDefinitionText { get; set; }

    public string CaptionEn { get; set; } = "";
    public string CaptionFa { get; set; } = "";
    public string? LegendEn { get; set; }
    public string? LegendFa { get; set; }
    public string AccessibilityDescriptionEn { get; set; } = "";
    public string AccessibilityDescriptionFa { get; set; } = "";
    public string? RelatedHelpKey { get; set; }
}
