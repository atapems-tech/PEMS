using Microsoft.AspNetCore.Identity;

namespace PEMS.Persistence.Identity;

/// <summary>
/// کاربر سیستم بر پایه‌ی ASP.NET Core Identity (جدول AspNetUsers).
/// این کلاس مستقل از Entity قدیمی PEMS.Domain.Entities.User است و آن را جایگزین نمی‌کند؛
/// جدول قدیمی دست‌نخورده باقی می‌ماند تا Migration های موجود خراب نشوند.
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    public string FullName { get; set; } = string.Empty;

    // لینک اختیاری به رکورد Employee (اطلاعات سازمانی کارمند)
    public int? EmployeeId { get; set; }

    // لینک اختیاری به Discipline (برای فیلتر کردن مدارک/فعالیت‌های مرتبط با کاربر)
    public int? DisciplineId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public DateTime? LastLoginDate { get; set; }

    // طبق درخواست: کاربرانی که با رمز پیش‌فرض ساخته می‌شوند (مثلاً هنگام ساخت خودکار از
    // Employee) باید در اولین ورود مجبور به تغییر رمز شوند.
    public bool MustChangePassword { get; set; } = false;
}
