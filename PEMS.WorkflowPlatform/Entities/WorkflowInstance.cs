namespace PEMS.WorkflowPlatform.Entities;

/// <summary>
/// یک نمونه‌ی در حال اجرای گردش‌کار برای یک رکورد کسب‌وکاری مشخص.
/// طبق سند Baseline: موتور هرگز نباید بداند BusinessObjectId به کدام جدول واقعی اشاره دارد -
/// این مسئولیت لایه‌ی بالاتر (سرویس ماژول، مثلاً Engineering Document) است که این کلید را
/// تفسیر کند.
/// </summary>
public class WorkflowInstance
{
    public int WorkflowInstanceId { get; set; }

    public int WorkflowVersionId { get; set; }
    public WorkflowVersion? Version { get; set; }

    // شناسه‌ی ماژول مصرف‌کننده و نوع رکورد - مثلاً Module="EngineeringDocument",
    // BusinessObjectType="Document". این دو با هم، همراه BusinessObjectId، منحصربه‌فرد کافی است
    public string Module { get; set; } = "";
    public string BusinessObjectType { get; set; } = "";
    public int BusinessObjectId { get; set; }

    public int? ProjectId { get; set; }

    public int? CurrentStageId { get; set; }
    public WorkflowStage? CurrentStage { get; set; }

    public int? CurrentOwnerId { get; set; }

    // Running | Completed | Rejected | Cancelled | Suspended
    public string Status { get; set; } = "Running";

    public string? Priority { get; set; }

    public DateTime StartDate { get; set; } = DateTime.Now;
    public DateTime? CompletionDate { get; set; }

    public int? InitiatedById { get; set; }

    // --- Phase 1 Hardening (additive; null برای Instance های قدیمی) ---
    /// <summary>Binding صریحی که این Instance را ایجاد کرده (null = مسیر Legacy با Module+Category).</summary>
    public int? WorkflowBindingId { get; set; }

    /// <summary>کلید صریح ارتباط با موجودیت: "{Module}|{BusinessObjectType}|{BusinessObjectId}".</summary>
    public string? EntityKey { get; set; }

    public ICollection<WorkflowInstanceStage> InstanceStages { get; set; } = new List<WorkflowInstanceStage>();
    public ICollection<WorkflowHistory> History { get; set; } = new List<WorkflowHistory>();
}
