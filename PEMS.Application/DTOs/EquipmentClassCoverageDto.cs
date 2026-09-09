namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 3.5 — Data Quality + Maintenance Intelligence Coverage Gate» بند ۱۵-۲۳: برای
/// هر EquipmentClass، پوشش هوش نگهداری را به‌صورت شفاف (نه یک Health Score مبهم) نشان می‌دهد -
/// Direct (تعریف‌شده دقیقاً روی همین کلاس) در برابر Inherited (فقط از طریق سلسله‌مراتب Parent/
/// Grandparent به ارث رسیده، طبق همان الگوریتم MaintenanceIntelligenceService). یک شمارنده صفر
/// به‌تنهایی «نقص» نیست - GapReasons صریحاً می‌گوید این خلأ به چه معناست (بند ۱۶).
/// </summary>
public class EquipmentClassCoverageDto
{
    public int EquipmentClassId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentClassName { get; set; }

    public int EquipmentCount { get; set; }

    public int DirectActivityCount { get; set; }
    public int InheritedActivityCount { get; set; }

    public int DirectPMTemplateCount { get; set; }
    public int InheritedPMTemplateCount { get; set; }

    public int DirectJobPlanCount { get; set; }
    public int InheritedJobPlanCount { get; set; }

    public int DirectFailureModeCount { get; set; }
    public int InheritedFailureModeCount { get; set; }

    public int DirectStandardReferenceCount { get; set; }
    public int InheritedStandardReferenceCount { get; set; }

    public int StrategyTypeCount { get; set; }

    public int TotalActivityCoverage => DirectActivityCount + InheritedActivityCount;
    public int TotalPMTemplateCoverage => DirectPMTemplateCount + InheritedPMTemplateCount;
    public int TotalFailureModeCoverage => DirectFailureModeCount + InheritedFailureModeCount;

    // طبق بند ۱۶: چرا این خلأ وجود دارد - هرگز خودکار پر نمی‌شود
    public List<string> GapReasons { get; set; } = new();
}

public class MaintenanceIntelligenceCoverageSummaryDto
{
    public List<EquipmentClassCoverageDto> Classes { get; set; } = new();

    // طبق بند ۲۳: هر بُعد مستقل و شفاف است - نه یک درصد ترکیبی گمراه‌کننده
    public int ClassesWithEquipment { get; set; }
    public int ClassesWithDirectOrInheritedActivities { get; set; }
    public int ClassesWithDirectOrInheritedPMTemplates { get; set; }
    public int ClassesWithDirectOrInheritedFailureModes { get; set; }
    public int ClassesWithDirectOrInheritedStandards { get; set; }
    public int ClassesWithJobPlans { get; set; }

    public double ActivityCoveragePercent { get; set; }
    public double PMTemplateCoveragePercent { get; set; }
    public double FailureModeCoveragePercent { get; set; }
    public double StandardsCoveragePercent { get; set; }
}
