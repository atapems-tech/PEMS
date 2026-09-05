namespace PEMS.WorkflowPlatform.Entities;

/// <summary>
/// نسخه‌ی مشخصی از یک WorkflowDefinition. طبق سند Baseline: هرگز Instance های در حال اجرا
/// به‌صورت خودکار به نسخه‌ی جدید منتقل نمی‌شوند - فقط Instance های جدید از آخرین نسخه‌ی
/// Published استفاده می‌کنند.
/// </summary>
public class WorkflowVersion
{
    public int WorkflowVersionId { get; set; }

    public int WorkflowDefinitionId { get; set; }
    public WorkflowDefinition? Definition { get; set; }

    public int VersionNumber { get; set; }

    // Draft: قابل‌ویرایش، هنوز قابل استفاده برای Instance جدید نیست
    // Published: فعال و قابل استفاده برای Instance های جدید
    // Retired: دیگر برای Instance جدید استفاده نمی‌شود، ولی Instance های قدیمی همچنان به آن ارجاع دارند
    public string Status { get; set; } = "Draft";

    public DateTime? EffectiveDate { get; set; }
    public DateTime? PublishedDate { get; set; }

    public int? PublishedById { get; set; }

    public ICollection<WorkflowStage> Stages { get; set; } = new List<WorkflowStage>();
    public ICollection<WorkflowTransition> Transitions { get; set; } = new List<WorkflowTransition>();
}
