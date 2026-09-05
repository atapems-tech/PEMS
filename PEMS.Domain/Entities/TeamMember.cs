using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند معماری: صرفاً رابطه‌ی Employee و Team را مدیریت می‌کند - هیچ اطلاعات پرسنلی
/// (نام، دپارتمان، ...) این‌جا تکرار نمی‌شود؛ همیشه از طریق EmployeeId ارجاع داده می‌شود.
/// تغییر تیم باید تاریخچه داشته باشد (LeftDate)، نه بازنویسی.
/// </summary>
public class TeamMember : BaseEntity
{
    public int TeamMemberId { get; set; }

    public int TeamId { get; set; }
    public Team? Team { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int RoleId { get; set; }
    public MaintenanceRole? Role { get; set; }

    public DateTime JoinedDate { get; set; } = DateTime.Now;

    // طبق سند: وقتی عضویت تمام می‌شود (یا تیم عوض می‌شود)، این تاریخ ثبت می‌شود - رکورد
    // حذف نمی‌شود تا تاریخچه‌ی WorkOrder های قبلی دست‌نخورده بماند
    public DateTime? LeftDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
