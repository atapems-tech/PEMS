using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق بازنگری CMMS: یک ماژول «قطعات یدکی» بدون موجودی انبار عملاً کاتالوگه، نه ابزار
/// عملیاتی. حالا موجودی واقعی، حداقل موجودی، و نقطه‌ی سفارش مجدد دارد - استاندارد پایه‌ی هر
/// سیستم مدیریت انبار صنعتی.
/// </summary>
public class SparePart : BaseEntity
{
    [Key]
    public int SparePartId { get; set; }

    // شماره قطعه
    // مثال: BRG-6312
    public string PartNumber { get; set; } = string.Empty;

    // نام قطعه
    public string PartName { get; set; } = string.Empty;

    // سازنده
    public string? Manufacturer { get; set; }

    // مدل / کد سازنده
    public string? Model { get; set; }

    // واحد اندازه‌گیری
    // PCS, SET, KG
    public string? Unit { get; set; }

    public string? Description { get; set; }

    // ==============================================================================
    // طبق بازنگری CMMS: موجودی انبار واقعی - قبلاً این‌جا هیچ فیلدی نبود
    // ==============================================================================

    public decimal QuantityOnHand { get; set; }

    // زیر این مقدار، وضعیت به‌عنوان «کمبود موجودی» علامت‌گذاری می‌شود
    public decimal? MinStockLevel { get; set; }

    // نقطه‌ی سفارش مجدد - معمولاً بالاتر از MinStockLevel (زمان کافی برای رسیدن سفارش جدید)
    public decimal? ReorderPoint { get; set; }

    public string? StorageLocation { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;

    // طبق سطح موجودی، آیا این قطعه نیاز به سفارش مجدد دارد
    public bool IsLowStock => ReorderPoint.HasValue && QuantityOnHand <= ReorderPoint.Value;
}
