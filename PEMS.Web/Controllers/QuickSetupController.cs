using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Identity;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق «PEMS — Quick Setup &amp; Domain Import Architecture Refactoring»: این Controller صرفاً
/// وضعیت راه‌اندازی سیستم را نمایش می‌دهد و به Workflow های دامنه‌ای موجود (Projects/Employee/
/// Team/Equipment/MaterialItem/Import Pipelines/...) لینک می‌دهد - هیچ منطق Mutation یا
/// Business Rule ای این‌جا تکرار نمی‌شود؛ خودِ آن Controllerها مجوز خودشان را مستقل بررسی
/// می‌کنند (طبق بند ۱۰ سند: "QuickSetup.View مجوز انجام Import را نمی‌دهد").
/// </summary>
public class QuickSetupController : Controller
{
    private readonly ISetupService _setupService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public QuickSetupController(ISetupService setupService, ICurrentUserService currentUser,
        IPermissionService permissionService)
    {
        _setupService = setupService;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Setup", "View"))
            return Forbid();

        ViewData["Title"] = "Quick Setup";
        ViewData["Section"] = "QuickSetup";

        var status = await _setupService.GetStatusAsync();

        // طبق بند ۸/۱۰ سند: کارت‌های Import سریع فقط وقتی نشان داده می‌شوند که کاربر واقعاً
        // مجوز همان Pipeline دامنه‌ای را هم داشته باشد - QuickSetup.View کافی نیست.
        ViewBag.CanImportMdr = await _permissionService.HasPermissionAsync(_currentUser.Roles, "MDRImport", "Create");
        ViewBag.CanImportVdr = await _permissionService.HasPermissionAsync(_currentUser.Roles, "VDR", "Create");
        ViewBag.CanImportEquipment = await _permissionService.HasPermissionAsync(_currentUser.Roles, "EquipmentImport", "Create");
        ViewBag.CanImportMrs = await _permissionService.HasPermissionAsync(_currentUser.Roles, "MrsImport", "Create");

        // طبق ممیزی: Setup/View هم به EngineeringManager داده می‌شود، ولی PermissionMatrix و
        // AI Routing Rules هر دو با [Authorize(Roles = SystemAdmin)] سخت‌کد شده‌اند (نه از طریق
        // ماژول مجوز عمومی) - بدون این پرچم، دکمه‌ی "Permissions"/"AI Configuration" برای
        // EngineeringManager یک لینک مرده می‌ساخت (Case C: سایدبار/دکمه قابل‌مشاهده، دسترسی رد).
        ViewBag.IsSystemAdmin = _currentUser.Roles.Contains(SystemRoles.SystemAdmin);

        return View(status);
    }
}
