using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق «PEMS Phase 7.6 — Engineering Criticality Assessment & Controlled Maintenance
/// Activation»: گردش‌کار مهندسیِ ارزیابی بحرانی‌بودن (DRAFT→UNDER_REVIEW→APPROVED→SUPERSEDED)
/// روی ماژول مجوز مجزای "CriticalityAssessment" - Create/Edit برای MaintenanceEngineer،
/// Approve فقط برای Supervisor/Manager. هیچ منطق کسب‌وکاری این‌جا نیست - همه از
/// ICriticalityAssessmentService می‌آید (Controller نازک، طبق CLAUDE.md).
/// </summary>
public class CriticalityAssessmentController : Controller
{
    private readonly ICriticalityAssessmentService _service;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public CriticalityAssessmentController(ICriticalityAssessmentService service, PEMSDbContext context,
        ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _service = service;
        _context = context;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Dashboard()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "CriticalityAssessment", "View"))
            return Forbid();

        ViewData["Title"] = "Criticality Assessment Dashboard";
        ViewData["Section"] = "CMMS Integration";
        ViewData["HelpKey"] = "CriticalityAssessment.Dashboard";

        var dashboard = await _service.GetDashboardAsync();
        return View(dashboard);
    }

    public async Task<IActionResult> Index(string? status, int? equipmentClassId, int page = 1)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "CriticalityAssessment", "View"))
            return Forbid();

        ViewData["Title"] = "Criticality Assessment Queue";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.StatusFilter = status;
        ViewBag.EquipmentClassId = equipmentClassId;
        ViewBag.CanEdit = await _permissionService.HasPermissionAsync(_currentUser.Roles, "CriticalityAssessment", "Edit");
        ViewBag.CanApprove = await _permissionService.HasPermissionAsync(_currentUser.Roles, "CriticalityAssessment", "Approve");
        ViewBag.EquipmentClasses = await _context.EquipmentClasses.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Name).Select(x => new { x.EquipmentClassId, x.Name, x.Code }).ToListAsync();

        var result = await _service.GetQueueAsync(status, equipmentClassId, page);
        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "CriticalityAssessment", "View"))
            return Forbid();

        var assessment = await _service.GetByIdAsync(id);
        if (assessment == null) return NotFound();

        ViewData["Title"] = $"Criticality Assessment — {assessment.EquipmentTagNumber}";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.CanEdit = await _permissionService.HasPermissionAsync(_currentUser.Roles, "CriticalityAssessment", "Edit");
        ViewBag.CanApprove = await _permissionService.HasPermissionAsync(_currentUser.Roles, "CriticalityAssessment", "Approve");

        return View(assessment);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? equipmentId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "CriticalityAssessment", "Create"))
            return Forbid();

        ViewData["Title"] = "New Criticality Assessment";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.PreselectedEquipmentId = equipmentId;
        if (equipmentId.HasValue)
        {
            ViewBag.PreselectedEquipmentLabel = await _context.Equipments.AsNoTracking()
                .Where(x => x.EquipmentId == equipmentId.Value)
                .Select(x => x.TagNumber + " - " + x.Name)
                .FirstOrDefaultAsync();
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int equipmentId, string? rationale)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "CriticalityAssessment", "Create"))
            return Forbid();

        var (success, error, id) = await _service.CreateDraftAsync(equipmentId, _currentUser.UserName, rationale);
        if (!success)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Create), new { equipmentId });
        }

        TempData["Message"] = "پیش‌نویس ارزیابی بحرانی‌بودن ساخته شد.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetScores(int id, int? safetyScore, int? productionScore, int? environmentScore, string? rationale)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "CriticalityAssessment", "Edit"))
            return Forbid();

        var (success, error) = await _service.SetDimensionScoresAsync(id, safetyScore, productionScore, environmentScore, rationale);
        TempData[success ? "Message" : "Error"] = success ? "امتیازها ثبت شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEvidence(int id, string sourceType, string? sourceIdentifier, string? reference, string description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "CriticalityAssessment", "Edit"))
            return Forbid();

        var (success, error) = await _service.AddEvidenceAsync(id, sourceType, sourceIdentifier, reference, description, _currentUser.UserName);
        TempData[success ? "Message" : "Error"] = success ? "شاهد اضافه شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitForReview(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "CriticalityAssessment", "Edit"))
            return Forbid();

        var (success, error) = await _service.SubmitForReviewAsync(id, _currentUser.UserName);
        TempData[success ? "Message" : "Error"] = success ? "برای بازبینی ارسال شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "CriticalityAssessment", "Approve"))
            return Forbid();

        var (success, error) = await _service.ApproveAsync(id, _currentUser.UserName);
        TempData[success ? "Message" : "Error"] = success
            ? "ارزیابی تایید و در EquipmentCriticality منتشر شد."
            : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "CriticalityAssessment", "Approve"))
            return Forbid();

        var (success, error) = await _service.RejectAsync(id, _currentUser.UserName);
        TempData[success ? "Message" : "Error"] = success ? "به DRAFT بازگشت داده شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }
}
