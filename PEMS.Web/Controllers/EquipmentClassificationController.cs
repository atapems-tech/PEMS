using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق «PEMS Phase 3 — Equipment Classification Completion» بند ۱۴/۱۵/۱۰: Dashboard (آمار
/// تجمیعی SQL-Aggregated) + صف بازبینی انسانی (Accept/Reject). هیچ منطق طبقه‌بندی این‌جا نیست -
/// همه‌چیز از IEquipmentClassificationService می‌آید. طبق بند ۲۸: Accept/Reject نیازمند مجوز
/// Edit روی همان ماژول "MaintenanceMasterData" است (بدون ماژول مجوز جدید، طبق بند ۴۲)؛ کاربران
/// فقط‌خواندنی می‌توانند Dashboard/Queue را ببینند ولی نمی‌توانند طبقه‌بندی معتبر را تغییر دهند.
/// </summary>
public class EquipmentClassificationController : Controller
{
    private readonly IEquipmentClassificationService _classificationService;
    private readonly IEquipmentClassCoverageService _coverageService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public EquipmentClassificationController(IEquipmentClassificationService classificationService,
        IEquipmentClassCoverageService coverageService, ICurrentUserService currentUser,
        IPermissionService permissionService)
    {
        _classificationService = classificationService;
        _coverageService = coverageService;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "View"))
            return Forbid();

        ViewData["Title"] = "Equipment Classification Dashboard";
        ViewData["Section"] = "CMMS Integration";

        var dashboard = await _classificationService.GetDashboardAsync();
        return View(dashboard);
    }

    public async Task<IActionResult> ReviewQueue(string status = "PENDING", int page = 1)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "View"))
            return Forbid();

        ViewData["Title"] = "Classification Review Queue";
        ViewData["Section"] = "CMMS Integration";
        ViewBag.StatusFilter = status;

        var result = await _classificationService.GetReviewQueueAsync(page, 50, status);
        return View(result);
    }

    public async Task<IActionResult> Coverage()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "View"))
            return Forbid();

        ViewData["Title"] = "Maintenance Intelligence Coverage";
        ViewData["Section"] = "CMMS Integration";

        var matrix = await _coverageService.GetCoverageMatrixAsync();
        return View(matrix);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id, string? comment, string status = "PENDING", int page = 1)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Edit"))
            return Forbid();

        var ok = await _classificationService.AcceptSuggestionAsync(id, _currentUser.UserName ?? "Unknown", comment);
        TempData[ok ? "Message" : "Error"] = ok ? "طبقه‌بندی تایید و اعمال شد." : "این پیشنهاد یافت نشد یا قبلاً بررسی شده.";
        return RedirectToAction(nameof(ReviewQueue), new { status, page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? comment, string status = "PENDING", int page = 1)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Edit"))
            return Forbid();

        var ok = await _classificationService.RejectSuggestionAsync(id, _currentUser.UserName ?? "Unknown", comment);
        TempData[ok ? "Message" : "Error"] = ok ? "پیشنهاد رد شد." : "این پیشنهاد یافت نشد یا قبلاً بررسی شده.";
        return RedirectToAction(nameof(ReviewQueue), new { status, page });
    }
}
