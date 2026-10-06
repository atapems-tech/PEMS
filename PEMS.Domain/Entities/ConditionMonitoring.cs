using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Phase 9 — Enterprise Reliability, RAM &amp; Predictive Maintenance Intelligence»
/// بند ۱۵: زیرساخت نظارت وضعیت (Condition Monitoring) - یک نقطه‌ی پایش واقعی روی یک تجهیز
/// (ارتعاش، دما، فشار، ...). این جدول فقط تعریف نقطه‌ی پایش است؛ خودِ قرائت‌ها در
/// ConditionReading (Append-only، مثل CalibrationHistory) ثبت می‌شوند. هیچ داده‌ی تله‌متری
/// جعلی این‌جا درج نمی‌شود - این صرفاً ساختار داده‌ای آماده برای اتصال آینده به منبع واقعی
/// (SCADA/OPC/دستی) است.
/// </summary>
public class ConditionMonitoringPoint : BaseEntity
{
    [Key]
    public int ConditionMonitoringPointId { get; set; }

    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    // Vibration, Temperature, Pressure, Flow, Current, Voltage, Lubrication, OilAnalysis,
    // Thermography, Acoustic, ProcessDeviation - طبق بند ۱۵ سند فاز ۹
    public string VariableType { get; set; } = string.Empty;

    public string Tag { get; set; } = string.Empty;

    public string? Unit { get; set; }

    public decimal? AlarmThresholdLow { get; set; }
    public decimal? AlarmThresholdHigh { get; set; }

    // طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» بند ۲۴:
    // پیوند اختیاری به پروفایل ارزیابی نسخه‌دار - AlarmThresholdLow/High بالا دست‌نخورده می‌مانند
    // (مصرف‌کننده‌های موجود RecordReadingAsync را نمی‌شکند)؛ وقتی این پروفایل تنظیم شده باشد،
    // ارزیابی چندسطحی (Normal/Warning/Alarm/Critical) به‌جای فقط دو-سطحیِ IsAlarm محاسبه می‌شود.
    public int? EvaluationProfileId { get; set; }
    public EvaluationProfile? EvaluationProfile { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<ConditionReading> Readings { get; set; } = new List<ConditionReading>();
}

/// <summary>
/// قرائت واقعی یک نقطه‌ی پایش - Append-only (هرگز Overwrite/حذف نمی‌شود)، دقیقاً همان الگوی
/// CalibrationHistory/MaintenanceHistory. Source همیشه باید واقعی باشد (Manual تا وقتی یک
/// یکپارچه‌سازی واقعی SCADA/OPC وصل شود) - هرگز یک مقدار تصادفی/شبیه‌سازی‌شده این‌جا به‌عنوان
/// OBSERVED ثبت نمی‌شود.
/// </summary>
public class ConditionReading
{
    [Key]
    public int ConditionReadingId { get; set; }

    public int ConditionMonitoringPointId { get; set; }
    public ConditionMonitoringPoint? ConditionMonitoringPoint { get; set; }

    public DateTime ReadingDate { get; set; }

    public decimal Value { get; set; }

    // Manual, SCADA, OPC, Portable - همیشه واقعی؛ داده‌ی جعلی هرگز اینجا با Source واقعی ثبت نمی‌شود
    public string Source { get; set; } = "Manual";

    // طبق بند ۱۸ سند فاز ۹ (Data Provenance) - قرائت واقعی همیشه OBSERVED است
    public string Provenance { get; set; } = "OBSERVED";

    public bool IsAlarm { get; set; }

    // طبق بند ۲۶ سند (Versioning of Evaluation Profiles): این سه فیلد در لحظه‌ی ثبت منجمد
    // می‌شوند و هرگز بعداً بازمحاسبه نمی‌شوند - حتی اگر پروفایل بعداً نسخه‌ی تازه‌ای بگیرد یا
    // تغییر کند، تفسیر این قرائتِ خاصِ تاریخی همان می‌ماند که در لحظه‌ی ثبت بوده. NotEvaluated
    // یعنی هیچ پروفایلی در دسترس نبوده - هرگز به‌صورت خاموش Normal فرض نمی‌شود (بند ۲۹).
    public int? EvaluationProfileId { get; set; }
    public int? EvaluationProfileVersion { get; set; }

    // Normal, Warning, Alarm, Critical, NotEvaluated
    public string EvaluationStatus { get; set; } = "NotEvaluated";

    public string? Notes { get; set; }

    public string? RecordedBy { get; set; }

    // طبق «PEMS — Equipment Passport Audit & CBM» فاز ۱ ممیزی §3.3: اختیاری - کدام ابزار واقعی
    // این قرائت را گرفته (برای ردیابی اینکه آیا یک ابزار با کالیبراسیون منقضی‌شده قرائت‌هایی
    // تولید کرده). اختیاری و Nullable می‌ماند چون بیشتر قرائت‌های دستی/قدیمی هرگز ابزار مشخصی
    // ثبت نکرده‌اند - این یک زنجیره‌ی اختیاری تازه است، نه یک فیلد اجباری تازه روی داده‌ی موجود.
    public int? InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
