namespace PEMS.Application.DTOs;

// ============================================================================================
// طبق «PEMS Phase 7 — Maintenance Intelligence & Reliability Operationalization»: همه‌ی DTO های
// زیر روی داده‌ی واقعی موجود (WorkOrder/FailureRecord/MaintenancePlan/EquipmentCriticality)
// ساخته شده‌اند - هیچ منبع داده‌ی موازی نیست. هرجا داده کافی نیست، Null/شمارش صفر واقعی با
// DataStatus صریح گزارش می‌شود - هرگز یک KPI جعل نمی‌شود (بند ۳۸: «No Fabricated KPI Rule»).
// DataStatus یکی از: NOT_READY (اصلاً داده‌ای در این دسته نیست)، INSUFFICIENT_DATA (داده هست
// ولی برای نتیجه‌گیری معنادار کافی نیست)، PARTIAL (نمونه‌ی کوچک - نتیجه محتاطانه قابل‌استفاده
// است)، READY (نمونه‌ی کافی).
// ============================================================================================

/// <summary>
/// طبق بند ۱۲: مخرج کسر عمداً فقط WorkOrder هایی است که واقعاً از یک MaintenancePlan تولید
/// شده‌اند (MaintenancePlanId != null) و DueDate واقعی دارند (بند ۱۲ فاز ۷: از
/// PMGenerationService، در لحظه‌ی تولید ثبت می‌شود). Cancelled/Rejected و NotYetDue و
/// InsufficientData (بدون DueDate - رکوردهای پیش از این فیلد) هرگز در محاسبه‌ی درصد به‌عنوان
/// «عدم انطباق» شمرده نمی‌شوند - فقط در صورتی که WO کامل شده یا واقعاً از سررسیدش گذشته باشد
/// (بدون تکمیل)، وارد مخرج می‌شود.
/// </summary>
public class PmComplianceDto
{
    public int TotalQualifyingWorkOrders { get; set; }

    public int CompliantCount { get; set; }
    public int CompletedLateCount { get; set; }
    public int OverdueCount { get; set; }
    public int NotYetDueCount { get; set; }
    public int CancelledCount { get; set; }
    public int InsufficientDataCount { get; set; }

    // (Compliant) / (Compliant + CompletedLate + Overdue) × 100 - Null اگر مخرج صفر باشد
    public double? CompliancePercent { get; set; }

    public string DataStatus { get; set; } = "NOT_READY";
    public string? DataStatusReason { get; set; }
}

public class MaintenanceTypeItemDto
{
    public string TypeName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double? Percent { get; set; }
    public decimal? TotalHours { get; set; }
    public decimal? TotalCost { get; set; }
    public decimal? TotalDowntimeHours { get; set; }
}

/// <summary>
/// طبق بند ۱۳/۱۴: دسته‌بندی فقط از روی WorkOrder.WorkOrderType واقعی (Preventive/Corrective/
/// Emergency/Inspection/Predictive) - هرگز از متن آزاد استنتاج نمی‌شود. مقادیر ناشناخته/خالی
/// در سطر "Other" جمع می‌شوند تا هیچ رکوردی گم نشود، ولی هرگز به‌اشتباه به یک دسته‌ی مشخص نسبت
/// داده نمی‌شود.
/// </summary>
public class MaintenanceTypeBreakdownDto
{
    public int TotalWorkOrdersInPeriod { get; set; }
    public List<MaintenanceTypeItemDto> Items { get; set; } = new();

    // طبق بند ۱۴: نسبت کار اضطراری - همان مخرج (WorkOrder های واجد شرط در بازه) که در Items هم استفاده شده
    public double? EmergencyWorkRatioPercent { get; set; }
    public int EmergencyWorkOrderCount { get; set; }

    public string DataStatus { get; set; } = "NOT_READY";
}

/// <summary>
/// طبق بند ۱۵: قاعده‌ی تشخیص - همان تجهیز + همان حالت‌خرابی (یا نام رشته‌ای وقتی FailureModeId
/// موجود نیست)، با حداقل یک زوج خرابی که فاصله‌شان ≤ WindowDays باشد. این قاعده صریح و
/// پیکربندی‌پذیر است (پارامتر windowDays) - نه یک تعریف پنهان.
/// </summary>
public class RepeatFailureGroupDto
{
    public int EquipmentId { get; set; }
    public string TagNumber { get; set; } = string.Empty;
    public string FailureModeKey { get; set; } = string.Empty;
    public int OccurrenceCount { get; set; }
    public DateTime FirstOccurrence { get; set; }
    public DateTime LastOccurrence { get; set; }
    public int WindowDays { get; set; }
    public List<DateTime> OccurrenceDates { get; set; } = new();
}

/// <summary>
/// طبق بند ۱۸: شواهد چندبعدی - هرگز یک امتیاز واحد جهانی. هر بعد Trend مستقل خودش را دارد؛
/// وقتی مقایسه‌ی دو دوره به دلیل داده‌ی ناکافی در دوره‌ی پایه (قبلی) ممکن نیست،
/// InsufficientData گزارش می‌شود - هرگز یک روند جعل نمی‌شود.
/// </summary>
public class MaintenanceEffectivenessDto
{
    public int WindowDays { get; set; }
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime PriorPeriodStart { get; set; }

    public double? PmCompliancePercent { get; set; }
    public string PmComplianceDataStatus { get; set; } = "NOT_READY";

    public int RepeatFailureGroupCount { get; set; }

    public int CurrentPeriodFailureCount { get; set; }
    public int PriorPeriodFailureCount { get; set; }
    public string FailureFrequencyTrend { get; set; } = "INSUFFICIENT_DATA";

    public double? CurrentPeriodAverageMTTRHours { get; set; }
    public double? PriorPeriodAverageMTTRHours { get; set; }
    public string MttrTrend { get; set; } = "INSUFFICIENT_DATA";

    public decimal CurrentPeriodDowntimeHours { get; set; }
    public decimal PriorPeriodDowntimeHours { get; set; }
    public string DowntimeTrend { get; set; } = "INSUFFICIENT_DATA";

    public double? CorrectiveWorkloadPercent { get; set; }
    public double? EmergencyWorkloadPercent { get; set; }
}

/// <summary>
/// طبق بند ۱۹: این یافته‌های هوشمندی هستند، نه تصمیم خودکار - Findings فقط شرایط را برجسته
/// می‌کند (بر اساس مقایسه‌ی صریح و مستندشده با میانگین ناوگان)، هرگز خودش چیزی را تغییر نمی‌دهد.
/// </summary>
public class CriticalityMatrixRowDto
{
    public string CriticalityClass { get; set; } = string.Empty;
    public int EquipmentCount { get; set; }
    public int EquipmentWithFailureHistoryCount { get; set; }
    public int FailureCount { get; set; }
    public decimal TotalDowntimeHours { get; set; }
    public int RepeatFailureGroupCount { get; set; }
    public double? PmCompliancePercent { get; set; }
    public List<string> Findings { get; set; } = new();
}
