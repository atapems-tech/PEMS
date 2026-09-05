using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند Inventory بخش ۱۴: درخواست داخلی برداشت از انبار (Draft→Submitted→Approved→
/// Reserved→Issued→Closed، +Rejected/Cancelled/PartiallyIssued) - این با MaterialRequisition
/// موجود (خرید از Vendor) کاملاً متفاوته، به همین خاطر اسمش عمداً StoreRequisition است نه
/// MaterialRequest تا با واژگان MR/MRQ موجود تداخل نکنه.
/// </summary>
public class StoreRequisition : BaseEntity
{
    public int StoreRequisitionId { get; set; }

    public string RequisitionNumber { get; set; } = string.Empty;

    // Draft, Submitted, Approved, Reserved, Issued, Closed, Rejected, Cancelled, PartiallyIssued
    public string Status { get; set; } = "Draft";

    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public int? WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public int RequestedById { get; set; }
    public Employee? RequestedBy { get; set; }

    public DateTime RequestedDate { get; set; } = DateTime.Now;

    public string? Purpose { get; set; }

    public int? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public ICollection<StoreRequisitionLine> Lines { get; set; } = new List<StoreRequisitionLine>();

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
