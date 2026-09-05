using System.ComponentModel.DataAnnotations;

namespace PEMS.WorkflowPlatform.Entities;

/// <summary>
/// وضعیت یک بازبین/دیسیپلین مشخص در یک مرحله‌ی Parallel از یک Instance. هر بازبین رکورد
/// مستقل خودش را دارد - این دقیقاً چیزی است که در ممیزی سند Baseline به‌عنوان نیاز به
/// جلوگیری از Race Condition در بازبینی موازی مطرح شد.
/// </summary>
public class WorkflowInstanceStage
{
    public int WorkflowInstanceStageId { get; set; }

    public int WorkflowInstanceId { get; set; }
    public WorkflowInstance? Instance { get; set; }

    public int WorkflowStageId { get; set; }
    public WorkflowStage? Stage { get; set; }

    public int? AssignedToEmployeeId { get; set; }
    public string? AssignedToRole { get; set; }

    // Pending | Approved | Rejected | Skipped | Delegated
    public string Status { get; set; } = "Pending";

    public string? Decision { get; set; }
    public string? Comment { get; set; }

    public DateTime AssignedDate { get; set; } = DateTime.Now;
    public DateTime? ActionDate { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
