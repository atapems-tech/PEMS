using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق ممیزی «Material Management Audit» (Gap A) + «Step 1 Closure Report»: سند رسمی دریافت
/// مادی (Inspection/Material Receipt Note) - جدا از MrsImportBatch/Row که صرفاً مکانیزم
/// Import اکسل است. IRN نقطه‌ی ورودی زنجیره‌ی Receiving→IRN→QC/Disposition→Stock است؛ طبق
/// مرز صریح Step 2، این Entity فقط تا آماده‌سازی برای بازرسی (Submitted) می‌رود - گیت
/// QC/Disposition و تعیین این‌که چه زمانی موجودی واقعاً Available می‌شود، عمداً به Step 3
/// موکول شده و این‌جا پیاده‌سازی/فرض نمی‌شود. ثبت این سند به‌تنهایی هیچ InventoryTransaction/
/// MaterialStock ای نمی‌سازد یا تغییر نمی‌دهد.
/// </summary>
public class IrnReceipt : BaseEntity
{
    public int IrnReceiptId { get; set; }

    // از طریق INumberingService.GetNextNumberAsync("IRN") - طبق قاعده‌ی موجود WorkOrder/StoreRequisition
    // (سالانه، Concurrency-Safe با UPDATE...OUTPUT) - نه Count()+1/MAX()+1/GUID.
    public string IrnNumber { get; set; } = string.Empty;

    // Draft, Submitted, Cancelled - عمداً بدون Dispositioned/Closed؛ آن دو به Step 3 تعلق دارند.
    public string Status { get; set; } = "Draft";

    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public DateTime ReceivingDate { get; set; } = DateTime.Now;

    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    // اختیاری - طبق محدودیت صریح Step 2: بدون PurchaseOrderLine (که به Step 7 موکول شده)،
    // فقط ارجاع سطح-Header به PurchaseOrder موجود.
    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    // اختیاری - حداقل ارتباط لازم با MrsImportBatch موجود، طبق دستور صریح: بدون بازطراحی MRS.
    public int? MrsImportBatchId { get; set; }
    public MrsImportBatch? MrsImportBatch { get; set; }

    public int ReceivedById { get; set; }
    public Employee? ReceivedBy { get; set; }

    public string? Notes { get; set; }

    public ICollection<IrnReceiptLine> Lines { get; set; } = new List<IrnReceiptLine>();

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
