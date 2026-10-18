using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Dynamic Maintenance Organization» بخش ۴/۱۷-۱۹: تخصیص یک Employee به یک Position
/// معین - Position جدا از Employee است (سمت سازمانی می‌ماند، حتی وقتی خالی/Vacant است). دقیقاً
/// هم‌الگو با TeamMember/CrewMember (LeftDate/EndDate برای پایان تخصیص، نه حذف رکورد؛ تاریخچه
/// همیشه حفظ می‌شود). خروج کارمند از یک Position فقط این رکورد را می‌بندد - خودِ Position،
/// Employee، و هر WorkOrder/WorkRequest مرتبط دست‌نخورده می‌مانند.
/// </summary>
public class EmployeeAssignment : BaseEntity
{
    public int EmployeeAssignmentId { get; set; }

    public int PositionId { get; set; }
    public Position? Position { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateTime StartDate { get; set; } = DateTime.Now;

    // طبق بند ۱۸: پایان تخصیص = ثبت EndDate + IsActive=false، نه حذف رکورد. Null یعنی تخصیص
    // هنوز جاری است.
    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// سمت اصلی سازمانی کارمند: برای تعیین مدیر مستقیم فقط تخصیص «اصلی» فعالِ معتبر (طبق تاریخ‌ها) ملاک است. حداکثر یک تخصیص اصلیِ فعال
    /// برای هر کارمند (ایندکس یکتا). تخصیص‌های Secondary مدیر را تعیین نمی‌کنند.
    /// </summary>
    public bool IsPrimary { get; set; } = true;

    /// <summary>Primary | Secondary (فاز ۱). Acting/Temporary/Delegated عمداً به فاز بعدی موکول شده (مدل داده آماده‌ی افزودنِ آن‌هاست).</summary>
    public string AssignmentType { get; set; } = EmployeeAssignmentTypes.Primary;

    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}

public static class EmployeeAssignmentTypes
{
    public const string Primary = "Primary";
    public const string Secondary = "Secondary";
}
