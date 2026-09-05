using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند PEMS-SEC-PERMIT-AUTH-001 بند ۵: محدوده‌ی Plant/Area/Unit که یک کارمند مجاز به تایید
/// Permit در آن است. جدول خالی Ship می‌شود - طبق تصمیم صریح کاربر، تا وقتی داده‌ای برای یک
/// کارمند/Permit ثبت نشده، این بررسی صرفاً Advisory است، نه Required (فاز اول Rollout مانع
/// همه‌ی تاییدها نمی‌شود).
/// </summary>
public class EmployeeAreaScope : BaseEntity
{
    public int EmployeeAreaScopeId { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public string? Plant { get; set; }
    public string? Area { get; set; }
    public string? Unit { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
