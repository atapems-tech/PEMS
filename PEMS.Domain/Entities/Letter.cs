using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// ماژول مکاتبات رسمی (Official Correspondence). یک Entity واحد برای هر سه نوع (Incoming،
/// Outgoing، Internal Memo) با فیلد LetterType - به‌جای سه جدول تقریباً یکسان، طبق همان اصل
/// یکپارچه‌سازی که در ProcurementPackage/Document Core استفاده شد.
/// </summary>
public class Letter : BaseEntity
{
    [Key]
    public int LetterId { get; set; }

    // Incoming, Outgoing, Internal
    public string LetterType { get; set; } = string.Empty;

    public string LetterNumber { get; set; } = string.Empty;

    public DateTime LetterDate { get; set; } = DateTime.Now;

    // برای Incoming: فرستنده‌ی بیرونی؛ برای Outgoing: گیرنده‌ی بیرونی؛ برای Internal هردو آزاد است
    public string? Sender { get; set; }

    public string? Receiver { get; set; }

    // برای مکاتبات داخلی، ارتباط مستقیم با گیرنده‌ی داخلی (اختیاری)
    public int? ReceiverDepartmentId { get; set; }
    public Department? ReceiverDepartment { get; set; }

    public int? ReceiverEmployeeId { get; set; }
    public Employee? ReceiverEmployee { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string? Body { get; set; }

    // Low, Normal, High, Urgent
    public string Priority { get; set; } = "Normal";

    // Public, Internal, Confidential, Restricted, HighlyConfidential
    public string ConfidentialityLevel { get; set; } = "Internal";

    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    public int? DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    // پیوند مستقیم دوطرفه با یک مدرک مهندسی/Vendor مشخص (نه فقط پروژه/دیسیپلین مشترک)
    public int? RelatedDocumentId { get; set; }
    public Document? RelatedDocument { get; set; }

    // پیوند مستقیم دوطرفه با یک Material Requisition مشخص
    public int? RelatedMRId { get; set; }
    public MaterialRequisition? RelatedMR { get; set; }

    // Incoming: Registered, UnderReview, Referred, Closed
    // Outgoing: Draft, PendingApproval, Approved, Rejected, Issued
    // Internal: Sent, Acknowledged, Closed
    public string Status { get; set; } = "Registered";

    public int? RegisteredById { get; set; }

    public DateTime? ApprovedDate { get; set; }
    public int? ApprovedById { get; set; }

    // پیوند به نامه‌ی والد در یک زنجیره‌ی پاسخ (Thread View) - طبق نمونه‌ی کارتابل مرجع
    public int? ParentLetterId { get; set; }
    public Letter? ParentLetter { get; set; }

    public DateTime? IssuedDate { get; set; }

    // تایید الکترونیکی سبک (نام تایپ‌شده + مهر زمانی) - نه امضای PKI واقعی
    public string? SignedByName { get; set; }
    public DateTime? SignedDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<DocumentAttachment> Attachments { get; set; } = new List<DocumentAttachment>();

    public ICollection<LetterReferral> Referrals { get; set; } = new List<LetterReferral>();

    public ICollection<LetterRecipient> Recipients { get; set; } = new List<LetterRecipient>();

    public ICollection<LetterTag> Tags { get; set; } = new List<LetterTag>();
}
