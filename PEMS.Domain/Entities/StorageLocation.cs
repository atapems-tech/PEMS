using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند Inventory بخش ۸: Warehouse -> Zone -> Rack -> Bin - یک زنجیره‌ی خودارجاعی
/// اختیاری زیر هر Warehouse (همون الگوی MaterialCategory/FunctionalLocation).
/// </summary>
public class StorageLocation : BaseEntity
{
    public int StorageLocationId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public int? ParentLocationId { get; set; }
    public StorageLocation? Parent { get; set; }
    public ICollection<StorageLocation> Children { get; set; } = new List<StorageLocation>();

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
