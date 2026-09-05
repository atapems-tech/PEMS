using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

public class MaintenanceSpareUsage : BaseEntity
{
    [Key]
    public int MaintenanceSpareUsageId { get; set; }

    // دستور کار مرتبط
    public int WorkOrderId { get; set; }

    public WorkOrder? WorkOrder { get; set; }

    // طبق سند Inventory & Material Management بخش ۱۲: از SparePart قدیمی به MaterialItem
    // جدید سوییچ شد تا دو سیستم موازی برای ثبت مصرف قطعه نداشته باشیم - SparePart همچنان
    // برای سازگاری باقی می‌ماند ولی مسیر رشد آینده از این‌جا نیست. جدول قبل از این تغییر
    // ۰ رکورد داشت (تأیید‌شده با sqlcmd) - Migration بدون ریسک داده است.
    public int MaterialItemId { get; set; }

    public MaterialItem? MaterialItem { get; set; }

    // تعداد مصرف
    public decimal Quantity { get; set; }

    // قیمت واحد
    public decimal? UnitCost { get; set; }

    // هزینه کل - طبق CMMS Production Baseline v1 بند ۱۲.۱: وقتی مقداری Return می‌شود، این
    // فیلد به مصرف خالص (Quantity - ReturnedQuantity) بازمحاسبه می‌شود؛ Quantity خودش دست‌نخورده
    // می‌ماند (واقعیت تاریخی تحویل ناخالص).
    public decimal? TotalCost { get; set; }

    public decimal? ReturnedQuantity { get; set; }

    // تاریخ مصرف
    public DateTime UsageDate { get; set; } = DateTime.Now;

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}