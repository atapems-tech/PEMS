using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// Procurement Package طبق سند Engineering Intelligence Redesign: جایگزین سیستم جدای MR/MRQ.
/// MR، MRQ، Vendor Inquiry، Technical Clarification، Response و Approval History همه از یک
/// صفحه مدیریت می‌شوند.
/// </summary>
public class ProcurementPackageController : Controller
{
    private readonly IProcurementPackageService _service;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public ProcurementPackageController(IProcurementPackageService service, PEMSDbContext context,
        ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _service = service;
        _context = context;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    // طبق «PEMS — Final Global Repository Audit» بند ۱۹ (P0): این کنترلر هیچ بررسی مجوزی نداشت -
    // هر کاربر احرازهویت‌شده‌ای (صرف‌نظر از نقش) می‌توانست Package بسازد، مرحله را جلو ببرد، و
    // از همه مهم‌تر AddApproval (تاییدیه‌ی خرید) را ثبت کند.
    // طبق «PEMS — Global Grid Hardening» P1: این Grid فیلتری نداشت؛ فقط صفحه‌بندی واقعی اضافه شد.
    public async Task<IActionResult> Index(int page = 1, int? pageSize = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "ProcurementPackage", "View"))
            return Forbid();

        ViewData["Title"] = "Procurement Packages";
        ViewData["Section"] = "Engineering Management";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var result = await _service.GetPagedAsync(page, normalizedPageSize);

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();

        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "ProcurementPackage", "View"))
            return Forbid();

        ViewData["Title"] = "Procurement Package Details";
        ViewData["Section"] = "Engineering Management";

        var detail = await _service.GetDetailAsync(id);
        if (detail == null) return NotFound();

        ViewBag.Vendors = await _context.Vendors.Where(x => x.IsActive)
            .Select(x => new { x.VendorId, x.Name }).ToListAsync();

        ViewBag.UnlinkedMRs = await _context.MaterialRequisitions
            .Where(x => x.ProcurementPackageId == null)
            .Select(x => new { x.MRId, x.MRNumber }).ToListAsync();

        return View(detail);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "ProcurementPackage", "Create"))
            return Forbid();

        ViewData["Title"] = "New Procurement Package";
        ViewData["Section"] = "Engineering Management";

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string packageNumber, int projectId, int? disciplineId, string title, string? description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "ProcurementPackage", "Create"))
            return Forbid();

        var id = await _service.CreateAsync(packageNumber, projectId, disciplineId, title, description);
        TempData["Message"] = "Procurement Package created";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdvanceStage(int packageId, string newStage)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "ProcurementPackage", "Edit"))
            return Forbid();

        await _service.AdvanceStageAsync(packageId, newStage);
        return RedirectToAction(nameof(Details), new { id = packageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkMR(int packageId, int mrId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "ProcurementPackage", "Edit"))
            return Forbid();

        await _service.LinkExistingMRAsync(packageId, mrId);
        TempData["Message"] = "MR linked to package";
        return RedirectToAction(nameof(Details), new { id = packageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCommunication(int packageId, string type, string subject, string body, int? vendorId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "ProcurementPackage", "Edit"))
            return Forbid();

        await _service.AddCommunicationAsync(packageId, type, subject, body, vendorId, _currentUser.FullName, null);
        return RedirectToAction(nameof(Details), new { id = packageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RespondCommunication(int packageId, int communicationId, string responseText)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "ProcurementPackage", "Edit"))
            return Forbid();

        await _service.RespondToCommunicationAsync(packageId, communicationId, responseText, _currentUser.FullName);
        return RedirectToAction(nameof(Details), new { id = packageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCommunicationStatus(int packageId, int communicationId, string actionStatus)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "ProcurementPackage", "Edit"))
            return Forbid();

        await _service.SetCommunicationStatusAsync(packageId, communicationId, actionStatus);
        return RedirectToAction(nameof(Details), new { id = packageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddApproval(int packageId, string decision, string? comment)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "ProcurementPackage", "Approve"))
            return Forbid();

        await _service.AddApprovalAsync(packageId, _currentUser.FullName ?? "Unknown", decision, comment);
        TempData["Message"] = $"Approval recorded: {decision}";
        return RedirectToAction(nameof(Details), new { id = packageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Closeout(int packageId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "ProcurementPackage", "Approve"))
            return Forbid();

        await _service.CloseoutAsync(packageId);
        TempData["Message"] = "Package closed out";
        return RedirectToAction(nameof(Details), new { id = packageId });
    }
}
