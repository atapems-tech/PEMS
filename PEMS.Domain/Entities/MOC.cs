using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// ==============================================================================
/// Management of Change (MOC) - طبق فاز ۲ نقشه‌راه معماری و رویه‌ی استاندارد PSM/ایمنی
/// فرآیند در صنعت پتروشیمی: هیچ تغییری (دائم یا موقت) در تجهیز، فرآیند یا مدرک بدون گذر از
/// این چرخه رسمی نباید اعمال شود.
///
/// چرخه: درخواست → ارزیابی ریسک → تایید (چندمرحله‌ای اگر ریسک بالا باشد) → اجرا → بستن
/// (به‌روزرسانی مدارک + آموزش به‌عنوان چک‌لیست قبل از بستن نهایی، نه مراحل گردش‌کار جدا).
/// ==============================================================================
/// </summary>
public class MOC : BaseEntity
{
    public int MOCId { get; set; }

    public string MOCNumber { get; set; } = string.Empty;

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    // دیسیپلین درخواست‌کننده
    public int DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Reason { get; set; }

    // Permanent, Temporary
    public string ChangeType { get; set; } = "Permanent";

    // اگر Temporary باشد، تاریخ برگشت به حالت اولیه لازم است
    public DateTime? TemporaryExpiryDate { get; set; }

    // Low, Medium, High - طبق این سطح، مسیر تایید چندمرحله‌ای فعال/غیرفعال می‌شود
    public string RiskLevel { get; set; } = "Low";

    public int? RelatedDocumentId { get; set; }
    public Document? RelatedDocument { get; set; }

    public int? RelatedEquipmentId { get; set; }
    public Equipment? RelatedEquipment { get; set; }

    public int? RequestedById { get; set; }
    public DateTime RequestedDate { get; set; } = DateTime.Now;

    // ارزیابی ریسک
    public string? RiskAssessmentText { get; set; }
    public int? RiskAssessedById { get; set; }
    public DateTime? RiskAssessedDate { get; set; }

    // تایید نهایی (چه یک‌مرحله‌ای چه دومرحله‌ای)
    public int? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? RejectionReason { get; set; }

    // اجرا
    public string? ImplementationPlan { get; set; }
    public int? ImplementedById { get; set; }
    public DateTime? ImplementedDate { get; set; }

    // چک‌لیست پیش از بستن نهایی - طبق رویه‌ی استاندارد MOC
    public bool DocumentsUpdated { get; set; }
    public string? UpdatedDocumentsNote { get; set; }
    public bool TrainingRequired { get; set; }
    public bool TrainingCompleted { get; set; }

    // Draft, PendingRiskAssessment, PendingApproval, PendingSeniorApproval, Approved,
    // Rejected, Implementing, PendingClosure, Closed
    public string Status { get; set; } = "Draft";

    public int? ClosedById { get; set; }
    public DateTime? ClosedDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<DocumentAttachment> Attachments { get; set; } = new List<DocumentAttachment>();
}
