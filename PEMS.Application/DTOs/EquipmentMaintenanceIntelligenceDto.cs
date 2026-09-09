namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 3 — Hierarchy-Aware Maintenance Intelligence» بند ۱۷-۲۱: خروجی فقط DTO است
/// (هرگز Entity مستقیم EF افشا نمی‌شود). تطبیق دیگر فقط روی EquipmentClassId دقیق نیست - از
/// کلاس خودِ تجهیز تا ریشه‌ی سلسله‌مراتب (Parent/Grandparent) دنبال می‌شود؛ هر آیتم فقط یک‌بار
/// برمی‌گردد (بند ۱۸: بدون تکرار) و اولویت با نزدیک‌ترین سطح (Exact) است. EXCLUDE/Override
/// (بند ۱۹) عمداً پیاده‌سازی نشد - مدل داده‌ی فعلی (MaintenanceActivityEquipmentClass و مشابه)
/// هیچ ستون Mode ای ندارد و هیچ داده‌ی واقعی امروز به آن نیاز ندارد؛ افزودنش بدون یک مورد واقعی
/// دقیقاً همان Overengineering ای است که این فاز صریحاً منع کرده - در گزارش نهایی به‌عنوان یک
/// Gap مستند شده، نه یک نقص پنهان.
/// </summary>
public class EquipmentMaintenanceIntelligenceDto
{
    public int EquipmentId { get; set; }
    public string TagNumber { get; set; } = string.Empty;
    public string EquipmentType { get; set; } = string.Empty;

    public int? EquipmentClassId { get; set; }
    public string? EquipmentClassCode { get; set; }
    public string? EquipmentClassName { get; set; }
    public string? ClassificationConfidence { get; set; }
    public string? ClassificationSource { get; set; }

    // طبق بند ۲۰: زنجیره‌ی سلسله‌مراتب واقعاً پیموده‌شده (خودِ کلاس → والد → ...) - برای شفافیت
    public List<string> ResolvedClassHierarchy { get; set; } = new();

    public List<MaintenanceActivitySummaryDto> ApplicableActivities { get; set; } = new();
    public List<LookupSummaryDto> RecommendedStrategyTypes { get; set; } = new();
    public List<LookupSummaryDto> ApplicableStandardReferences { get; set; } = new();
    public List<PMTemplateSummaryDto> CandidatePMTemplates { get; set; } = new();
    public List<JobPlanSummaryDto> CandidateJobPlans { get; set; } = new();
    public List<LookupSummaryDto> ApplicableFailureModes { get; set; } = new();
}

public class MaintenanceActivitySummaryDto
{
    public int MaintenanceActivityId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? MaintenanceGroupName { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class PMTemplateSummaryDto
{
    public int PMTemplateId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public int? IntervalDays { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class JobPlanSummaryDto
{
    public int JobPlanId { get; set; }
    public string JobPlanNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public class LookupSummaryDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}
