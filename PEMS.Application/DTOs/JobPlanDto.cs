namespace PEMS.Application.DTOs;

public class JobPlanListDto
{
    public int JobPlanId { get; set; }
    public string JobPlanNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public decimal? EstimatedDurationHours { get; set; }
    public int TaskCount { get; set; }
    public int PlanUsageCount { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// طبق CMMS Production Baseline v1 بند ۱۰.۳: جزئیات کامل یک Job Plan - Task ها (مرحله به
/// مرحله)، Required Skills، Required Materials.
/// </summary>
public class JobPlanDetailDto
{
    public int JobPlanId { get; set; }
    public string JobPlanNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Scope { get; set; }
    public decimal? EstimatedDurationHours { get; set; }
    public string? SafetyRequirements { get; set; }
    public bool IsActive { get; set; }

    public List<MaintenanceTaskDto> Tasks { get; set; } = new();
    public List<JobPlanRequiredSkillDto> RequiredSkills { get; set; } = new();
    public List<JobPlanRequiredMaterialDto> RequiredMaterials { get; set; } = new();
}

public class MaintenanceTaskDto
{
    public int MaintenanceTaskId { get; set; }
    public int TaskNumber { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
}

public class JobPlanRequiredSkillDto
{
    public int JobPlanRequiredSkillId { get; set; }
    public int SkillId { get; set; }
    public string SkillName { get; set; } = "";
    public string? MinimumProficiencyLevel { get; set; }
    public decimal? PlannedHours { get; set; }
}

public class JobPlanRequiredMaterialDto
{
    public int JobPlanRequiredMaterialId { get; set; }
    public int MaterialItemId { get; set; }
    public string MaterialCode { get; set; } = "";
    public string? MaterialDescription { get; set; }
    public decimal RequiredQuantity { get; set; }
}
