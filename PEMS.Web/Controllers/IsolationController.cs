using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق CMMS-10c: IsolationPoint همیشه زیرمجموعه‌ی یک Permit است - هیچ Index مستقلی ندارد؛
/// همه‌ی اکشن‌ها به Permit/Details/{permitId} برمی‌گردند. مجوزدهی از طریق IRoleModuleActionService
/// با Module="LOTO" (نه "Permit") - طبق طراحی ADR-02 که از ابتدا این تفکیک را پیش‌بینی کرده بود.
/// </summary>
public class IsolationController : Controller
{
    private readonly IIsolationService _service;
    private readonly IRoleModuleActionService _roleModuleActionService;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermitAuthorityService _permitAuthorityService;

    public IsolationController(IIsolationService service, IRoleModuleActionService roleModuleActionService,
        PEMSDbContext context, ICurrentUserService currentUser, IPermitAuthorityService permitAuthorityService)
    {
        _service = service;
        _roleModuleActionService = roleModuleActionService;
        _context = context;
        _currentUser = currentUser;
        _permitAuthorityService = permitAuthorityService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int permitId, string energyType, string location, string? normalState, string? requiredIsolationState,
        string? isolationMethod, int? equipmentId)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "LOTO", "Create"))
            return Forbid();

        var (success, error, _) = await _service.CreateAsync(
            permitId, energyType, location, normalState, requiredIsolationState, isolationMethod,
            equipmentId, null, null);

        TempData[success ? "Message" : "Error"] = success ? "نقطه‌ی ایزوله‌سازی ثبت شد." : error;
        return RedirectToAction("Details", "Permit", new { id = permitId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkIsolated(int id, int permitId, string? lockNumber, string? tagNumber, string? reason)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "LOTO", "Isolate"))
            return Forbid();

        var (success, error) = await _service.AdvanceStatusAsync(
            id, "Isolated", reason, await GetCurrentEmployeeIdAsync(), lockNumber, tagNumber);

        TempData[success ? "Message" : "Error"] = success ? "نقطه ایزوله شد." : error;
        return RedirectToAction("Details", "Permit", new { id = permitId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(int id, int permitId, string? reason, string? webAuthnAssertionJson, string? webAuthnOptionsJson)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "LOTO", "Verify"))
            return Forbid();

        // طبق سند PEMS-SEC-PERMIT-AUTH-001: تایید ایزوله‌سازی (نزدیک‌ترین معادل "LOTO Approval")
        // نیازمند Step-up Authentication است.
        var employeeId = await GetCurrentEmployeeIdAsync();
        var (authOk, authError) = await _permitAuthorityService.AuthorizeSensitiveActionAsync(
            _currentUser.UserId ?? 0, employeeId, _currentUser.Roles.ToList(), permitId, "LOTO", "Verify", "LOTO",
            webAuthnAssertionJson, webAuthnOptionsJson);
        if (!authOk)
        {
            TempData["Error"] = authError;
            TempData["ErrorArea"] = "LOTO";
            return RedirectToAction("Details", "Permit", new { id = permitId });
        }

        var (success, error) = await _service.AdvanceStatusAsync(id, "Verified", reason, employeeId);

        TempData[success ? "Message" : "Error"] = success ? "ایزوله‌سازی تایید شد." : error;
        if (!success) TempData["ErrorArea"] = "LOTO";
        return RedirectToAction("Details", "Permit", new { id = permitId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deisolate(int id, int permitId, string? reason)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "LOTO", "Deisolate"))
            return Forbid();

        var (success, error) = await _service.AdvanceStatusAsync(id, "Deisolated", reason, await GetCurrentEmployeeIdAsync());

        TempData[success ? "Message" : "Error"] = success ? "ایزوله‌سازی برداشته شد (De-isolation)." : error;
        return RedirectToAction("Details", "Permit", new { id = permitId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, int permitId, string? reason)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "LOTO", "Cancel"))
            return Forbid();

        var (success, error) = await _service.AdvanceStatusAsync(id, "Cancelled", reason, await GetCurrentEmployeeIdAsync());

        TempData[success ? "Message" : "Error"] = success ? "نقطه‌ی ایزوله‌سازی لغو شد." : error;
        return RedirectToAction("Details", "Permit", new { id = permitId });
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }
}
