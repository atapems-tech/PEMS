namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» بخش AD: واقعیتِ دامنه‌ی تولید
/// (چه چیزی واقعاً تولید شد) - جدا از InventoryTransaction. هر خروجی الزاماً به یک
/// ProductionBatch وصل می‌شود (بچ یا تازه ساخته می‌شود یا مقدار قبلی‌اش افزایش می‌یابد) -
/// طبق بند «Production output MUST NOT automatically mean AVAILABLE FOR SALE»، ثبت این ردیف
/// هرگز به‌خودی‌خود وضعیت QualityStatus بچ را Released نمی‌کند (پیش‌فرض بچ همیشه Produced است).
/// </summary>
public class ProductionOutput
{
    public int ProductionOutputId { get; set; }

    public int ProductionRunId { get; set; }
    public ProductionRun? ProductionRun { get; set; }

    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    public int ProductionBatchId { get; set; }
    public ProductionBatch? ProductionBatch { get; set; }

    public decimal Quantity { get; set; }

    public int? UomId { get; set; }
    public MaterialUOM? Uom { get; set; }

    // Primary, ByProduct - طبق بند ۱۱ سند فاز (خروجی اصلی در برابر محصول جانبی)
    public string OutputClassification { get; set; } = "Primary";

    public int? InventoryTransactionId { get; set; }
    public InventoryTransaction? InventoryTransaction { get; set; }

    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateTime TransactionDate { get; set; } = DateTime.Now;

    public string? Notes { get; set; }
}
