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

    public ICollection<WorkflowTransition> OutgoingTransitions { get; set; } = new List<WorkflowTransition>();
    public ICollection<WorkflowTransition> IncomingTransitions { get; set; } = new List<WorkflowTransition>();
}
