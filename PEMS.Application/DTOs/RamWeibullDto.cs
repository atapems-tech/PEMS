namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۳: تحلیل Weibull دو-پارامتری روی فاصله‌های واقعی بین خرابی‌های
/// متوالی یک تجهیز (از FailureRecord.FailureDate واقعی - هرگز از داده‌ی جعلی). روش برآورد:
/// Median Rank Regression با تقریب برنارد - یک روش عملی استاندارد که بدون کتابخانه‌ی آماری
/// خارجی قابل پیاده‌سازی است؛ این یک قرارداد مهندسی این پروژه است، نه ادعای انطباق با یک
/// استاندارد رسمی خاص.
/// </summary>
public class WeibullAnalysisDto
{
    public int EquipmentId { get; set; }
    public string? TagNumber { get; set; }

    // تعداد فاصله‌های واقعی بین خرابی (نه تعداد خرابی - با n خرابی، n-1 فاصله داریم)
    public int SampleSize { get; set; }
    public int RequiredMinimumSample { get; set; }

    // READY, PARTIAL, INSUFFICIENT_DATA
    public string DataStatus { get; set; } = "INSUFFICIENT_DATA";
    public string? Reason { get; set; }

    // پارامتر شکل - β
    public double? Beta { get; set; }

    // پارامتر مقیاس (عمر مشخصه) به ساعت - η
    public double? Eta { get; set; }

    // decreasing failure rate / approximately constant failure rate / increasing failure rate
    public string? BetaInterpretation { get; set; }

    // کیفیت برازش رگرسیون خطی Median Rank - فقط برای گزارش شفافیت، نه یک آستانه‌ی رد/قبول سخت‌گیرانه
    public double? RSquared { get; set; }

    public string Provenance { get; set; } = "CALCULATED";
    public string MethodologyVersion { get; set; } = "PEMS-WEIBULL-1.0 (Median Rank Regression, Bernard's approximation)";
}

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۲: پایه‌ی RAM (Reliability/Availability/Maintainability) - روی
/// همان IReliabilityService.GetEquipmentReliabilityAsync موجود سوار شده (MTBF/MTTR/Availability
/// را دوباره محاسبه نمی‌کند)، فقط FailureRateLambda و تفکیک صریح Inherent/Operational
/// Availability را اضافه می‌کند.
/// </summary>
public class RamAnalysisDto
{
    public int EquipmentId { get; set; }
    public string? TagNumber { get; set; }

    public string ReliabilityDataStatus { get; set; } = "INSUFFICIENT_DATA";
    public double? MTBFHours { get; set; }
    public double? FailureRateLambdaPerHour { get; set; }
    public int FailureSampleSize { get; set; }

    public string MaintainabilityDataStatus { get; set; } = "INSUFFICIENT_DATA";
    public double? MTTRHours { get; set; }

    public string InherentAvailabilityDataStatus { get; set; } = "INSUFFICIENT_DATA";
    // MTBF / (MTBF + MTTR) - از همان AvailabilityPercent موجود در EquipmentReliabilityDto
    public double? InherentAvailabilityPercent { get; set; }

    public string OperationalAvailabilityDataStatus { get; set; } = "INSUFFICIENT_DATA";
    // 1 - (TotalDowntimeHours / ساعت‌های تقویمی بین اولین خرابی ثبت‌شده تا امروز) - فقط وقتی
    // یک بازه‌ی تقویمی واقعی قابل تعریف باشد؛ هرگز از مدت‌زمان WorkOrder استنتاج نمی‌شود.
    public double? OperationalAvailabilityPercent { get; set; }
    public decimal? TotalDowntimeHoursConsidered { get; set; }
    public double? CalendarWindowHoursConsidered { get; set; }

    public string Provenance { get; set; } = "CALCULATED";
}
