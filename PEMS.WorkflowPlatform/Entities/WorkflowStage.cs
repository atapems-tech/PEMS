namespace PEMS.WorkflowPlatform.Entities;

/// <summary>
/// یک مرحله‌ی قابل‌تنظیم از یک نسخه‌ی گردش‌کار.
/// </summary>
public class WorkflowStage
{
    public int WorkflowStageId { get; set; }

    public int WorkflowVersionId { get; set; }
    public WorkflowVersion? Version { get; set; }

    public string Name { get; set; } = "";

    public int StageOrder { get; set; }

    // Sequential | Parallel
    public string StageType { get; set; } = "Sequential";

    // نقش مسئول این مرحله (رشته - باید با یکی از نقش‌های سیستم موجود (SystemRoles در PEMS.Persistence)
    // مطابقت داشته باشد؛ عمداً اینجا وابستگی مستقیم به آن پروژه گرفته نشده چون این پروژه باید مستقل بماند)
    public string? ResponsibleRole { get; set; }

    // برای مراحل Parallel: چگونه مرحله «کامل» تلقی می‌شود
    // AllApprovalsRequired | MandatoryRolesOnly | MinimumCount | FirstResponse
    public string ApprovalStrategy { get; set; } = "AllApprovalsRequired";

    public int? MinimumApprovalCount { get; set; }

    public bool IsMandatory { get; set; } = true;
    public bool IsSkippable { get; set; }
    public bool IsDelegatable { get; set; } = true;
    public bool IsEscalatable { get; set; }

    public int? TimeoutHours { get; set; }

    public string? Color { get; set; }
    public string? Icon { get; set; }
    public string? Description { get; set; }

    // --- Phase 1 Hardening (additive) ---
    /// <summary>تخصیص مستقیم به یک کارمند مشخص (اختیاری؛ در کنار ResponsibleRole).</summary>
    public int? ResponsibleEmployeeId { get; set; }

    /// <summary>نقش مقصد Escalation. فقط داده/Read-model است؛ اجرای خودکار Escalation در Phase 1 وجود ندارد.</summary>
    public string? EscalationRole { get; set; }

    /// <summary>کلید قالب اعلان. فقط Extension Point است؛ ارسال اعلان توسط پلتفرم انجام نمی‌شود.</summary>
    public string? NotificationKey { get; set; }

    // --- Phase 2 (additive, module-neutral) ---
    /// <summary>نام نمایشی فارسی مرحله (UI دوزبانه از روی داده، نه ثابت‌نویسی در View).</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? NameFa { get; set; }

    /// <summary>
    /// وضعیت «کسب‌وکاری» که هنگام ورود به این مرحله روی موجودیت ماژول نگاشت می‌شود (مثلاً ستون Status
    /// قدیمی که داشبوردها و گزارش‌ها هنوز می‌خوانند). خود پلتفرم هیچ معنایی برایش قائل نیست؛ ماژول تصمیم می‌گیرد.
    /// </summary>
    [System.ComponentModel.DataAnnotations.MaxLength(50)]
    public string? BusinessStatus { get; set; }

    // --- Enterprise Actor Resolution (additive) ---
    /// <summary>
    /// کلید «مسئولیت» کسب‌وکاری (مثلاً MaintenanceSupervisor). اگر تنظیم شود، بازیگران مجاز هنگام ورود به مرحله توسط
    /// IWorkflowActorResolutionHook (Enterprise Actor Resolver) از Position/Role/Scope/Commodity حل و Snapshot می‌شوند؛
    /// ResponsibleRole در این حالت نادیده گرفته می‌شود. این فیلد نقش (Identity Role) نیست.
    /// </summary>
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? ResponsibilityKey { get; set; }

    /// <summary>شرط ورود به مرحله (گرامر IWorkflowConditionEvaluator با Factهای ماژول). false ⇒ مرحله خودکار Skip می‌شود. خالی = همیشه.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? EntryCondition { get; set; }

    public ICollection<WorkflowTransition> OutgoingTransitions { get; set; } = new List<WorkflowTransition>();
    public ICollection<WorkflowTransition> IncomingTransitions { get; set; } = new List<WorkflowTransition>();
}
