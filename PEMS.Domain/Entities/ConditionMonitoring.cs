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

    public string? Notes { get; set; }

    public string? RecordedBy { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
