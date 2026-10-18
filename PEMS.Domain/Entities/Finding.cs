using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS — Standards-Based Mechanical Equipment Passport &amp; Condition Monitoring» بند ۳۶:
/// یک یافته - چرا این رکورد وجود دارد باید از خودش قابل‌فهم باشد (تجهیز، منبع، وضعیت، توضیح).
/// عمداً منبع را از طریق دو FK اختیاری جدا (ConditionReading یا InspectionRecord) پیوند می‌دهد،
/// نه یک نوعِ چندریختی/Polymorphic - دقیقاً یکی از این دو باید پر باشد (اعتبارسنجی سطح سرویس،
/// نه محدودیت پایگاه‌داده، چون EF Core محدودیت XOR ندارد).
///
/// این Entity هرگز WorkRequest را تکرار نمی‌کند (بند ۳۶: «Do not duplicate Work Request») - فقط
/// وقتی به یک WorkRequest واقعی متصل شود، از طریق WorkRequestId موجود، نه فیلدهای اجرایی خودش.
/// همچنین هرگز طبقه‌بندی خرابی را دوباره اختراع نمی‌کند - به FailureMode/FailureCause موجود
/// (RCM-FMEA، PEMS.Domain/Entities/FailureTaxonomy.cs) پیوند می‌خورد.
/// </summary>
public class Finding : BaseEntity
{
    public int FindingId { get; set; }

    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    // دقیقاً یکی از این دو باید پر باشد - منبع واقعیِ این یافته
    public int? ConditionReadingId { get; set; }
    public ConditionReading? ConditionReading { get; set; }

    public int? InspectionRecordId { get; set; }
    public InspectionRecord? InspectionRecord { get; set; }

    public DateTime ObservedAt { get; set; } = DateTime.Now;

    public string Description { get; set; } = string.Empty;

    public string? RecommendedAction { get; set; }

    // Normal, Warning, Alarm, Critical - وضعیت در لحظه‌ی مشاهده (از ConditionReading.EvaluationStatus
    // یا InspectionRecord.Condition کپی می‌شود، نه دوباره محاسبه)
    public string Condition { get; set; } = string.Empty;

    // اختیاری - پیوند به طبقه‌بندی خرابی موجود (RCM-FMEA)، هرگز اختراع تازه
    public int? FailureModeId { get; set; }
    public FailureMode? FailureMode { get; set; }

    public int? FailureCauseId { get; set; }
    public FailureCause? FailureCause { get; set; }

    // فقط بعد از ایجاد/پیوند موفق یک WorkRequest واقعی پر می‌شود
    public int? WorkRequestId { get; set; }
    public WorkRequest? WorkRequest { get; set; }

    // Open, Linked, Closed
    public string Status { get; set; } = "Open";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
