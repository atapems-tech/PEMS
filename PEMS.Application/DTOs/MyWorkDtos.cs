namespace PEMS.Application.DTOs;

/// <summary>
/// یک «کارِ منتظر اقدام من» از کارتابل مشترک موتور گردش‌کار (IWorkflowRuntime.GetPendingTasksAsync).
/// این یک Task جدا نیست؛ همان مرحله‌ی Pending است که کاربر طبق قواعد خود موتور (نقش یا Snapshot کاندیدها + تفکیک وظایف) مجاز به اقدام روی آن است.
/// </summary>
public class MyApprovalItemDto
{
    public int WorkflowInstanceId { get; set; }
    public int WorkflowInstanceStageId { get; set; }
    public string Module { get; set; } = "";
    public string BusinessObjectType { get; set; } = "";
    public int BusinessObjectId { get; set; }
    public string StageName { get; set; } = "";
    public string? StageNameFa { get; set; }
    public DateTime AssignedDate { get; set; }

    /// <summary>سررسید SLA مرحله (AssignedDate + TimeoutHours)؛ null = مرحله SLA ندارد.</summary>
    public DateTime? DueDate { get; set; }
    public bool IsOverdue { get; set; }

    /// <summary>true = مستقیماً به خود کاربر ارجاع شده (کاندیدای انتخاب‌شده)؛ false = صف نقش.</summary>
    public bool IsPersonal { get; set; }
}

public class MyApprovalsSummaryDto
{
    public int Total { get; set; }
    public int Overdue { get; set; }
    public int Personal { get; set; }

    /// <summary>تعداد کارهایی که مرحله‌شان مهلت (SLA) پیکربندی‌شده دارد. 0 = هیچ SLA ای تعریف نشده؛ «عقب‌افتاده» در آن حالت معنا ندارد و نباید نمایش داده شود.</summary>
    public int WithSla { get; set; }

    /// <summary>تعداد منتظر اقدام به تفکیک ماژول (Module موتور گردش‌کار).</summary>
    public Dictionary<string, int> ByModule { get; set; } = new();
}

public class MyApprovalsPageDto
{
    public PagedResult<MyApprovalItemDto> Page { get; set; } = new();
    public MyApprovalsSummaryDto Summary { get; set; } = new();
    public string? Module { get; set; }
    public bool OverdueOnly { get; set; }
    public bool PersonalOnly { get; set; }
}
