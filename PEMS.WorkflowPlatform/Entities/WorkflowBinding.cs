namespace PEMS.WorkflowPlatform.Entities;

/// <summary>
/// اتصال صریح و قطعی «ماژول + نوع موجودیت + زمینه (Context/Category)» به یک تعریف گردش‌کار.
/// اگر PinnedVersionId خالی باشد، آخرین نسخه‌ی Published تعریف استفاده می‌شود؛ اگر پر باشد همان نسخه
/// (باید Published باشد). چند Binding فعال هم‌زمان برای یک کلید، در بازه‌ی زمانی هم‌پوشان، ابهام است و
/// هرگز «اولین مورد» انتخاب نمی‌شود - خطای قطعی می‌دهد.
/// </summary>
public class WorkflowBinding
{
    public int WorkflowBindingId { get; set; }

    public string Module { get; set; } = "";
    public string EntityType { get; set; } = "";

    /// <summary>هم‌ارز WorkflowDefinition.Category؛ رشته‌ی خالی یعنی «بدون زمینه».</summary>
    public string Context { get; set; } = "";

    public int WorkflowDefinitionId { get; set; }
    public WorkflowDefinition? Definition { get; set; }

    public int? PinnedVersionId { get; set; }
    public WorkflowVersion? PinnedVersion { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public int? CreatedById { get; set; }
    public DateTime? DeactivatedDate { get; set; }
}
