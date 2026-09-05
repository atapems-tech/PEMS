namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10f: رکورد یک تصمیم Execution Gate - هم StartWork عادی و هم EmergencyOverride را
/// در یک جدول ثبت می‌کند (نه دو مکانیزم Audit موازی، طبق دستورالعمل بند ۱۲). فقط روی اقدام
/// واقعی (StartWork/EmergencyOverride) نوشته می‌شود - نه روی هر بار مشاهده‌ی پنل (Compute-on-read
/// بدون اثر جانبی، همون الگوی ارزیابی‌های قبلی این فاز).
/// </summary>
public class ExecutionGateDecision
{
    public int ExecutionGateDecisionId { get; set; }

    public int PermitId { get; set; }
    public Permit? Permit { get; set; }

    public int? ActorEmployeeId { get; set; }
    public Employee? ActorEmployee { get; set; }

    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;

    // Eligible, Blocked
    public string Result { get; set; } = string.Empty;

    // فهرست دلایل رد/بایپس‌شده - حتی در EmergencyOverride موفق هم پر می‌شود تا مشخص باشد چه
    // چیزی Bypass شده.
    public string? BlockingReasonsSummary { get; set; }

    public bool IsEmergencyOverride { get; set; }
    public string? EmergencyReason { get; set; }

    public DateTime DecisionDate { get; set; } = DateTime.Now;
}
