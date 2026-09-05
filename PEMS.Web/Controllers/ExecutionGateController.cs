using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق CMMS-10f: Execution Gate همیشه زیرمجموعه‌ی یک Permit است - هیچ Index مستقلی ندارد؛ همه‌ی
/// اکشن‌ها به Permit/Details/{permitId} برمی‌گردند. StartWork/EmergencyOverride هر دو سمت
/// سرور، درون IExecutionGateService، دوباره کامل ارزیابی می‌شوند - هرگز به وضعیت UI قبلی
/// اعتماد نمی‌شود (بند ۷ دستورالعمل).
/// </summary>
public class ExecutionGateController : Controller
{
    private readonly IExecutionGateService _service;
    private readonly IRoleModuleActionService _roleModuleActionService;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermitAuthorityService _permitAuthorityService;

    public ExecutionGateController(IExecutionGateService service, IRoleModuleActionService roleModuleActionService,
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
    public async Task<IActionResult> StartWork(int permitId, string? webAuthnAssertionJson, string? webAuthnOptionsJson)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "ExecutionGate", "StartWork"))
            return Forbid();

        // طبق سند PEMS-SEC-PERMIT-AUTH-001: StartWork یکی از حساس‌ترین اقدامات کل سیستم است -
        // Step-up Authentication الزامی، بدون استثنا.
        var employeeId = await GetCurrentEmployeeIdAsync();
        var (authOk, authError) = await _permitAuthorityService.AuthorizeSensitiveActionAsync(
            _currentUser.UserId ?? 0, employeeId, _currentUser.Roles.ToList(), permitId, "ExecutionGate", "StartWork", "ExecutionGate",
            webAuthnAssertionJson, webAuthnOptionsJson);
        if (!authOk)
        {
            TempData["Error"] = authError;
            TempData["ErrorArea"] = "ExecutionGate";
            return RedirectToAction("Details", "Permit", new { id = permitId });
        }

        var (success, error) = await _service.StartWorkAsync(permitId, employeeId);

        TempData[success ? "Message" : "Error"] = success ? "کار آغاز شد - Permit به Active منتقل شد." : error;
        if (!success) TempData["ErrorArea"] = "ExecutionGate";
        return RedirectToAction("Details", "Permit", new { id = permitId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmergencyOverride(int permitId, string reason, string? webAuthnAssertionJson, string? webAuthnOptionsJson)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "ExecutionGate", "EmergencyOverride"))
            return Forbid();

        // طبق سند PEMS-SEC-PERMIT-AUTH-001 بند ۹: Emergency Override هم باید Actor را با
        // Step-up Authentication به‌طور قطعی احراز کند - نه فقط بر اساس نقش.
        var emergencyEmployeeId = await GetCurrentEmployeeIdAsync();
        var (authOk, authError) = await _permitAuthorityService.AuthorizeSensitiveActionAsync(
            _currentUser.UserId ?? 0, emergencyEmployeeId, _currentUser.Roles.ToList(), permitId, "ExecutionGate", "EmergencyOverride", "EmergencyOverride",
            webAuthnAssertionJson, webAuthnOptionsJson);
        if (!authOk)
        {
            TempData["Error"] = authError;
            TempData["ErrorArea"] = "ExecutionGate";
            return RedirectToAction("Details", "Permit", new { id = permitId });
        }

        var (success, error) = await _service.EmergencyOverrideAsync(permitId, reason, emergencyEmployeeId, _currentUser.Roles.ToList());

        TempData[success ? "Message" : "Error"] = success ? "Emergency Override ثبت و کار آغاز شد." : error;
        if (!success) TempData["ErrorArea"] = "ExecutionGate";
        return RedirectToAction("Details", "Permit", new { id = permitId });
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }
}
