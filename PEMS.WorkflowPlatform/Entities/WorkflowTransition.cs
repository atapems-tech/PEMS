namespace PEMS.WorkflowPlatform.Entities;

/// <summary>
/// تعریف می‌کند که با انجام یک اکشن مشخص از یک مرحله، به کدام مرحله باید رفت.
/// FromStageId=null یعنی گذار شروع (اولین مرحله). ToStageId=null یعنی پایان گردش‌کار.
/// </summary>
public class WorkflowTransition
{
    public int WorkflowTransitionId { get; set; }

    public int WorkflowVersionId { get; set; }
    public WorkflowVersion? Version { get; set; }

    public int? FromStageId { get; set; }
    public WorkflowStage? FromStage { get; set; }

    public int? ToStageId { get; set; }
    public WorkflowStage? ToStage { get; set; }

    // نوع اکشنی که این گذار را فعال می‌کند - Approve, Reject, Return, Skip, ...
    public string ActionType { get; set; } = "";

    // ==============================================================================
    // قلاب فاز B (Rule Engine): در فاز A فقط ذخیره می‌شود، ارزیابی نمی‌شود - یعنی اگر خالی
    // باشد، گذار همیشه فعال است. وقتی Rule Engine ساخته شد، این رشته توسط یک Expression
    // Evaluator امن (نه eval خام - طبق ریسک امنیتی که در ممیزی سند Rule Library مطرح شد)
    // ارزیابی خواهد شد.
    // ==============================================================================
    public string? ConditionExpression { get; set; }

    public int Priority { get; set; }
}
