using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق PEMS_CMMS_ARCHITECTURE.md: هویت قابل‌نگهداری و چرخه‌عمر CMMS برای یک Equipment -
/// جدا از هویت مهندسی Equipment خودش (Tag/Manufacturer/Model). اهمیت (Criticality) عمداً
/// اینجا تکرار نشده؛ همیشه از EquipmentCriticality واقعی خوانده می‌شود تا دو منبع حقیقت
/// نداشته باشیم.
/// </summary>
public class Asset : BaseEntity
{
    [Key]
    public int AssetId { get; set; }

    // ارتباط با Equipment
    public int EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    // شماره Asset
    // مثال: AST-P101
    public string AssetNumber { get; set; } = string.Empty;

    // محل عملکردی - مقدار متنی خام (از ایمپورت/ورود دستی قدیمی)، دست‌نخورده برای ردیابی.
    // مثال: Unit-100 / Area-A
    public string? FunctionalLocation { get; set; }

    // طبق CMMS Production Baseline v1 بند ۶: رابطه‌ی واقعی به FunctionalLocation - این همون
    // "فاز بعدی"ای هست که کامنت بالا بهش اشاره می‌کرد.
    public int? FunctionalLocationId { get; set; }
    public FunctionalLocation? FunctionalLocationRef { get; set; }

    // کلاس تعمیراتی
    // Rotating, Static, Electrical...
    public string? MaintenanceClass { get; set; }

    // تاریخ راه‌اندازی
    public DateTime? CommissioningDate { get; set; }

    // وضعیت تجهیز
    // Active, Standby, Retired
    public string Status { get; set; } = "Active";

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}