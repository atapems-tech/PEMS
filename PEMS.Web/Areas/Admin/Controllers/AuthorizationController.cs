using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Web.Areas.Admin.Models;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// طبق «PEMS Authorization Architecture Completion» فاز ۴ بند ۱۵-۱۹: Effective Permissions +
/// Permission Simulator - هر دو صرفاً یک UI تشخیصی روی IAccessControlService/IPermissionService
/// موجود هستند؛ هیچ منطق مجوزدهی تازه‌ای این‌جا نوشته نشده. برای فهرست کامل Permission (نه یک
/// User خاص)، عمداً از AuthorizeForRolesAsync (بدون Audit) استفاده می‌شود، نه AuthorizeAsync
/// (که برای هر بررسی یک ردیف AuthorizationAuditLog می‌نویسد) - وگرنه هر بار باز کردن این صفحه
/// ۲۰۰+ ردیف Audit جعلی تولید می‌کرد. Simulate عمداً از AuthorizeAsync استفاده می‌کند - آن یک
/// بررسی صریح و یک‌باره‌ی مدیر است، نه یک پویش گروهی.
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class AuthorizationController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly IAccessControlService _accessControlService;
    private readonly IUserAuthorizationSummaryService _summaryService;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthorizationController(
        PEMSDbContext context,
        IAccessControlService accessControlService,
        IUserAuthorizationSummaryService summaryService,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _accessControlService = accessControlService;
        _summaryService = summaryService;
        _roleManager = roleManager;
        _userManager = userManager;
    }

    public async Task<IActionResult> EffectiveRole(string? roleName)
    {
        ViewData["Title"] = "Effective Permissions";
        ViewData["Section"] = "Administration";

        var vm = new EffectiveRoleVm
        {
            AllRoles = await _roleManager.Roles.Select(r => r.Name!).OrderBy(n => n).ToArrayAsync(),
            SelectedRole = roleName
        };

        if (!string.IsNullOrWhiteSpace(roleName))
            vm.Rows = await BuildRoleRowsAsync(new[] { roleName });

        return View(vm);
    }

    public async Task<IActionResult> EffectiveUser(int? userId)
    {
        ViewData["Title"] = "Effective Permissions";
        ViewData["Section"] = "Administration";

        var users = await _userManager.Users
            .Select(u => new { u.Id, u.UserName, u.FullName })
            .OrderBy(u => u.UserName)
            .ToListAsync();

        var vm = new EffectiveUserVm
        {
            AllUsers = users.Select(u => (u.Id, string.IsNullOrWhiteSpace(u.FullName) ? u.UserName! : $"{u.FullName} ({u.UserName})")).ToList(),
            SelectedUserId = userId
        };

        if (userId.HasValue)
        {
            var user = await _userManager.FindByIdAsync(userId.Value.ToString());
            if (user == null) return NotFound();

            vm.RoleNames = (await _userManager.GetRolesAsync(user)).OrderBy(r => r).ToList();

            var now = DateTime.Now;
            vm.Assignments = await _context.RoleAssignments
                .Where(a => a.UserId == userId.Value && a.IsActive)
                .Join(_context.Roles, a => a.RoleId, r => r.Id, (a, r) => new RoleAssignmentRowVm
                {
                    RoleName = r.Name!,
                    ScopeType = a.ScopeType,
                    ScopeValue = a.ScopeValue,
                    AssignmentType = a.AssignmentType,
                    EffectiveFrom = a.EffectiveFrom,
                    EffectiveTo = a.EffectiveTo
                })
                .ToListAsync();

            // طبق بند ۱۷: نقش‌های مستقیم AspNetUserRoles هم به‌صورت Scope=Global در نتیجه دیده
            // شوند - دقیقاً هم‌الگو با ResolveUserRoleGrantsAsync خودِ AccessControlService.
            var directOnly = vm.RoleNames.Where(rn => !vm.Assignments.Any(a => a.RoleName == rn)).ToList();
            vm.Assignments.AddRange(directOnly.Select(rn => new RoleAssignmentRowVm { RoleName = rn, ScopeType = "Global", AssignmentType = "Direct (AspNetUserRoles)" }));

            vm.Rows = await BuildRoleRowsAsync(vm.RoleNames.ToArray());
        }

        return View(vm);
    }

    /// <summary>
    /// طبق «PEMS — Enterprise User-Centric Authorization Workspace Consolidation»: منطق قبلاً
    /// این‌جا خصوصی بود (BuildRoleRowsAsync) - حالا به IUserAuthorizationSummaryService منتقل شده
    /// تا Admin/Users/Details تازه هم بتواند از همان یک منبع بخواند (بدون تکرار منطق). رفتار این
    /// صفحه کاملاً بدون تغییر می‌ماند - فقط نگاشت DTO مشترک به همین ViewModel محلی.
    /// </summary>
    private async Task<List<EffectivePermissionRowVm>> BuildRoleRowsAsync(string[] roles)
    {
        var dtos = await _summaryService.GetEffectivePermissionsForRolesAsync(roles);
        return dtos.Select(d => new EffectivePermissionRowVm
        {
            ResourceName = d.ResourceName,
            ActionName = d.ActionName,
            Allowed = d.Allowed,
            IsDestructive = d.IsDestructive,
            Detail = d.Detail
        }).ToList();
    }

    public async Task<IActionResult> Simulate()
    {
        ViewData["Title"] = "Permission Simulator";
        ViewData["Section"] = "Administration";
        return View(await BuildSimulateFormAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Simulate(SimulateFormVm form)
    {
        ViewData["Title"] = "Permission Simulator";
        ViewData["Section"] = "Administration";

        var populated = await BuildSimulateFormAsync();
        populated.UserId = form.UserId;
        populated.ResourceName = form.ResourceName;
        populated.ActionName = form.ActionName;
        populated.ResourceId = form.ResourceId;

        if (!ModelState.IsValid || !form.UserId.HasValue || string.IsNullOrWhiteSpace(form.ResourceName) || string.IsNullOrWhiteSpace(form.ActionName))
            return View(populated);

        // طبق بند ۱۹: مستقیماً موتور واقعی - همان مسیری که یک درخواست HTTP واقعی طی می‌کند
        // (Role → Scope → SoD)، نه یک شبیه‌سازی جدا.
        var decision = await _accessControlService.AuthorizeAsync(new AccessControlRequest
        {
            UserId = form.UserId.Value,
            ResourceName = form.ResourceName.Trim(),
            ActionName = form.ActionName.Trim(),
            ResourceId = string.IsNullOrWhiteSpace(form.ResourceId) ? null : form.ResourceId.Trim(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        populated.Result = new SimulateResultVm
        {
            Allowed = decision.Allowed,
            Reason = decision.Reason.ToString(),
            Detail = decision.Detail,
            MatchedRoleName = decision.MatchedRoleName,
            MatchedScopeType = decision.MatchedScopeType,
            MatchedScopeValue = decision.MatchedScopeValue
        };

        return View(populated);
    }

    private async Task<SimulateFormVm> BuildSimulateFormAsync()
    {
        var users = await _userManager.Users
            .Select(u => new { u.Id, u.UserName, u.FullName })
            .OrderBy(u => u.UserName)
            .ToListAsync();

        var permissionNames = await _context.Permissions
            .Where(p => p.IsActive)
            .OrderBy(p => p.ResourceName).ThenBy(p => p.ActionName)
            .Select(p => p.Name)
            .ToListAsync();

        return new SimulateFormVm
        {
            AllUsers = users.Select(u => (u.Id, string.IsNullOrWhiteSpace(u.FullName) ? u.UserName! : $"{u.FullName} ({u.UserName})")).ToList(),
            AllPermissionNames = permissionNames
        };
    }
}
