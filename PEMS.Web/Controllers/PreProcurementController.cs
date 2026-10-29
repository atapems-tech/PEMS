using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// SCRUM-471: the procurement cartable (pre-procurement queue). Thin controller: every rule, permission check and write is in
/// IPreProcurementService. Tasks reach the buyer through My Work; this page lists the queue and holds the decision forms.
/// </summary>
public class PreProcurementController : Controller
{
    private const string Module = "PreProcurement";

    private readonly IPreProcurementService _service;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserService _currentUser;
    private readonly PEMSDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public PreProcurementController(IPreProcurementService service, IPermissionService permissions, ICurrentUserService currentUser,
        PEMSDbContext context, IStringLocalizer<SharedResource> localizer)
    {
        _service = service;
        _permissions = permissions;
        _currentUser = currentUser;
        _context = context;
        _localizer = localizer;
    }

    private Task<bool> CanViewAsync() => _permissions.HasPermissionAsync(_currentUser.Roles, Module, "View");

    public async Task<IActionResult> Index(string? status = "Open", string? source = null)
    {
        if (!await CanViewAsync()) return Forbid();
        ViewData["Title"] = _localizer["Ppr_Title"].Value;
        ViewData["Section"] = "Procurement";
        ViewBag.Status = status;
        ViewBag.Source = source;
        return View(await _service.GetQueueAsync(string.IsNullOrWhiteSpace(status) ? null : status, string.IsNullOrWhiteSpace(source) ? null : source));
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await CanViewAsync()) return Forbid();
        var details = await _service.GetAsync(id);
        if (details == null) return NotFound();

        ViewData["Title"] = _localizer["Ppr_Title"].Value;
        ViewData["Section"] = "Procurement";
        ViewBag.CanDecide = await _permissions.HasPermissionAsync(_currentUser.Roles, Module, "Edit");
        ViewBag.Projects = await _context.Projects.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.ProjectCode)
            .Select(p => new { p.ProjectId, p.ProjectCode, p.ProjectName }).ToListAsync();
        ViewBag.Disciplines = await _context.Disciplines.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Code)
            .Select(d => new { d.DisciplineId, d.Code, d.Name }).ToListAsync();
        return View(details);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(int id, string target, int? existingMrId, string? mrNumber, int? projectId, int? disciplineId,
        string? description, DateTime? requiredDate)
    {
        NewRequisitionInput? newMr = null;
        if (target != "existing")
        {
            if (projectId is not > 0 || disciplineId is not > 0) return Done(id, PreProcurementResult.Fail("Ppr_Err_ProjectDiscipline"));
            newMr = new NewRequisitionInput(mrNumber, projectId.Value, disciplineId.Value, description, requiredDate);
        }
        var r = await _service.ConvertToRequisitionAsync(id, target == "existing" ? existingMrId : null, newMr, await ActorAsync());
        if (r.Success) return RedirectToAction("Details", "MaterialRequisition", new { id = r.Id });
        return Done(id, r);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id) => Done(id, await _service.AcceptAsync(id, await ActorAsync()), "Ppr_Accepted");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(int id, string? reason) => Done(id, await _service.DismissAsync(id, reason ?? string.Empty, await ActorAsync()), "Ppr_Dismissed");

    /// <summary>From the replenishment recommendation page: hand the shortage to procurement.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendRecommendation(int recommendationId)
    {
        var r = await _service.SendRecommendationAsync(recommendationId, await ActorAsync());
        TempData[r.Success ? "Message" : "Error"] = Text(r.Success ? "Ppr_Sent" : r.Error!);
        return RedirectToAction("Details", "Replenishment", new { area = "Warehouse", id = recommendationId });
    }

    private IActionResult Done(int id, PreProcurementResult r, string? successKey = null)
    {
        TempData[r.Success ? "Message" : "Error"] = Text(r.Success ? successKey ?? "Ppr_Saved" : r.Error!);
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Errors are resource keys, except messages passed through from the MR service (already user text).</summary>
    private string Text(string keyOrText)
    {
        var l = _localizer[keyOrText];
        return l.ResourceNotFound ? keyOrText : l.Value;
    }

    private async Task<PreProcurementActor> ActorAsync()
    {
        int? employeeId = null;
        if (_currentUser.UserId.HasValue)
            employeeId = await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
        return new PreProcurementActor(employeeId, _currentUser.Roles, _currentUser.UserName);
    }
}
