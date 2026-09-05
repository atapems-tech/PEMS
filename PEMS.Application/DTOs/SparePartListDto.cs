namespace PEMS.Application.DTOs;

/// <summary>
/// ردیف Grid قطعات یدکی - طبق سند «PEMS — Global Data Grid Standard»: فقط فیلدهای نمایش‌داده‌شده،
/// نه کل Entity. IsLowStock همان فرمول SparePart.IsLowStock (ReorderPoint دارد و QuantityOnHand
/// از آن کمتر یا مساوی است) است، اما این‌جا در SQL محاسبه می‌شود، نه بعد از بارگذاری در حافظه.
/// </summary>
public class SparePartListDto
{
    public int SparePartId { get; set; }
    public string PartNumber { get; set; } = "";
    public string PartName { get; set; } = "";
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public string? StorageLocation { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal? MinStockLevel { get; set; }
    public decimal? ReorderPoint { get; set; }
    public bool IsLowStock { get; set; }
}
