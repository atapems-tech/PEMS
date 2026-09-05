using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند Inventory بخش ۹: موجودی واقعی یک MaterialItem در یک Warehouse/Location مشخص -
/// جدا از تعریف خودِ متریال (MaterialItem). AvailableQuantity محاسباتیه، ذخیره نمی‌شه.
/// </summary>
public class MaterialStock : BaseEntity
{
    public int MaterialStockId { get; set; }

    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public int? StorageLocationId { get; set; }
    public StorageLocation? StorageLocation { get; set; }

    public decimal OnHandQuantity { get; set; }

    public decimal ReservedQuantity { get; set; }

    public decimal? MinimumQuantity { get; set; }

    public decimal? MaximumQuantity { get; set; }

    public decimal? ReorderPoint { get; set; }

    public decimal? ReorderQuantity { get; set; }

    public DateTime? LastUpdated { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // طبق فرمول بخش ۹ سند: AvailableQuantity = OnHandQuantity - ReservedQuantity
    [NotMapped]
    public decimal AvailableQuantity => OnHandQuantity - ReservedQuantity;

    [NotMapped]
    public bool IsLowStock => ReorderPoint.HasValue && AvailableQuantity <= ReorderPoint.Value;

    [NotMapped]
    public bool IsOutOfStock => OnHandQuantity <= 0;
}
