using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق بخش ۱۷ سند: تخصیص یک مهارت به یک کارمند - Non-destructive مثل TeamMember
/// (IsActive=false به‌جای حذف، تا در صورت اشتباه یا تغییر بعداً Reactivate ممکن باشد).
/// </summary>
public class EmployeeSkill : BaseEntity
{
    public int EmployeeSkillId { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int SkillId { get; set; }
    public Skill? Skill { get; set; }

    // Basic, Intermediate, Advanced, Expert
    public string ProficiencyLevel { get; set; } = "Basic";

    public DateTime? CertifiedDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
