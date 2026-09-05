using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق PEMS_CMMS_MAINTENANCE_ORGANIZATION_BLUEPRINT.md بخش ۱۶: فهرست مشترک مهارت‌ها -
/// به Employee (از طریق EmployeeSkill) و به WorkOrder (از طریق RequiredSkill) وصل می‌شود.
/// </summary>
public class Skill : BaseEntity
{
    public int SkillId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
