using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// AI Intake Queue: صف مرکزی همه‌ی فایل‌های آپلودشده که هنوز طبقه‌بندی/مسیریابی نشده‌اند یا
/// نیاز به تایید انسانی دارند (طبق AI Engineering Intelligence Redesign).
/// </summary>
public class IntakeController : Controller
{
    private readonly IIntakeService _intakeService;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public IntakeController(IIntakeService intakeService, PEMSDbContext context, ICurrentUserService currentUser)
    {
        _intakeService = intakeService;
        _context = context;
        _currentUser = currentUser;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - P1): قبلاً pageSize=25 مستقیم و
    // بدون Normalize پاس داده می‌شد و از QueryString هم پذیرفته نمی‌شد.
    public async Task<IActionResult> Index(string? stage, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "AI Intake Queue";
        ViewData["Section"] = "AI Engineering Intelligence";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var result = await _intakeService.GetQueueAsync(stage, page, normalizedPageSize);

        ViewBag.SelectedStage = stage;
        ViewBag.Stages = new[] { "Uploaded", "Classifying", "PendingEngineeringReview", "PendingManualClassification", "Routed", "Rejected" };

        return View(result);
    }

    public async Task<IActionResult> Review(int id)
    {
        ViewData["Title"] = "Review Intake Item";
        ViewData["Section"] = "AI Engineering Intelligence";

        var detail = await _intakeService.GetDetailAsync(id);
        if (detail == null) return NotFound();

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.Code, x.Name }).ToListAsync();

        ViewBag.Modules = new[] { "MDR", "VDR", "MR", "MRQ", "Contract", "CMMS", "Unrouted" };

        return View(detail);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Route(int attachmentId, string targetModule, string? category,
        string? documentType, string? disciplineCode, int? projectId, string? area, string? revisionNo,
        string? issuePurpose, string? vendorName, string? mrNumber)
    {
        try
        {
            var (module, entityId) = await _intakeService.RouteAsync(attachmentId, targetModule, category,
                documentType, disciplineCode, projectId, area, revisionNo, issuePurpose, vendorName, mrNumber,
                _currentUser.UserId);

            TempData["Message"] = entityId > 0
                ? $"Routed to {module} (record #{entityId})."
                : $"Tagged for {module}. Please complete the record manually in that module; the file is attached for reference.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int attachmentId)
    {
        await _intakeService.RejectAsync(attachmentId, _currentUser.UserId);
        TempData["Message"] = "Item rejected and removed from the active queue.";
        return RedirectToAction(nameof(Index));
    }
}
