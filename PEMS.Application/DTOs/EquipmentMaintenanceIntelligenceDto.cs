namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 2 — Maintenance Intelligence Engine» بند ۲۰: خروجی فقط DTO است (هرگز Entity
/// مستقیم EF افشا نمی‌شود). تطبیق فقط بر اساس EquipmentClassId دقیق تجهیز است (نه بالارفتن از
/// سلسله‌مراتب تا والد) - طبق بند ۴۴ عمداً ساده نگه داشته شد؛ اگر در آینده نیاز واقعی به تطبیق
/// سطح والد اثبات شود، این یک توسعه‌ی مجزا و مستند خواهد بود.
/// </summary>
public class EquipmentMaintenanceIntelligenceDto
{
    public int EquipmentId { get; set; }
    public string TagNumber { get; set; } = string.Empty;
    public string EquipmentType { get; set; } = string.Empty;

    public int? EquipmentClassId { get; set; }
    public string? EquipmentClassCode { get; set; }
    public string? EquipmentClassName { get; set; }

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
}

public class PMTemplateSummaryDto
{
    public int PMTemplateId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public int? IntervalDays { get; set; }
}

public class JobPlanSummaryDto
{
    public int JobPlanId { get; set; }
    public string JobPlanNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class LookupSummaryDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
