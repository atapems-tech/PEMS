using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Identity;
using PEMS.Web.Areas.Admin.Models;
using PEMS.Web.Security;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// طبق سند «PEMS Phase 6 — User Module & Navigation Visibility Architecture»: تنها کنترلر
/// مدیریت Visibility - Role-level Override (Index/SetOverride/ClearOverride) + User-level
/// Effective Visibility با توضیح منبع + Preview As User (فقط نمایش، بدون Impersonation واقعی).
/// عمداً بدون هیچ منطق Authorization جدید - همه چیز یا از NavigationVisibilityResolver
/// (خواندنی) یا INavigationVisibilityOverrideService (نوشتنی روی همان یک جدول Override) می‌آید.
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class NavigationVisibilityController : Controller
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INavigationVisibilityOverrideService _overrides;
    private readonly NavigationVisibilityResolver _resolver;

    public NavigationVisibilityController(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        INavigationVisibilityOverrideService overrides,
        NavigationVisibilityResolver resolver)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _overrides = overrides;
        _resolver = resolver;
    }

    /// <summary>
    /// طبق «PEMS Phase 6 — Hierarchical Role/Discipline Visibility Correction»: این صفحه اکنون
    /// همیشه از روی سلسله‌مراتب واقعی ناوبری (NavigationCatalog، نه فهرست Permission) ساخته
    /// می‌شود - نقش/دیسیپلین Context زیرِ هر گره‌ی ناوبری‌اند، نه ردیف‌های هم‌سطح آن. برای هر نقش
    /// واقعی، DescribeForRoleAsync (بدون تغییر) صدا زده و نتیجه در NavigationVisibilityTreeBuilder
    /// Pivot می‌شود - هیچ تصمیم Visibility تازه‌ای این‌جا گرفته نمی‌شود.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Navigation Visibility";
        ViewData["Section"] = "Administration";

        var roles = await _roleManager.Roles
            .Where(r => r.Name != null)
            .Select(r => new { r.Id, r.Name })
            .OrderBy(r => r.Name)
            .ToListAsync();

        var roleTuples = roles.Select(r => (r.Id, r.Name!)).ToList();

        var rowsByRole = new Dictionary<string, List<NavigationVisibilityRowDescription>>();
        foreach (var role in roleTuples)
            rowsByRole[role.Item2] = await _resolver.DescribeForRoleAsync(role.Item2);

        var tree = NavigationVisibilityTreeBuilder.BuildTree(roleTuples, rowsByRole);

        return View(tree);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetOverride(int roleId, string moduleKey, string? itemKey, bool isVisible, string? reason)
    {
        await _overrides.SetOverrideAsync(roleId, moduleKey, string.IsNullOrWhiteSpace(itemKey) ? null : itemKey, isVisible, reason, User.Identity?.Name ?? "system");
        TempData["Success"] = "Visibility override saved.";
        return RedirectToAction(actionName: nameof(Index), controllerName: null, routeValues: null, fragment: NodeAnchor(moduleKey, itemKey));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearOverride(int roleId, string moduleKey, string? itemKey)
    {
        await _overrides.ClearOverrideAsync(roleId, moduleKey, string.IsNullOrWhiteSpace(itemKey) ? null : itemKey);
        TempData["Success"] = "Override cleared - back to default visibility.";
        return RedirectToAction(actionName: nameof(Index), controllerName: null, routeValues: null, fragment: NodeAnchor(moduleKey, itemKey));
    }

    private static string NodeAnchor(string moduleKey, string? itemKey) =>
        $"node-{moduleKey}-{itemKey}";

    /// <summary>
    /// طبق بند «User-level Effective Visibility view with source explanation» + «Preview As User»:
    /// یک صفحه برای هر دو - انتخاب هر کاربر، سایدبار واقعی‌اش (union همه‌ی نقش‌هایش، دقیقاً همان
    /// مسیر NavigationVisibilityResolver.ResolveAsync که _SidebarMenu.cshtml خودش هم استفاده
    /// می‌کند) را فقط نمایش می‌دهد - بدون هیچ تغییری در Session/Identity/Cookie کاربر جاری.
    /// </summary>
    public async Task<IActionResult> EffectiveUser(int? userId)
    {
        ViewData["Title"] = "Effective Navigation Visibility";
        ViewData["Section"] = "Administration";

        var users = await _userManager.Users
            .Select(u => new { u.Id, u.UserName, u.FullName })
            .OrderBy(u => u.UserName)
            .ToListAsync();

        var vm = new NavigationVisibilityUserVm
        {
            AllUsers = users.Select(u => (u.Id, string.IsNullOrWhiteSpace(u.FullName) ? u.UserName! : $"{u.FullName} ({u.UserName})")).ToList(),
            SelectedUserId = userId
        };

        if (userId.HasValue)
        {
            var user = await _userManager.FindByIdAsync(userId.Value.ToString());
            if (user == null) return NotFound();

            vm.RoleNames = (await _userManager.GetRolesAsync(user)).OrderBy(r => r).ToList();

            var snapshot = await _resolver.ResolveAsync(vm.RoleNames);
            vm.Tree = NavigationVisibilityTreeBuilder.BuildUserTree(snapshot);
        }

        return View(vm);
    }
}
