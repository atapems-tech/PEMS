using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق «PEMS Phase 7.5 — CMMS Operational Data Readiness & Master Data Activation»: آیا PEMS
/// می‌تواند با داده‌ی مرجع موجود، واقعاً روی یک تجهیز واقعی کار نگهداری اجرا کند؟ این کنترلر
/// فقط گزارش می‌دهد - هیچ Mutation ای این‌جا نیست. فعال‌سازی واقعی از طریق کنترلرهای موجود
/// (MaintenanceStrategyController/JobPlanController/EquipmentCriticalityController) انجام
/// می‌شود که این صفحه صرفاً به آن‌ها لینک می‌دهد (بند ۲۷: Candidate → Review → کنترلرهای
/// موجود، نه یک گردش‌کار موازی جدید).
///
/// طبق بند ۲۴: روی همان ماژول مجوز "MaintenanceMasterData" موجود سوار شده (همان‌طور که
/// EquipmentClassificationController.Coverage از فاز ۳.۵ می‌کند) - هیچ ماژول مجوز جدیدی
/// برای یک صفحه‌ی صرفاً-گزارشی لازم نیست.
/// </summary>
public class CmmsReadinessController : Controller
{
    private readonly IEquipmentReadinessService _readinessService;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public CmmsReadinessController(IEquipmentReadinessService readinessService, PEMSDbContext context,
        ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _readinessService = readinessService;
        _context = context;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "View"))
            return Forbid();

        ViewData["Title"] = "CMMS Operational Readiness";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.Summary = await _readinessService.GetOperationalReadinessSummaryAsync();
        var backlog = await _readinessService.GetClassActivationBacklogAsync();

        return View(backlog);
    }

    public async Task<IActionResult> ClassDetail(int id, int page = 1)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "View"))
            return Forbid();

        var equipmentClass = await _context.EquipmentClasses.AsNoTracking().FirstOrDefaultAsync(x => x.EquipmentClassId == id);
        if (equipmentClass == null) return NotFound();

        ViewData["Title"] = $"Readiness — {equipmentClass.Name}";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.EquipmentClass = equipmentClass;
        var result = await _readinessService.GetEquipmentReadinessAsync(id, page);

        return View(result);
    }
}
