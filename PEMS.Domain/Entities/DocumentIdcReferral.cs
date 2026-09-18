namespace PEMS.Domain.Entities;

/// <summary>
/// ارجاع یک مدرک برای بازبینی IDC (Inter Discipline Check) به یک دیسیپلین مشخص. این تصمیم
/// را سازنده‌ی مدرک هنگام Submit می‌گیرد - نه یک قاعده‌ی ثابت سیستمی. هر ردیف یعنی «این
/// دیسیپلین باید این مدرک را بازبینی کند».
/// </summary>
public class DocumentIdcReferral
{
    public int DocumentIdcReferralId { get; set; }

    public int DocumentId { get; set; }

    public int DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public int? CreatedById { get; set; }

    // ==============================================================================
    // طبق «PEMS Engineering IDC Ownership Correction»: قبلاً نتیجه‌ی هر دیسیپلین فقط به‌صورت
    // گذرا در جداول موتور گردش‌کار عمومی (WorkflowInstanceStage) وجود داشت و هیچ‌وقت روی این
    // رکورد نوشته نمی‌شد - یعنی «نتیجه‌ی بازبینی این دیسیپلین» هیچ‌جای دائمی و صریح در مدل
    // دامنه نداشت. این فیلدها این رکورد را به رکورد واقعیِ «بازبینی مستقل هر دیسیپلین» تبدیل
    // می‌کنند - مستقل از وضعیت کل مدرک (Document.Status).
    // ==============================================================================

    // Pending | Approve | Comment | Reject - دقیقاً همون واژگانی که IdcRespondAsync می‌پذیرد
    public string Status { get; set; } = "Pending";

    public string? Comment { get; set; }

    public int? RespondedById { get; set; }

    public DateTime? RespondedDate { get; set; }
}
