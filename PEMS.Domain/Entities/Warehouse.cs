using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند Inventory بخش ۸: بدون Warehouse، Inventory معنی نداره. لیست پیش‌فرض از روی
/// پیشوندهای واقعی MRS NO در فایل‌های MRS استخراج شده (WH-EL, WH-CH, HV, SF, WH-TANK,
/// WH-ME, WH-GEN, WH-CV) - حدسی نیست.
/// </summary>
public class Warehouse : BaseEntity
{
    public int WarehouseId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<StorageLocation> Locations { get; set; } = new List<StorageLocation>();
}
