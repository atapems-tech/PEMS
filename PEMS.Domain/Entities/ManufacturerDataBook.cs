using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» بخش ۶ (Handover of Final Documentation):
/// MDB/Mechanical Catalogue برای هر تجهیز به سه بخش تقسیم می‌شود (Part A/B/C - جزئیات هر بخش
/// در MDBDocument.Part). این موجودیت، سرآیند (Header) یک MDB است؛ اسناد واقعی هر بخش در
/// MDBDocument نگه‌داری می‌شوند.
/// </summary>
public class ManufacturerDataBook
{
    [Key]
    public int MDBId { get; set; }

    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public int? PoId { get; set; }
    public PurchaseOrder? Po { get; set; }

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    // طبق بخش ۶: «Index of contents... due 4 weeks before final MDB issue timing (per SRQ)»
    public DateTime? PlannedFinalIssueDate { get; set; }

    public DateTime? IndexOfContentsSubmittedDate { get; set; }

    // Planned, IndexSubmitted, InProgress, MasterReview, FinalIssued, Delivered
    public string Status { get; set; } = "Planned";

    // طبق بخش ۷.۱: تحویل نسخهٔ چاپی (تعداد جلد، تاریخ ارسال)
    public int? HardCopyCount { get; set; }
    public DateTime? HardCopyShippedDate { get; set; }

    // طبق بخش ۷.۲: تحویل نسخهٔ نرم/DVD
    public DateTime? SoftCopyDeliveredDate { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<MDBDocument> Documents { get; set; } = new List<MDBDocument>();

    [NotMapped]
    public DateTime? IndexOfContentsDueDate =>
        PlannedFinalIssueDate.HasValue ? PlannedFinalIssueDate.Value.AddDays(-28) : null;

    [NotMapped]
    public bool IsIndexOverdue =>
        IndexOfContentsDueDate.HasValue && IndexOfContentsSubmittedDate == null && DateTime.Now > IndexOfContentsDueDate.Value;
}
