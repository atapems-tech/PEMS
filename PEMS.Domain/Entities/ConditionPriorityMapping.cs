using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS — Equipment Passport Audit & CBM» فاز ۱ ممیزی §3.7: قبل از این Entity،
/// FindingController.RaiseWorkRequest یک نگاشت Condition→Priority را مستقیم در کد C# Hard-Code
/// کرده بود (Critical/Reject→Critical، Alarm/Poor→High، Warning→Medium، پیش‌فرض→Medium) - دقیقاً
/// همون نقضی که سند CBM صریحاً منع می‌کند: «Do not automatically equate Critical=Priority X
/// unless such mapping is explicitly configured and approved». این جدول همون نگاشت قبلی را
/// (بدون تغییر رفتار پیش‌فرض) به یک پیکربندی قابل‌مشاهده/قابل‌ویرایش/قابل‌ممیزی تبدیل می‌کند.
/// </summary>
public class ConditionPriorityMapping : BaseEntity
{
    public int ConditionPriorityMappingId { get; set; }

    // Normal, Warning, Alarm, Critical, NotEvaluated, RequiresEngineeringReview (از CM)،
    // یا Good/Fair/Poor/Reject (از Inspection) - عمداً رشته‌ی آزاد، نه Enum ثابت، چون منابع
    // مختلف (CM/Inspection) واژگان وضعیت متفاوتی دارند.
    public string Condition { get; set; } = string.Empty;

    // Low, Medium, High, Critical - همون مقادیر WorkRequest.Priority/WorkOrder.Priority موجود
    public string Priority { get; set; } = "Medium";

    public bool IsSystemDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
