using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق PEMS_CMMS_MAINTENANCE_ORGANIZATION_BLUEPRINT.md بخش ۲: Maintenance Group یک
/// گروه‌بندی تعمیراتی دقیق‌تر از Discipline است (مثلاً Rotating Equipment زیر Mechanical) -
/// مفهومی مستقل که Team باید زیرش تعریف شود، نه مستقیم زیر Discipline.
/// </summary>
public class MaintenanceGroup : BaseEntity
{
    public int MaintenanceGroupId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
