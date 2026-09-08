using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Phase 2 — Equipment Classification + ISO 14224 Failure Taxonomy + PM/Job Plan
/// Intelligence» بند ۲۰-۲۲: قالب نگهداری پیشگیرانه‌ی قابل‌استفاده‌ی مجدد - داده‌ی مرجع در سطح
/// EquipmentClass (نه یک تجهیز خاص). جدا از MaintenancePlan موجود (که یک نمونه‌ی واقعی و
/// اختصاصی به یک Equipment مشخص است، با MaintenanceStrategyId تراکنشی) - PMTemplate هرگز
/// جایگزین MaintenancePlan نمی‌شود، فقط منبع پیشنهاد/تولید نمونه‌های جدید آن است.
/// فیلدهای فرکانس عمداً دقیقاً هم‌شکل MaintenancePlan.TriggerType/IntervalDays/MeterType/
/// MeterInterval هستند (نه واژگان جدید) تا تولید یک MaintenancePlan از روی این Template یک
/// Copy ساده‌ی فیلد به فیلد باشد.
/// </summary>
public class PMTemplate : BaseEntity
{
    public int PMTemplateId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NameFa { get; set; }

    public string? Description { get; set; }

    public int? EquipmentClassId { get; set; }
    public EquipmentClass? EquipmentClass { get; set; }

    public int? MaintenanceStrategyTypeId { get; set; }
    public MaintenanceStrategyType? MaintenanceStrategyType { get; set; }

    public int? MaintenanceTypeId { get; set; }
    public MaintenanceType? MaintenanceType { get; set; }

    // طبق بند ۲۵: قالب می‌تواند به یک Job Plan موجود (روال اجرای واقعی) اشاره کند - اختیاری،
    // چون قالب می‌تواند به‌عنوان یک توصیه‌ی زمان‌بندی/کاربردپذیری قبل از نگارش روال کامل هم وجود
    // داشته باشد (طبق بند ۳۱: «Do NOT generate fake PM plans simply to increase record counts»).
    public int? JobPlanId { get; set; }
    public JobPlan? JobPlan { get; set; }

    // ==================== Frequency (دقیقاً هم‌شکل MaintenancePlan) ====================
    // Calendar, Meter, Both
    public string TriggerType { get; set; } = "Calendar";
    public int? IntervalDays { get; set; }
    public string? MeterType { get; set; }
    public decimal? MeterInterval { get; set; }

    // طبق بند ۲۶: به‌جای اختراع Entity جدید، از همون رشته‌ی CriticalityClass موجود در
    // EquipmentCriticality استفاده می‌شود (A/B/C) - لیست جدا با کاما (مثلاً "A,B")
    public string? ApplicableCriticalityClasses { get; set; }

    public int? StandardReferenceId { get; set; }
    public StandardReference? StandardReference { get; set; }

    public bool IsSystemDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
