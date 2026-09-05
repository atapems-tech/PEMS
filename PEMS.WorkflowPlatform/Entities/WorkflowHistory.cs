namespace PEMS.WorkflowPlatform.Entities;

/// <summary>
/// تاریخچه‌ی تغییرناپذیر هر اقدام گردش‌کار. طبق سند Baseline: "Nothing may ever be deleted."
/// این جدول هرگز UPDATE/DELETE نمی‌شود - فقط INSERT.
/// </summary>
public class WorkflowHistory
{
    public int WorkflowHistoryId { get; set; }

    public int WorkflowInstanceId { get; set; }
    public WorkflowInstance? Instance { get; set; }

    public int? FromStageId { get; set; }
    public int? ToStageId { get; set; }

    public string ActionType { get; set; } = "";
    public string? Decision { get; set; }
    public string? Comment { get; set; }
    public string? Reason { get; set; }

    // اگر این گذار توسط یک قانون (فاز B) فعال شده باشد، شناسه/نام قانون اینجا ثبت می‌شود
    public string? RuleApplied { get; set; }

    public int? UserId { get; set; }
    public string? Role { get; set; }

    public DateTime ActionDate { get; set; } = DateTime.Now;
    public int? DurationSeconds { get; set; }

    public string? IPAddress { get; set; }
    public string? Browser { get; set; }
}
