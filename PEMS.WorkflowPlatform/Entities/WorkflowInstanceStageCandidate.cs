using System.ComponentModel.DataAnnotations;

namespace PEMS.WorkflowPlatform.Entities;

/// <summary>
/// Snapshot تغییرناپذیر «چه کسی هنگام ورود به مرحله مجاز به اقدام بود و چرا». مجوز اقدام روی مراحل مبتنی بر مسئولیت
/// از همین جدول (نه از داده‌ی زنده‌ی سازمان) خوانده می‌شود تا تاریخچه با جابه‌جایی/بازنشستگی افراد تغییر نکند.
/// IsSelected=false یعنی واجد شرایط بوده ولی راهبرد SingleActor او را انتخاب نکرده.
/// </summary>
public class WorkflowInstanceStageCandidate
{
    public int WorkflowInstanceStageCandidateId { get; set; }

    public int WorkflowInstanceStageId { get; set; }
    public WorkflowInstanceStage? InstanceStage { get; set; }

    public int EmployeeId { get; set; }

    public bool IsSelected { get; set; } = true;

    /// <summary>دلایل واجد شرایط‌بودن (Position/Role/Scope/Commodity) به‌صورت متن کوتاه.</summary>
    [MaxLength(500)]
    public string? Reason { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
