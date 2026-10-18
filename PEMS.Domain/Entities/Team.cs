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

    // مثلاً "شیفت صبح"، "شیفت شب" - اختیاری - تاریخی/آزاد، دست‌نخورده (نگاه کن به NodeKind/
    // Children پایین برای مدل جدید شیفت به‌عنوان زیرگره)
    public string? Shift { get; set; }

    // طبق «PEMS Dynamic Maintenance Organization» بخش ۲/۱۳: تودرتویی دلخواه زیر یک Team -
    // زیرتیم، شیفت (Shift A/B/C به‌عنوان گره، نه فقط رشته)، واحد عملیاتی و غیره، همه همین
    // Entity با یک والد. Null یعنی تیم ریشه‌ی زیرمجموعه‌ی مستقیم MaintenanceGroup. NodeKind فقط
    // برچسب نمایشی/گزارشی است (مثلاً "Team"، "SubTeam"، "Shift"، "Unit")، هیچ رفتاری از رویش
    // مشروط نشده - از تبدیل آن به یک enum بسته خودداری شد چون این ماژول دقیقاً باید بدون کد جدید
    // انواع گره‌ی تازه بپذیرد.
    public int? ParentTeamId { get; set; }
    public Team? ParentTeam { get; set; }
    public ICollection<Team> Children { get; set; } = new List<Team>();

    public string NodeKind { get; set; } = "Team";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();

    public ICollection<Position> Positions { get; set; } = new List<Position>();
}
