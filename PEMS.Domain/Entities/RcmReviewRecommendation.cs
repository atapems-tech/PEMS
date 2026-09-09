using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Phase 7 — Maintenance Intelligence &amp; Reliability Operationalization» بند ۲۰:
/// حلقه‌ی بازخورد RCM - وقتی داده‌ی قابلیت‌اطمینان واقعی (خرابی تکراری/فراوانی بالا/Downtime
/// بالا) نشان می‌دهد یک MaintenanceStrategy عملیاتی ممکن است دیگر مؤثر نباشد، این رکورد فقط
/// یک «توصیه‌ی بازبینی» با شواهد مشخص می‌سازد - هرگز خودش MaintenanceStrategy/MaintenancePlan/
/// PMTemplate/RCMDecision/Criticality را تغییر نمی‌دهد. تصمیم نهایی همیشه با یک مهندس انسانی
/// است (Status از PENDING فقط با ReviewedBy واقعی به ACCEPTED/REJECTED می‌رود - هرگز خودکار).
///
/// این یک Entity کاملاً جدید است چون هیچ مدل موجودی نمی‌تواند این رفتار را نمایندگی کند:
/// MaintenanceStrategy = پیکربندی عملیاتی (نه یک رکورد شواهد/بازبینی)، RCMDecision = توصیه‌ی
/// سطح کلاس (نه سطح Equipment، نه شواهد عملیاتی)، AuditLog = ثبت عمومی سیستم (نه یک گردش‌کار
/// Pending→Accepted/Rejected با شواهد ساختاریافته).
/// </summary>
public class RcmReviewRecommendation : BaseEntity
{
    public int RcmReviewRecommendationId { get; set; }

    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    // استراتژی عملیاتی موجود که این توصیه درباره‌ی آن است (همیشه باید موجود باشد - این توصیه
    // هرگز برای تجهیزی بدون MaintenanceStrategy واقعی ساخته نمی‌شود)
    public int MaintenanceStrategyId { get; set; }
    public MaintenanceStrategy? MaintenanceStrategy { get; set; }

    // طبق بند ۵۳ فاز ۶.۱: اگر این استراتژی از یک RCMDecision تاییدشده اعمال شده باشد، ردیابی
    // می‌شود (اختیاری - ممکن است این استراتژی مستقیماً دستی ساخته شده باشد، بدون RCMDecision)
    public int? RCMDecisionId { get; set; }
    public RCMDecision? RCMDecision { get; set; }

    // RepeatFailure, HighFailureFrequency, HighDowntime, PoorPMEffectiveness
    public string TriggerReason { get; set; } = string.Empty;

    // خلاصه‌ی خوانا برای انسان - همیشه اعداد واقعی، هرگز جعلی («۳ خرابی تکراری در ۹۰ روز»)
    public string Evidence { get; set; } = string.Empty;

    // اعداد ساختاریافته‌ی پشت شواهد - برای فیلتر/مرتب‌سازی و AI-Readiness آینده (بند ۳۱)
    public int? RepeatFailureCount { get; set; }
    public int? FailureCountInWindow { get; set; }
    public decimal? TotalDowntimeHoursInWindow { get; set; }
    public int WindowDays { get; set; }

    // PENDING, ACCEPTED, REJECTED
    public string Status { get; set; } = "PENDING";

    public DateTime GeneratedDate { get; set; } = DateTime.Now;

    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
