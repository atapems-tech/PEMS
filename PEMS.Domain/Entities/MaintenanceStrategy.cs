using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// ==============================================================================
/// طبق تطبیق با RCM (Reliability Centered Maintenance - SAE JA1011/JA1012)، استاندارد
/// جهانی برای تصمیم‌گیری «کدام استراتژی برای کدام خرابی مناسبه»:
///
///   ۱. هر استراتژی به یک حالت خرابی مشخص (FailureModeCode - همون واژگان ISO 14224 که
///      روی WorkOrder هم استفاده کردیم) وصل می‌شه، نه فقط یک برچسب کلی روی کل تجهیز.
///   ۲. طبقه‌بندی پیامد (Safety/Environmental/Operational/Economic) طبق RCM Decision Logic
///      مشخص می‌کنه که اصلاً باید فعالانه جلوی خرابی رو گرفت یا Run-to-Failure قابل‌قبوله.
///   ۳. Justification: چرا این استراتژی انتخاب شده - بدون این، انتخاب فقط سلیقه‌ست نه RCM.
/// ==============================================================================
/// </summary>
public class MaintenanceStrategy : BaseEntity
{
    [Key]
    public int MaintenanceStrategyId { get; set; }

    // ارتباط با تجهیز
    public int EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    // نوع استراتژی
    // Preventive, Predictive, Corrective, CBM, RunToFailure
    public string StrategyType { get; set; } = string.Empty;

    // ==============================================================================
    // طبق «PEMS — Enterprise Maintenance Master Data & Standards Implementation» بند ۹/۱۷:
    // ارتباط افزودنی/اختیاری به جدول مرجع MaintenanceStrategyType - StrategyType (رشته‌ی بالا)
    // برای Backward Compatibility دست‌نخورده می‌ماند؛ این فیلد فقط زیرساخت AI-Ready/کد پایدار
    // است، نه جایگزین فوری آن.
    // ==============================================================================
    public int? MaintenanceStrategyTypeId { get; set; }
    public MaintenanceStrategyType? MaintenanceStrategyTypeRef { get; set; }

    // ==============================================================================
    // طبق RCM: این استراتژی برای کدام حالت خرابی مشخص تعریف شده - همون واژگان ISO 14224
    // که روی WorkOrder.FailureModeCode هم داریم (Leakage, Fracture, Vibration, ...)
    // ==============================================================================
    public string? FailureModeCode { get; set; }

    // طبق RCM Decision Logic: Safety, Environmental, Operational, Economic - تعیین می‌کنه
    // که آیا اصلاً باید فعالانه جلوی این خرابی گرفته بشه یا Run-to-Failure قابل‌قبوله
    public string ConsequenceCategory { get; set; } = "Operational";

    // چرا این استراتژی (نه یکی دیگه) انتخاب شده - بدون این مستندسازی، RCM واقعی نیست
    public string? Justification { get; set; }

    // پیشنهاد نوع تریگر برای Planهایی که از این استراتژی ساخته می‌شن (Calendar/Meter/Both) -
    // طبق رفع ناهماهنگی قبلی بین Strategy و Plan
    public string RecommendedTriggerType { get; set; } = "Calendar";

    // تناوب انجام - طبق رفع ناهماهنگی قبلی: این‌جا فقط یک برچسب راهنماست (نه منبع رسمی
    // زمان‌بندی)؛ زمان‌بندی واقعی روی MaintenancePlan.IntervalDays/MeterInterval تعریف می‌شه
    // Daily, Weekly, Monthly, Yearly
    public string? Frequency { get; set; }

    // شرح فعالیت
    public string? Description { get; set; }

    // تاریخ شروع
    public DateTime? StartDate { get; set; }

    // وضعیت
    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}