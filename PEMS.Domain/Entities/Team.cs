using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند معماری Employee/Team/Labor Cost: یک گروه عملیاتی از چند Employee - مفهوم
/// مستقل از Employee (که مرجع پرسنلی واحد در ماژول Employees است).
/// </summary>
public class Team : BaseEntity
{
    public int TeamId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    // طبق PEMS_CMMS_MAINTENANCE_ORGANIZATION_BLUEPRINT.md بخش ۲: Team دیگر مستقیم زیر
    // Discipline نیست - Discipline از طریق MaintenanceGroup.Discipline در دسترس می‌ماند
    public int MaintenanceGroupId { get; set; }
    public MaintenanceGroup? MaintenanceGroup { get; set; }

    public int? SupervisorEmployeeId { get; set; }
    public Employee? SupervisorEmployee { get; set; }

    // مثلاً "شیفت صبح"، "شیفت شب" - اختیاری
    public string? Shift { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
}
