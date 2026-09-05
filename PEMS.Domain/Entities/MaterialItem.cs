using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند Inventory بخش ۳ و ۵: SparePart معادل کل Inventory نیست - MaterialItem تعریف
/// خودِ متریال است (کاتالوگ)، جدا از موجودی واقعی‌اش در یک انبار (MaterialStock). ساختار
/// از روی ~۵۰هزار ردیف واقعی فایل‌های MRS (بعد از حذف تکرار) طراحی شده، نه حدسی.
/// </summary>
public class MaterialItem : BaseEntity
{
    public int MaterialItemId { get; set; }

    // کد یکتای متریال - طبق بخش ۱۹ سند، قبل از Import باید Normalize/Dedup بشه
    public string MaterialCode { get; set; } = string.Empty;

    // Bearing, Valve, Cable, Chemical, PPE, ... - طبق دسته‌های واقعی فایل‌های MRS
    public string? MaterialType { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? LongDescription { get; set; }

    public int? CategoryId { get; set; }
    public MaterialCategory? Category { get; set; }

    public int? DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    public int? UomId { get; set; }
    public MaterialUOM? Uom { get; set; }

    // طبق بخش ۱۸ سند: Manufacturer و Supplier یکی نیستند - Manufacturer رشته می‌ماند
    // (دقیقاً مثل SparePart موجود)، Supplier به Vendor واقعی وصل می‌شود
    public string? Manufacturer { get; set; }

    public string? ManufacturerPartNumber { get; set; }

    public int? VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public string? Specification { get; set; }

    public string? Model { get; set; }

    public string? Size { get; set; }

    public string? MaterialGrade { get; set; }

    // طبق بخش ۱۷ سند: معماری آماده برای آینده - داده‌ی MRS فعلی برای این قابلیت کافی نیست
    public bool IsSerialized { get; set; }

    public bool IsBatchControlled { get; set; }

    public bool IsStockItem { get; set; } = true;

    // طبق بخش ۱۶ سند: Critical Spare مستقل از Criticality تجهیزات است
    public bool IsCritical { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
