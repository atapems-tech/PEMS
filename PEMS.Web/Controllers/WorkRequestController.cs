using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق CMMS Production Baseline v1 بند P0-05: مسیر رسمی قبل از WorkOrder اجرایی. برخلاف
/// StoreRequisition در فاز CMMS-00 (که بدون Permission ساخته شد و بعداً اصلاح شد)، این
/// Controller از همون ابتدا با بررسی مجوز واقعی ساخته می‌شود.
/// </summary>
public class WorkRequestController : Controller
{
    private readonly IWorkRequestService _service;
    private readonly PEMSDbContext _context;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUser;
    private readonly IFunctionalLocationService _functionalLocationService;

    private static readonly Dictionary<string, string> NextStatusMap = new()
    {
        ["Draft"] = "Submitted",
        ["Submitted"] = "UnderReview",
        ["UnderReview"] = "Approved",
        ["Approved"] = "Planned",
    };

    public WorkRequestController(IWorkRequestService service, PEMSDbContext context,
        IPermissionService permissionService, ICurrentUserService currentUser, IFunctionalLocationService functionalLocationService)
    {
        _service = service;
        _context = context;
        _permissionService = permissionService;
        _currentUser = currentUser;
        _functionalLocationService = functionalLocationService;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): قبلاً کل جدول فیلترشده
    // با ToListAsync (بدون Skip/Take) بارگذاری می‌شد.
    public async Task<IActionResult> Index(string? status, int page = 1, int? pageSize = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkRequest", "View"))
            return Forbid();

        ViewData["HelpKey"] = "WorkRequests.Index";

        ViewData["Title"] = "Work Requests";
        ViewData["Section"] = "CMMS Integration";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var result = await _service.GetListAsync(page, normalizedPageSize, status);
        ViewBag.SelectedStatus = status;

        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? equipmentId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkRequest", "Create"))
            return Forbid();

        // پیش‌پر کردن تجهیز وقتی از Digital Equipment Passport آمده (فقط نمایشی؛ ثبت همچنان از همین Action)
        if (equipmentId.HasValue)
        {
            var eq = await _context.Equipments.Where(e => e.EquipmentId == equipmentId.Value)
                .Select(e => new { e.EquipmentId, e.TagNumber, e.Name }).FirstOrDefaultAsync();
            if (eq != null) { ViewBag.PrefillEquipmentId = eq.EquipmentId; ViewBag.PrefillEquipmentText = eq.TagNumber + " — " + eq.Name; }
        }

        ViewData["Title"] = "New Work Request";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();
        ViewBag.FunctionalLocations = await _functionalLocationService.GetPickerListAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string title, string? description, int? equipmentId, int? assetId,
        int? functionalLocationId, string? reportedCondition, string priority, int? suggestedDisciplineId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkRequest", "Create"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        if (!employeeId.HasValue)
        {
            TempData["Error"] = "کارمند مرتبط با این کاربر پیدا نشد.";
            return RedirectToAction(nameof(Index));
        }

        var (success, error, id) = await _service.CreateAsync(
            title, description, employeeId.Value, equipmentId, assetId, functionalLocationId,
            reportedCondition, priority, suggestedDisciplineId);

        if (!success)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Create));
        }

        TempData["Message"] = "Work Request created";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkRequest", "View"))
            return Forbid();

        var wr = await _service.GetDetailsAsync(id);
        if (wr == null) return NotFound();

        ViewData["Title"] = "Work Request Details";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.NextStatus = NextStatusMap.TryGetValue(wr.Status, out var next) ? next : null;

        return View(wr);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdvanceStatus(int id, string? reason)
    {
        var wr = await _service.GetDetailsAsync(id);
        if (wr == null) return NotFound();

        if (!NextStatusMap.TryGetValue(wr.Status, out var next))
        {
            TempData["Error"] = "این درخواست در وضعیت نهایی است.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Submitted->UnderReview فقط نیازمند Create است (خودِ درخواست‌دهنده)؛ UnderReview->Approved
        // و Approved->Planned تصمیم نظارتی‌اند و نیازمند Approve.
        var requiredAction = next is "Approved" or "Planned" ? "Approve" : "Create";
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkRequest", requiredAction))
            return Forbid();

        var (success, error) = await _service.AdvanceStatusAsync(id, next, reason, await GetCurrentEmployeeIdAsync());

        TempData[success ? "Message" : "Error"] = success ? $"وضعیت به {next} تغییر کرد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reason)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkRequest", "Approve"))
            return Forbid();

        var (success, error) = await _service.AdvanceStatusAsync(id, "Rejected", reason, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "درخواست رد شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkRequest", "Create"))
            return Forbid();

        var (success, error) = await _service.AdvanceStatusAsync(id, "Cancelled", reason, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "درخواست لغو شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertToWorkOrder(int id, string workOrderType)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkRequest", "Approve"))
            return Forbid();

        var (success, error, workOrderId) = await _service.ConvertToWorkOrderAsync(id, workOrderType, _currentUser.UserName);

        if (!success)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Message"] = "WorkOrder ساخته شد.";
        return RedirectToAction("Details", "WorkOrder", new { id = workOrderId });
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }
}
