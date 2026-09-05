using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// نوع تخصصی "مدرک مهندسی" از Enterprise Document Core (Document). فقط فیلدهای خاص خودش را دارد؛
/// فیلدهای مشترک (DocumentNo, Title, ProjectId, DisciplineId, RevisionNo, Status, ...) از Document
/// به ارث می‌رسند (EF Core Table-Per-Type) تا کد و View های موجود بدون تغییر کار کنند.
/// </summary>
public class EngineeringDocument : Document
{
    // توضیحات تکمیلی (فیلد مشترک "Title" کافی نیست، جزئیات بیشتر این‌جا)
    public string? Description { get; set; }


    // =========================
    // Traceability (Related Equipment / Related Contract / Vendor / MR)
    // =========================

    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public int? ContractId { get; set; }
    public Contract? Contract { get; set; }

    // برای مدارک Vendor-submitted (دیتاشیت، منوال و ...) - طبق الگوی AVEVA Engineering که مدرک
    // را به داده‌ی سازمانی مرکزی (نه فقط فایل) وصل می‌کند
    public int? VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public int? RelatedMRId { get; set; }
    public MaterialRequisition? RelatedMR { get; set; }


    // =========================
    // Engineering Information
    // =========================

    // طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» بخش ۴.۶.۱: IFA/IFR/IFI/FI (کدهای رسمی
    // سند)، به‌همراه مقادیر قدیمی For Approval/For Construction/For Information (برای سازگاری با
    // داده‌ی موجود). VDRSubmission.PurposeOfIssueCode همین مفهوم را برای مدارک فروشنده نگه می‌دارد.
    public string? IssuePurpose { get; set; }

    public string? PreparedBy { get; set; }
    public string? CheckedBy { get; set; }
    public string? ApprovedBy { get; set; }

    // طبق بررسی چرخه‌ی مدرک: قبلاً این‌ها فقط رشته‌ی متنی دستی بودند، بدون هیچ ارتباطی با
    // گردش‌کار واقعی (سه امضای Prepared/Checked/Approved طبق ISO 9001، نه یک تایید یک‌مرحله‌ای).
    // این فیلدهای جدید با اقدامات واقعی گردش‌کار (Check/Approve) خودکار پر می‌شوند.
    public int? PreparedById { get; set; }
    public Employee? PreparedByEmployee { get; set; }

    public int? CheckedById { get; set; }
    public Employee? CheckedByEmployee { get; set; }

    public int? ApprovedById { get; set; }
    public Employee? ApprovedByEmployee { get; set; }

    public DateTime? CheckedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }

    // تایید کارفرما جدا از تایید داخلی EPC (IFR در برابر IFA) - طبق بررسی چرخه‌ی مدرک
    // Pending, Approved, ApprovedWithComments, Rejected
    public string? ClientApprovalStatus { get; set; }
    public string? ClientComment { get; set; }
    public DateTime? ClientResponseDate { get; set; }


    // =========================
    // Soft Delete / Archive
    // =========================

    public DateTime? DeletedDate { get; set; }
    public int? DeletedById { get; set; }
    public Employee? DeletedBy { get; set; }


    // =========================
    // Attachments / History
    // =========================

    public ICollection<DocumentAttachment> Attachments { get; set; }
        = new List<DocumentAttachment>();

    public ICollection<DocumentRevision> Revisions { get; set; }
        = new List<DocumentRevision>();

    public ICollection<DocumentWorkflow> Workflows { get; set; }
        = new List<DocumentWorkflow>();
}
