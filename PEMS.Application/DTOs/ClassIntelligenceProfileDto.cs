namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 9» بند ۲۱: نمای یکپارچه‌ی هوشمندی یک EquipmentClass - این DTO چیزی محاسبه
/// نمی‌کند، فقط سه سرویس موجود (IEquipmentClassCoverageService، IReliabilityAnalyticsService،
/// IRcmFmeaReadinessService) را کنار هم می‌گذارد.
/// </summary>
public class ClassIntelligenceProfileDto
{
    public int EquipmentClassId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public int EquipmentCount { get; set; }
    public int ClassifiedDirectlyCount { get; set; }

    // از IEquipmentClassCoverageService - پوشش تعریفی (Activity/PMTemplate/FailureMode) - تعداد
    // واقعی (مستقیم+ارثی)، نه یک درصد ساختگی
    public int TotalActivityCoverage { get; set; }
    public int TotalPMTemplateCoverage { get; set; }
    public int TotalFailureModeCoverage { get; set; }
    public List<string> CoverageGapReasons { get; set; } = new();

    // از IReliabilityAnalyticsService.GetClassReliabilityAsync
    public int FailureCount { get; set; }
    public double? AverageMTBFHours { get; set; }
    public double? AverageMTTRHours { get; set; }
    public double? AverageAvailabilityPercent { get; set; }
    public int MTBFSampleSize { get; set; }

    // از IRcmFmeaReadinessService
    public string FMEAReadiness { get; set; } = "NOT_APPLICABLE";
    public string RCMReadiness { get; set; } = "NOT_APPLICABLE";
    public int ApprovedFMEACount { get; set; }
    public int ApprovedRCMCount { get; set; }

    // توزیع بحرانیت واقعی تجهیزات این کلاس
    public Dictionary<string, int> CriticalityDistribution { get; set; } = new();

    public int RepeatFailureGroupCount { get; set; }
    public int BadActorCount { get; set; }
}
