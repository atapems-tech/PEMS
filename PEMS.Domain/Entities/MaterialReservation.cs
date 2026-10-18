using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند Inventory بخش ۱۳: رزرو مستقل از Issue است - مقدار Issued/Returned واقعی از
/// روی InventoryTransaction های مرتبط (ReferenceType="Reservation") محاسبه می‌شه، نه یک
/// عدد دوم روی همین رکورد؛ همین چیزی‌یه که Required=10/Reserved=10/Issued=6/Returned=1 رو
/// قابل ردیابی می‌کنه.
/// </summary>
public class MaterialReservation : BaseEntity
{
    public int MaterialReservationId { get; set; }

    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    public int? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public decimal Quantity { get; set; }

    // Active, PartiallyIssued, FullyIssued, Released, Cancelled
    public string Status { get; set; } = "Active";

    public int? WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public int? MaintenancePlanId { get; set; }
    public MaintenancePlan? MaintenancePlan { get; set; }

    public int? StoreRequisitionId { get; set; }
    public StoreRequisition? StoreRequisition { get; set; }

    /// <summary>خطِ دقیق درخواستی که این رزرو برایش ساخته شده (ردیابی خط‌به‌خط؛ خطوط تکراریِ یک کالا مبهم نمی‌شوند). null = رزروهای قدیمی/غیر StoreRequisition.</summary>
    public int? StoreRequisitionLineId { get; set; }
    public StoreRequisitionLine? StoreRequisitionLine { get; set; }

    /// <summary>مقدار تحویل‌شده از همین رزرو (تحویل بخشی)؛ باقی‌مانده‌ی رزرو = Quantity - IssuedQuantity.</summary>
    public decimal IssuedQuantity { get; set; }

    public int ReservedById { get; set; }
    public Employee? ReservedBy { get; set; }

    public DateTime ReservedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
