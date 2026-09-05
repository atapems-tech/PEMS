using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public ProcurementPackageController(IProcurementPackageService service, PEMSDbContext context, ICurrentUserService currentUser)
    {
        _service = service;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Procurement Packages";
        ViewData["Section"] = "Engineering Management";

        var packages = await _service.GetAllAsync();

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();

        return View(packages);
    }

    public async Task<IActionResult> Details(int id)
    {
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
        var id = await _service.CreateAsync(packageNumber, projectId, disciplineId, title, description);
        TempData["Message"] = "Procurement Package created";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdvanceStage(int packageId, string newStage)
    {
        await _service.AdvanceStageAsync(packageId, newStage);
        return RedirectToAction(nameof(Details), new { id = packageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkMR(int packageId, int mrId)
    {
        await _service.LinkExistingMRAsync(packageId, mrId);
        TempData["Message"] = "MR linked to package";
        return RedirectToAction(nameof(Details), new { id = packageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCommunication(int packageId, string type, string subject, string body, int? vendorId)
    {
        await _service.AddCommunicationAsync(packageId, type, subject, body, vendorId, _currentUser.FullName, null);
        return RedirectToAction(nameof(Details), new { id = packageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RespondCommunication(int packageId, int communicationId, string responseText)
    {
        await _service.RespondToCommunicationAsync(communicationId, responseText, _currentUser.FullName);
        return RedirectToAction(nameof(Details), new { id = packageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCommunicationStatus(int packageId, int communicationId, string actionStatus)
    {
        await _service.SetCommunicationStatusAsync(communicationId, actionStatus);
        return RedirectToAction(nameof(Details), new { id = packageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddApproval(int packageId, string decision, string? comment)
    {
        await _service.AddApprovalAsync(packageId, _currentUser.FullName ?? "Unknown", decision, comment);
        TempData["Message"] = $"Approval recorded: {decision}";
        return RedirectToAction(nameof(Details), new { id = packageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Closeout(int packageId)
    {
        await _service.CloseoutAsync(packageId);
        TempData["Message"] = "Package closed out";
        return RedirectToAction(nameof(Details), new { id = packageId });
    }
}
