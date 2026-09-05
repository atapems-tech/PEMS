using Microsoft.AspNetCore.Mvc;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق درخواست: یک بخش «تنظیمات» واحد و مرتب - قبلاً صفحات مدیریتی (Users، Departments،
/// Permission Matrix، Roles، Audit Log، Routing Rules) پراکنده در سایدبار بودند بدون هیچ
/// نقطه‌ی ورود سازمان‌یافته. این صفحه هم تنظیمات شخصی، هم پیوند سازمان‌یافته به تنظیمات
/// سیستمی (برای مدیران) را یک‌جا نشان می‌دهد.
/// </summary>
public class SettingsController : Controller
{
    private readonly ICurrentUserService _currentUser;

    public SettingsController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public IActionResult Index()
    {
        ViewData["Title"] = "Settings";
        ViewData["Section"] = "Administration";

        ViewBag.IsAdmin = _currentUser.Roles.Contains("SystemAdmin");
        ViewBag.CanManageUsers = _currentUser.Roles.Any(r => r is "SystemAdmin" or "EngineeringManager");

        return View();
    }
}
