namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند Inventory بخش ۱۰: هر تغییر موجودی باید Transaction داشته باشد - موجودی هرگز
/// مستقیم بدون Transaction تغییر نمی‌کند (برخلاف رفتار قبلی SparePartController.Edit/
/// WorkOrderController.AddSpareUsage که QuantityOnHand را مستقیم بازنویسی می‌کردند).
/// Append-only - مثل WorkOrderStatusHistory، عمداً IsActive ندارد.
/// </summary>
public class InventoryTransaction
{
    public int InventoryTransactionId { get; set; }

    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public int? StorageLocationId { get; set; }
    public StorageLocation? StorageLocation { get; set; }

    // Receipt, Issue, Return, Transfer, Adjustment, Reservation, ReservationRelease
    public string TransactionType { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public int? UomId { get; set; }
    public MaterialUOM? Uom { get; set; }

    // WorkOrder, StoreRequisition, MrsImport, Manual
    public string? ReferenceType { get; set; }

    public int? ReferenceId { get; set; }

    public int? WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateTime TransactionDate { get; set; } = DateTime.Now;

    public string? Reason { get; set; }

    // طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۹: پیوند اختیاری به بچ تولید -
    // Nullable، بدون تغییر معنای تراکنش‌های قدیمی (که همیشه Null باقی می‌مانند). فقط وقتی پر
    // می‌شود که منبع واقعی تراکنش به یک ProductionBatch شناخته‌شده وصل باشد (هرگز حدسی/بازسازی‌شده
    // برای داده‌ی تاریخی). این معماری موجود (InventoryTransaction = دفتر کل، MaterialStock =
    // فرافکنی موجودی) را جایگزین نمی‌کند - فقط دفتر کل موجود را بچ-آگاه می‌کند.
    public int? ProductionBatchId { get; set; }
    public ProductionBatch? ProductionBatch { get; set; }
}
