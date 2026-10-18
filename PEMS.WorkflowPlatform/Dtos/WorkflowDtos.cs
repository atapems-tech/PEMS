namespace PEMS.WorkflowPlatform.Dtos;

public class WorkflowInstanceDto
{
    public int WorkflowInstanceId { get; set; }
    public string Module { get; set; } = "";
    public string BusinessObjectType { get; set; } = "";
    public int BusinessObjectId { get; set; }
    public string Status { get; set; } = "";
    public int? CurrentStageId { get; set; }
    public string? CurrentStageName { get; set; }
    public string? CurrentOwnerRole { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public List<WorkflowInstanceStageDto> PendingStages { get; set; } = new();
}

public class WorkflowInstanceStageDto
{
    public int WorkflowInstanceStageId { get; set; }
    public string StageName { get; set; } = "";
    public int? AssignedToEmployeeId { get; set; }
    public string? AssignedToRole { get; set; }
    public string Status { get; set; } = "";
    public DateTime AssignedDate { get; set; }
}

public class WorkflowHistoryDto
{
    public string? FromStageName { get; set; }
    public string? ToStageName { get; set; }
    public string ActionType { get; set; } = "";
    public string? Decision { get; set; }
    public string? Comment { get; set; }
    public int? UserId { get; set; }
    public string? Role { get; set; }
    public DateTime ActionDate { get; set; }
    public string? IPAddress { get; set; }
}

public class PendingTaskDto
{
    public int WorkflowInstanceId { get; set; }
    public int WorkflowInstanceStageId { get; set; }
    public string Module { get; set; } = "";
    public string BusinessObjectType { get; set; } = "";
    public int BusinessObjectId { get; set; }
    public string StageName { get; set; } = "";
    public string? StageNameFa { get; set; }
    public DateTime AssignedDate { get; set; }

    /// <summary>مهلت مرحله (ساعت) از تعریف Stage؛ null = بدون SLA. فقط برای نمایش (سررسید/عقب‌افتاده) - اجرا/Escalation نیست.</summary>
    public int? TimeoutHours { get; set; }

    /// <summary>AssignedDate + TimeoutHours (همان قاعده‌ی WorkflowReadModelService)؛ null = بدون SLA.</summary>
    public DateTime? DueDate { get; set; }

    public bool IsOverdue { get; set; }

    /// <summary>true = کاربر خودش یکی از کاندیدهای انتخاب‌شده‌ی این مرحله است (مرحله‌ی مبتنی بر مسئولیت)؛ false = صف نقش (هر دارنده‌ی نقش می‌تواند اقدام کند).</summary>
    public bool IsPersonal { get; set; }
}
