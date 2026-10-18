namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۵: زیرساخت نرم‌افزاری همیشه آماده است (FoundationStatus=READY) -
/// آنچه واقعاً به داده نیاز دارد LiveTelemetryStatus است که فقط با قرائت واقعی READY می‌شود.
/// </summary>
public class ConditionMonitoringFoundationStatusDto
{
    public string FoundationStatus { get; set; } = "READY";
    public string LiveTelemetryStatus { get; set; } = "NOT_READY";
    public int MonitoringPointCount { get; set; }
    public int ReadingCount { get; set; }
    public List<string> SupportedVariableTypes { get; set; } = new();
}

public class ConditionMonitoringPointDto
{
    public int ConditionMonitoringPointId { get; set; }
    public int EquipmentId { get; set; }
    public string? EquipmentTagNumber { get; set; }
    public string VariableType { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public decimal? AlarmThresholdLow { get; set; }
    public decimal? AlarmThresholdHigh { get; set; }
    public int ReadingCount { get; set; }
    public DateTime? LastReadingDate { get; set; }
    public decimal? LastValue { get; set; }
    public bool LastReadingIsAlarm { get; set; }
    public int? LastReadingId { get; set; }

    // طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۵
    public int? EvaluationProfileId { get; set; }
    public string? EvaluationProfileCode { get; set; }
    // Normal, Warning, Alarm, Critical, NotEvaluated - از خودِ آخرین ConditionReading خوانده می‌شود
    // (منجمدشده در لحظه‌ی ثبت)، نه دوباره‌محاسبه‌شده این‌جا.
    public string LastReadingEvaluationStatus { get; set; } = "NotEvaluated";
}

/// <summary>
/// طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۸:
/// تاریخچه‌ی یک نقطه‌ی پایش، همیشه محدود به یک بازه‌ی زمانی مشخص (هرگز کل جدول ConditionReadings
/// بدون Skip/Take بارگذاری نمی‌شود - بند ۳۱ سند: no unbounded history queries).
/// </summary>
public class ConditionMonitoringTrendDto
{
    public int ConditionMonitoringPointId { get; set; }
    public string VariableType { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalInRange { get; set; }
    // اگر تعداد واقعی از سقف بیشتر باشد، این لیست فقط نمونه‌ی محدودشده است - Truncated=true
    // به کاربر نشان می‌دهد که باید بازه را کوچک‌تر کند، نه این‌که بی‌صدا داده حذف شود.
    public bool Truncated { get; set; }
    public List<ConditionReadingTrendPointDto> Points { get; set; } = new();
}

/// <summary>
/// طبق «PEMS — Equipment Passport Audit & CBM» فاز ۱ ممیزی §2/§5-1: خلاصه‌ی پایش وضعیت برای
/// نمایش داخل تب Equipment/Details - جزئیات کامل همچنان روی Reliability/ConditionMonitoring
/// و Finding/Index می‌مانند (دقیقاً همون الگوی Instrument/MechanicalEquipment summary بالا).
/// </summary>
public class ConditionMonitoringPassportSummaryDto
{
    public int PointCount { get; set; }
    public int OpenFindingCount { get; set; }
    public List<ConditionMonitoringPointDto> Points { get; set; } = new();

    // از MaintenanceStrategy(StrategyType="CBM").ConditionMonitoringPointId → MaintenancePlan
    // می‌آید - این صفحه چیزی را دوباره محاسبه نمی‌کند، فقط زنجیره‌ی موجود را نمایش می‌دهد.
    public bool HasCbmStrategy { get; set; }
    public DateTime? NextDueDate { get; set; }
}

public class ConditionReadingTrendPointDto
{
    public DateTime ReadingDate { get; set; }
    public decimal Value { get; set; }
    public string EvaluationStatus { get; set; } = "NotEvaluated";
    public bool IsAlarm { get; set; }
}

/// <summary>
/// طبق «PEMS — Equipment Passport Audit & CBM» فاز ۱ ممیزی §3.8: داشبورد CBM - فقط تجمیع
/// (Aggregation) روی داده‌ی موجود (ConditionMonitoringPoint/ConditionReading/Finding/
/// MaintenanceStrategy/MaintenancePlan)، هیچ Entity تازه‌ای نمی‌سازد. لیست‌ها طبق بند ۳۱
/// (no unbounded queries) به MaxDashboardRows محدود می‌شوند.
/// </summary>
public class CbmDashboardDto
{
    public int TotalActivePoints { get; set; }
    public int PointsWithEvaluationProfile { get; set; }
    public int PointsWithoutEvaluationProfile { get; set; }
    public int PointsWithCbmStrategy { get; set; }

    public int CriticalCount { get; set; }
    public int AlarmCount { get; set; }
    public int WarningCount { get; set; }
    public int RequiresEngineeringReviewCount { get; set; }

    public int OpenFindingCount { get; set; }
    public int LinkedFindingCount { get; set; }

    public int OverdueCbmPlanCount { get; set; }
    public int DueSoonCbmPlanCount { get; set; }

    // نقاط پایشی که آخرین قرائتشان Critical/Alarm/RequiresEngineeringReview است - مرتب‌شده بر
    // اساس شدت، محدود به MaxDashboardRows.
    public List<CbmDashboardPointRowDto> AttentionPoints { get; set; } = new();
    public bool AttentionPointsTruncated { get; set; }

    // طرح‌های CBM که از MaintenancePlan.NextDueDate گذشته‌اند - مرتب‌شده بر اساس قدیمی‌ترین.
    public List<CbmDashboardOverdueRowDto> OverdueCbmPlans { get; set; } = new();
    public bool OverdueCbmPlansTruncated { get; set; }
}

public class CbmDashboardPointRowDto
{
    public int ConditionMonitoringPointId { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentTagNumber { get; set; } = string.Empty;
    public string VariableType { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string EvaluationStatus { get; set; } = string.Empty;
    public DateTime? LastReadingDate { get; set; }
}

public class CbmDashboardOverdueRowDto
{
    public int EquipmentId { get; set; }
    public string EquipmentTagNumber { get; set; } = string.Empty;
    public string VariableType { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public DateTime NextDueDate { get; set; }
    public int DaysOverdue { get; set; }
}
