using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class InspectionRecord
{
    [Key]
    public int InspectionRecordId { get; set; }

    // تجهیز مورد بازرسی
    public int EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    // ==============================================================================
    // اتصال به ITP: طبق ساخت واقعی بخش QC/QA - بازرسی باید از روی یک ردیف ITP از‌پیش‌تعریف‌شده
    // انجام شود (Hold/Witness/Review Point)، نه بی‌ارتباط با یک برنامه. Nullable برای
    // سازگاری با هر رکورد قدیمی که ممکن است بدون ITP ثبت شده باشد.
    // ==============================================================================
    public int? ITPLineItemId { get; set; }
    public ITPLineItem? ITPLineItem { get; set; }

    public int? QCDisciplineId { get; set; }
    public QCDiscipline? QCDiscipline { get; set; }

    // نوع بازرسی
    // Thickness, Visual, Vibration...
    public string InspectionType { get; set; } = string.Empty;

    // تاریخ بازرسی
    public DateTime InspectionDate { get; set; }

    // بازرس
    public string? Inspector { get; set; }

    // نتیجه
    // Acceptable, Reject, Warning
    public string? Result { get; set; }

    // وضعیت تجهیز
    // Good, Fair, Poor
    public string? Condition { get; set; }

    // ==============================================================================
    // طبق CMMS Production Baseline v1 - CMMS-07 (Inspection): زنجیره‌ی Measurement → Limit →
    // Condition برای بازرسی‌های کمی/دوره‌ای (Condition Monitoring) - جدا از Result/Condition
    // کیفی بالا که برای پذیرش ITP ساخت (Pass/Fail یک‌باره). ConditionStatus (Normal/Alert) از
    // روی این مقادیر در سرویس محاسبه می‌شود، نه این‌جا ذخیره می‌شود.
    // ==============================================================================
    public decimal? MeasurementValue { get; set; }
    public string? MeasurementUnit { get; set; }
    public decimal? LowerLimit { get; set; }
    public decimal? UpperLimit { get; set; }

    // پیشنهاد اصلاحی
    public string? Recommendation { get; set; }

    // تاریخ بازرسی بعدی
    public DateTime? NextInspectionDate { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}