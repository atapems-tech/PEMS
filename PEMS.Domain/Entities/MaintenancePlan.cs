using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق بازنگری CMMS و RCM (Reliability Centered Maintenance - SAE JA1011): برنامه‌ی
/// نگهداری فقط نباید تقویمی (هر X روز) باشد - خیلی از تجهیزات دوار (پمپ، کمپرسور، موتور)
/// باید بر اساس ساعت کارکرد یا تعداد سیکل سرویس بشن، نه تاریخ تقویمی.
/// </summary>
public class MaintenancePlan : BaseEntity
{
    [Key]
    public int MaintenancePlanId { get; set; }

    // تجهیز مرتبط
    public int EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    // استراتژی نگهداری
    public int MaintenanceStrategyId { get; set; }

    public MaintenanceStrategy? MaintenanceStrategy { get; set; }

    // شماره برنامه
    // مثال: PM-P101-001
    public string PlanNumber { get; set; } = string.Empty;

    // عنوان برنامه
    public string Title { get; set; } = string.Empty;

    // طبق CMMS Production Baseline v1 بند ۱۰.۲: دستورالعمل اجرای قابل استفاده‌ی مجدد -
    // وقتی از این Plan یک WorkOrder ساخته می‌شود، Task/Skill/Material های این Job Plan
    // روی WorkOrder کپی می‌شوند (نگاه کنید GenerateWorkOrder).
    public int? JobPlanId { get; set; }
    public JobPlan? JobPlan { get; set; }

    // فاصله زمانی اجرا بر حسب روز (تریگر تقویمی)
    public int? IntervalDays { get; set; }

    // ==============================================================================
    // طبق RCM: تریگر مبتنی بر Meter (کارکرد) - برای تجهیزات دوار که سرویس‌شان باید بر اساس
    // استفاده‌ی واقعی باشد، نه تقویم خام
    // ==============================================================================

    // Calendar, Meter, Both
    public string TriggerType { get; set; } = "Calendar";

    // RunningHours, Cycles, ProductionUnits, ...
    public string? MeterType { get; set; }

    // هر چند واحد Meter یک‌بار سرویس لازم است (مثلاً هر ۲۰۰۰ ساعت کارکرد)
    public decimal? MeterInterval { get; set; }

    // آخرین قرائت ثبت‌شده‌ی Meter در زمان آخرین اجرا
    public decimal? LastExecutionMeterReading { get; set; }

    // قرائت Meter که در آن سررسید بعدی فرا می‌رسد
    public decimal? NextDueMeterReading { get; set; }

    // آخرین تاریخ اجرا
    public DateTime? LastExecutionDate { get; set; }

    // تاریخ اجرای بعدی
    public DateTime? NextDueDate { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}