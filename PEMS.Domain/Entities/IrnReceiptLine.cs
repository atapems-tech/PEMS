using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

public class IrnReceiptLine : BaseEntity
{
    public int IrnReceiptLineId { get; set; }

    public int IrnReceiptId { get; set; }
    public IrnReceipt? IrnReceipt { get; set; }

    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    public decimal ReceivedQuantity { get; set; }

    // طبق ممیزی Material Management (Gap B) + Step 3: نتیجه‌ی Disposition به‌ازای هر ردیف -
    // عمداً nullable (نه یک Enum سراسری) چون تا وقتی Disposition ثبت نشده، این سه فیلد باید
    // null بمانند (نه صفر) تا "هنوز بررسی نشده" با "بررسی شد و مقدار صفر تایید شد" اشتباه گرفته
    // نشود. AcceptedQuantity+RejectedQuantity+HoldQuantity باید دقیقاً برابر ReceivedQuantity شود
    // (اعتبارسنجی در IrnReceiptService، نه این‌جا). فقط AcceptedQuantity به InventoryTransaction/
    // MaterialStock تبدیل می‌شود - Rejected/Hold هرگز به موجودی Post نمی‌شوند (طبق اصل «Received
    // ≠ Available» - نه با یک ستون Condition جدید روی MaterialStock، بلکه با این‌که اصلاً چیزی
    // برای مقدار رد/بلوکه‌شده Post نمی‌شود).
    public decimal? AcceptedQuantity { get; set; }
    public decimal? RejectedQuantity { get; set; }
    public decimal? HoldQuantity { get; set; }

    public int? UomId { get; set; }
    public MaterialUOM? Uom { get; set; }

    // اختیاری - محل فیزیکی دریافت در همین انبار، اگر شناخته‌شده باشد.
    public int? StorageLocationId { get; set; }
    public StorageLocation? StorageLocation { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
