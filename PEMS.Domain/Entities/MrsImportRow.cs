namespace PEMS.Domain.Entities;

/// <summary>
/// یک ردیف خام از فایل Excel واقعی MRS، پیش از تایید. طبق ممیزی ۹ فایل واقعی: ستون‌های
/// خام این‌جا مستقیماً از نام‌گذاری ناهمگون منابع (MRS NO / ITEM CODE / SUPLYER / QTY
/// RECIVED / UNT ...) می‌آیند - نگاشت به MaterialItem فقط بعد از Review اتفاق می‌افتد.
/// </summary>
public class MrsImportRow
{
    public int MrsImportRowId { get; set; }

    public int MrsImportBatchId { get; set; }
    public MrsImportBatch? Batch { get; set; }

    public string? RawMrsNo { get; set; }
    public string? RawItemCode { get; set; }
    public string? RawPackingNo { get; set; }
    public string? RawPoNo { get; set; }
    public string? RawSupplier { get; set; }
    public string? RawDescription { get; set; }
    public string? RawUom { get; set; }
    public decimal? RawQtyReceived { get; set; }
    public decimal? RawQtyIssued { get; set; }
    public decimal? RawInventory { get; set; }
    public DateTime? RawDateReceived { get; set; }
    public DateTime? RawDateIssued { get; set; }
    public string? RawMivNo { get; set; }
    public string? RawMrvNo { get; set; }
    public string? RawLocation { get; set; }
    public string? RawDiscipline { get; set; }
    public string? RawSize { get; set; }
    public string? RawEquipmentTag { get; set; }

    // ناشناخته‌ها (ستون‌هایی که به هیچ فیلد بالا نگاشت نشدند) - JSON از Dictionary اصلی
    public string? RawExtraJson { get; set; }

    // نرمال‌شده: lowercase/trim/collapse-whitespace روی RawDescription، برای تطبیق سه‌سطحی
    public string? NormalizedDescription { get; set; }

    public string? ResolvedWarehouseCode { get; set; }
    public int? ResolvedWarehouseId { get; set; }
    public Warehouse? ResolvedWarehouse { get; set; }

    // New, MatchedExisting, DuplicateInBatch, Invalid
    public string MatchStatus { get; set; } = "New";

    public int? MatchedMaterialItemId { get; set; }
    public MaterialItem? MatchedMaterialItem { get; set; }

    // Pending, Approved, Rejected
    public string ReviewStatus { get; set; } = "Pending";

    public string? ErrorMessage { get; set; }

    public int? ReviewedById { get; set; }
    public Employee? ReviewedBy { get; set; }
    public DateTime? ReviewedDate { get; set; }

    public int? ResultMaterialItemId { get; set; }
    public MaterialItem? ResultMaterialItem { get; set; }

    public int? ResultInventoryTransactionId { get; set; }
    public InventoryTransaction? ResultInventoryTransaction { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // طبق سند Metadata Extraction: هر ستون واقعی این ردیف (شناخته‌شده یا نه)، با متن اصلی
    // سرستون + مقدار خام - نه فقط آن‌هایی که به فیلدهای Typed بالا نگاشت شدند
    public ICollection<MrsImportRowMetadata> Metadata { get; set; } = new List<MrsImportRowMetadata>();
}
