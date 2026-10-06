using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS — Standards-Based Mechanical Equipment Passport &amp; Condition Monitoring» بند ۲۴:
/// آستانه‌ها هرگز مستقیماً روی خودِ نقطه‌ی پایش (ConditionMonitoringPoint) قرار نمی‌گیرند - یک
/// پروفایل ارزیابی مجزا و نسخه‌دار لازم است تا (الف) بشود چند پروفایل مختلف بسته به Equipment
/// Class/شرایط عملیاتی/استاندارد مرجع تعریف کرد، (ب) یک قرائت تاریخی هرگز صرفاً به‌خاطر فعال‌شدن
/// نسخه‌ی تازه‌ی یک پروفایل، تفسیرش عوض نشود (بند ۲۶ - ConditionReading نسخه‌ی دقیق پروفایلِ
/// اعمال‌شده را در لحظه‌ی ثبت منجمد می‌کند).
///
/// الگوی این Entity عیناً از GasParameterLimit (PEMS.Domain/Entities/GasParameterLimit.cs - یک
/// جدول محدودیتِ پارامتر-محور، جدا از خودِ قرائت) اقتباس شده، با افزودن دقیقاً همان چیزی که
/// GasParameterLimit ندارد: پیوند به StandardReference/EquipmentClass و نسخه‌بندی با بازه‌ی
/// اعتبار. جدول مستقل خودش است - نه اشتراک با GasParameterLimit (آن دامنه‌ی مجزای Permit-to-Work
/// گاز-تست باقی می‌ماند).
/// </summary>
public class EvaluationProfile : BaseEntity
{
    public int EvaluationProfileId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NameFa { get; set; }

    // طبق مرجع تعیین‌کننده‌ی آستانه‌ها - اختیاری (ممکن است پروفایل داخلی/کارخانه‌ای باشد، نه استاندارد رسمی)
    public int? StandardReferenceId { get; set; }
    public StandardReference? StandardReference { get; set; }

    // اختیاری - پروفایل می‌تواند برای یک Equipment Class خاص یا عمومی باشد
    public int? EquipmentClassId { get; set; }
    public EquipmentClass? EquipmentClass { get; set; }

    // دقیقاً همان مقادیر مجاز ConditionMonitoringPoint.VariableType (Vibration/Temperature/...)
    public string VariableType { get; set; } = string.Empty;

    // اختیاری - مثلاً "Normal Load", "Startup" - طبق بند ۱۹ سند
    public string? OperatingCondition { get; set; }

    public int Version { get; set; } = 1;

    public DateTime EffectiveFrom { get; set; } = DateTime.Now;
    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public List<EvaluationLimit> Limits { get; set; } = new();
}

/// <summary>
/// طبق بند ۲۴/۲۵ سند: هیچ آستانه‌ای در این پروژه از حافظه/فرضیات اختراع نمی‌شود - این جدول فقط
/// ظرف داده‌ای برای مقادیری است که یک مهندس واقعی/سند واقعی تأمین می‌کند. تا وقتی مقداری تأمین
/// نشده، سطر Limit برای آن Status ساخته نمی‌شود (نه این‌که با NULL/صفر جعلی پر شود).
/// </summary>
public class EvaluationLimit : BaseEntity
{
    public int EvaluationLimitId { get; set; }

    public int EvaluationProfileId { get; set; }
    public EvaluationProfile? EvaluationProfile { get; set; }

    // Normal, Warning, Alarm, Critical - طبق بند ۲۴؛ اگر ارزیابی واقعی پروژه از دسته‌بندی دیگری
    // استفاده کند، همین رشته‌ی آزاد (نه Enum ثابت) امکان آن را می‌دهد
    public string Status { get; set; } = string.Empty;

    public decimal? LowValue { get; set; }
    public decimal? HighValue { get; set; }

    public bool LowInclusive { get; set; } = true;
    public bool HighInclusive { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
