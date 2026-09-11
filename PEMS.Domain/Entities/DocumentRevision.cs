using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class DocumentRevision
{
    [Key]
    public int RevisionId { get; set; }


    public int DocumentId { get; set; }

    public EngineeringDocument? Document { get; set; }



    // Revision Number
    public string RevisionNo { get; set; } = "00";


    // Previous Revision
    public string? PreviousRevisionNo { get; set; }



    // Draft / Review / Approved / Rejected
    public string Status { get; set; } = "Draft";



    public string? IssuePurpose { get; set; }



    public string? Comment { get; set; }



    public DateTime RevisionDate { get; set; }
        = DateTime.Now;



    // Prepared By
    public int? PreparedById { get; set; }

    public Employee? PreparedBy { get; set; }



    // Checked By
    public int? CheckedById { get; set; }

    public Employee? CheckedBy { get; set; }



    // Approved By
    public int? ApprovedById { get; set; }

    public Employee? ApprovedBy { get; set; }


    // طبق بازبینی معماری (بند ۷ - Revision استاندارد): فیلدهای جدید

    // چرا این رویژن ایجاد شد (Design Change, Client Comment, Vendor Update, ...)
    public string? Reason { get; set; }

    public string? ChangeDescription { get; set; }

    // نظر/کامنت وندور که باعث این رویژن شد - طبق بررسی، این فیلد اصلاً وجود نداشت
    public string? VendorComment { get; set; }

    public int? ReviewerId { get; set; }
    public Employee? Reviewer { get; set; }

    public int? CreatedById { get; set; }
    public Employee? CreatedBy { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public DateTime? IssuedDate { get; set; }

    public DateTime? EffectiveDate { get; set; }

    // رویژن قبلی که این رویژن جایگزینش شده (زنجیره‌ی کامل تاریخچه)
    public int? SupersededRevisionId { get; set; }
    public DocumentRevision? SupersededRevision { get; set; }

    // خلاصه‌ای که AI از تفاوت این رویژن با رویژن قبلی تولید می‌کند
    public string? AIRevisionSummary { get; set; }


    public bool IsActive { get; set; } = true;

    // طبق «PEMS EDMS Deep Repository Audit» یافته F-06: فایل‌های پیوست‌شده‌ی مخصوص همین رویژن
    // (اختیاری - پیوست‌های قدیمی/سطح-مدرک همچنان از طریق Document.Attachments قابل‌دسترسی می‌مانند)
    public ICollection<DocumentAttachment> Attachments { get; set; } = new List<DocumentAttachment>();

    // ==============================================================================
    // طبق «PEMS EDMS Phase 2 Lifecycle Design» یافته L2-01: پیش از این، پاسخ کارفرما فقط روی
    // EngineeringDocument.ClientApprovalStatus ثبت می‌شد - نه مخصوص یک رویژن - و هنگام ساخت
    // رویژن جدید هرگز پاک نمی‌شد، پس رویژن تازه (بدون بازبینی) به‌اشتباه تاییدشده نشان داده
    // می‌شد. حالا حقیقت مرجع همین‌جاست؛ فیلدهای هم‌نام روی Document فقط یک Cache از آخرین
    // رویژن هستند و روی ساخت رویژن جدید پاک می‌شوند.
    // ==============================================================================

    // کد پاسخ کارفرما - همان واژگان امروزی Document.ClientApprovalStatus: A/B/C/D
    public string? ClientReviewStatus { get; set; }

    public string? ClientReviewComment { get; set; }

    public DateTime? ClientReviewDate { get; set; }

    // پاسخ کارفرما باید از روی یک نامه‌ی وارده‌ی واقعی ثبت شود - همان قاعده‌ی سطح-مدرک قبلی
    public int? ClientReviewLetterId { get; set; }
    public Letter? ClientReviewLetter { get; set; }
}