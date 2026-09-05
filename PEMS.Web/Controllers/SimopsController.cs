using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق CMMS-10e: SimopsConflict همیشه به دو Permit وصل است - هیچ Index مستقلی ندارد؛ همه‌ی
/// اکشن‌ها به Permit/Details/{permitId} برمی‌گردند. مجوزدهی از طریق IRoleModuleActionService
/// با Module="Simops". هر اکشن permitId ارسالی را در برابر هر دو والد تعارض تایید می‌کند
/// (SimopsService.AdvanceStatusAsync) - طبق نیاز صریح دستورالعمل برای رفع نقص مالکیت CMMS-10c.
/// </summary>
public class SimopsController : Controller
{
    private readonly ISimopsService _service;
    private readonly IRoleModuleActionService _roleModuleActionService;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermitAuthorityService _permitAuthorityService;

    public SimopsController(ISimopsService service, IRoleModuleActionService roleModuleActionService,
        PEMSDbContext context, ICurrentUserService currentUser, IPermitAuthorityService permitAuthorityService)
    {
        _service = service;
        _roleModuleActionService = roleModuleActionService;
        _context = context;
        _currentUser = currentUser;
        _permitAuthorityService = permitAuthorityService;
    }

    private async Task<(bool Ok, string? Error, int? EmployeeId)> AuthorizeAsync(int permitId, string action, string? webAuthnAssertionJson, string? webAuthnOptionsJson)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        var (ok, error) = await _permitAuthorityService.AuthorizeSensitiveActionAsync(
            _currentUser.UserId ?? 0, employeeId, _currentUser.Roles.ToList(), permitId, "Simops", action, "SIMOPS",
            webAuthnAssertionJson, webAuthnOptionsJson);
        return (ok, error, employeeId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int permitId, int otherPermitId)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Simops", "Create"))
            return Forbid();

        var (success, error, _) = await _service.CreateAsync(permitId, otherPermitId, await GetCurrentEmployeeIdAsync());

        TempData[success ? "Message" : "Error"] = success ? "تعارض ثبت شد." : error;
        return RedirectToAction("Details", "Permit", new { id = permitId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id, int permitId, string? reason, string? webAuthnAssertionJson, string? webAuthnOptionsJson)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Simops", "Confirm"))
            return Forbid();

        var (authOk, authError, employeeId) = await AuthorizeAsync(permitId, "Confirm", webAuthnAssertionJson, webAuthnOptionsJson);
        if (!authOk)
        {
            TempData["Error"] = authError;
            TempData["ErrorArea"] = "Simops";
            return RedirectToAction("Details", "Permit", new { id = permitId });
        }

        var (success, error) = await _service.ConfirmAsync(id, permitId, reason, employeeId);

        TempData[success ? "Message" : "Error"] = success ? "تعارض تایید شد." : error;
        if (!success) TempData["ErrorArea"] = "Simops";
        return RedirectToAction("Details", "Permit", new { id = permitId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id, int permitId, string resolutionNotes, string? webAuthnAssertionJson, string? webAuthnOptionsJson)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Simops", "Resolve"))
            return Forbid();

        var (authOk, authError, employeeId) = await AuthorizeAsync(permitId, "Resolve", webAuthnAssertionJson, webAuthnOptionsJson);
        if (!authOk)
        {
            TempData["Error"] = authError;
            TempData["ErrorArea"] = "Simops";
            return RedirectToAction("Details", "Permit", new { id = permitId });
        }

        var (success, error) = await _service.ResolveAsync(id, permitId, resolutionNotes, employeeId);

        TempData[success ? "Message" : "Error"] = success ? "تعارض رفع شد." : error;
        if (!success) TempData["ErrorArea"] = "Simops";
        return RedirectToAction("Details", "Permit", new { id = permitId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptRisk(int id, int permitId, string justification, string? webAuthnAssertionJson, string? webAuthnOptionsJson)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Simops", "AcceptRisk"))
            return Forbid();

        var (authOk, authError, employeeId) = await AuthorizeAsync(permitId, "AcceptRisk", webAuthnAssertionJson, webAuthnOptionsJson);
        if (!authOk)
        {
            TempData["Error"] = authError;
            TempData["ErrorArea"] = "Simops";
            return RedirectToAction("Details", "Permit", new { id = permitId });
        }

        var (success, error) = await _service.AcceptRiskAsync(id, permitId, justification, employeeId);

        TempData[success ? "Message" : "Error"] = success ? "ریسک پذیرفته شد." : error;
        if (!success) TempData["ErrorArea"] = "Simops";
        return RedirectToAction("Details", "Permit", new { id = permitId });
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }
}
