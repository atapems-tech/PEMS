using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// درخواست مناقصه/استعلام رسمی (Tender/RFQ/ITB) وابسته به یک ProcurementPackage - طبق ممیزی
/// Package Procurement Contract Finalization (§C): تا پیش از این هیچ Entity ای برای این مفهوم
/// وجود نداشت؛ نزدیک‌ترین جایگزین (MaterialRequisitionQuotation) فقط استعلام تک-فروشنده بود.
/// ProcurementPackage همچنان سرصفحه‌ی بسته است - Tender یک زیرمجموعه‌ی آن برای مرحله‌ی مناقصه‌ی
/// رسمی چند-فروشنده است، نه یک معماری موازی.
/// </summary>
public class Tender : BaseEntity
{
    [Key]
    public int TenderId { get; set; }

    public string TenderNumber { get; set; } = string.Empty;

    public int ProcurementPackageId { get; set; }
    public ProcurementPackage? ProcurementPackage { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime? IssueDate { get; set; }

    public DateTime? ResponseDeadline { get; set; }

    // آیا فروشندگان دعوت‌شده باید در زمان دعوت واجد صلاحیت باشند - گیت §I قاعده‌ی ۱/۱۴
    public bool RequiresVendorQualification { get; set; } = true;

    public string? TechnicalRequirements { get; set; }
    public string? CommercialRequirements { get; set; }
    public string? InspectionRequirements { get; set; }
    public string? ContractualRequirements { get; set; }

    // Draft, Issued, Open, Closed, Cancelled, Awarded
    public string Status { get; set; } = "Draft";

    public int? AwardedBidId { get; set; }
    public Bid? AwardedBid { get; set; }

    public DateTime? AwardedDate { get; set; }

    public int? AwardedById { get; set; }
    public Employee? AwardedBy { get; set; }

    // طبق §C/§I (قاعده‌ی ۱۲): پس از Winner Selection، Contract Draft باید به همین Tender/Bid
    // برنده وصل باشد - نه یک قرارداد بی‌ریشه‌ی جدید
    public int? ContractId { get; set; }
    public Contract? Contract { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<TenderInvitedVendor> InvitedVendors { get; set; } = new List<TenderInvitedVendor>();
    public ICollection<Bid> Bids { get; set; } = new List<Bid>();
}
