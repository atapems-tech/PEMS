using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق بخش ۱۴ سند: عضویت در Crew - دقیقاً همون الگوی TeamMember (LeftDate برای تاریخچه،
/// نه حذف رکورد). نقش از همون MaintenanceRole موجود استفاده می‌شود، نه یک مفهوم نقش جدید.
/// </summary>
public class CrewMember : BaseEntity
{
    public int CrewMemberId { get; set; }

    public int CrewId { get; set; }
    public Crew? Crew { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int RoleId { get; set; }
    public MaintenanceRole? Role { get; set; }

    public DateTime JoinedDate { get; set; } = DateTime.Now;

    public DateTime? LeftDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
