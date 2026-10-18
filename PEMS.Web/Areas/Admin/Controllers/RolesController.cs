using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Web.Areas.Admin.Models;

namespace PEMS.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class RolesController : Controller
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PEMSDbContext _context;


    public RolesController(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        PEMSDbContext context)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _context = context;
    }



    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Roles";
        ViewData["Section"] = "Administration";
        ViewData["HelpKey"] = "Admin.Roles.Index";

        var roles = await _roleManager.Roles.ToListAsync();

        // یک کوئری برای شمارش کاربران همه‌ی نقش‌ها، به‌جای یک کوئری جداگانه به ازای هر نقش
        var userCountByRoleId = (await _context.UserRoles
            .GroupBy(ur => ur.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToListAsync())
            .ToDictionary(x => x.RoleId, x => x.Count);

        // طبق «PEMS Authorization Architecture Completion» فاز ۴ بند ۹/۱۴: شمارش RolePermissionGrant
        // فعال به‌ازای هر نقش - منبع واقعی زمان اجرا (نه RolePermission قدیمی)، تا مدیر بتواند نقشی
        // که «هنوز پیکربندی نشده» را از نقشی که واقعاً مجوز دارد تشخیص دهد.
        var grantCountByRoleId = (await _context.RolePermissionGrants
            .Where(g => g.IsActive)
            .GroupBy(g => g.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToListAsync())
            .ToDictionary(x => x.RoleId, x => x.Count);

        var list = roles.Select(role => new RoleListItemViewModel
        {
            Id = role.Id,
            Name = role.Name ?? "",
            DisplayName = role.DisplayName,
            Description = role.Description,
            UserCount = userCountByRoleId.TryGetValue(role.Id, out var count) ? count : 0,
            IsSystemRole = role.IsSystemRole || SystemRoles.All.Contains(role.Name),
            Domain = role.Domain,
            Category = role.Category,
            SortOrder = role.SortOrder,
            PermissionCount = role.Name == SystemRoles.SystemAdmin
                ? -1 // نامحدود (Bypass عمومی) - نه صفر، تا با «پیکربندی‌نشده» اشتباه گرفته نشود
                : grantCountByRoleId.TryGetValue(role.Id, out var g) ? g : 0
        }).ToList();

        // نقش سفارشیِ هم‌معنا با نقش پایه فقط گزارش می‌شود - هرگز خودکار ادغام/تغییر نمی‌کند
        ViewBag.PossibleDuplicates = RoleCatalog.FindPossibleDuplicates(roles.Select(r => (r.Name ?? "", r.DisplayName)));

        return View(list.OrderBy(x => x.SortOrder == 0 ? int.MaxValue : x.SortOrder).ThenBy(x => x.Name).ToList());
    }



    public IActionResult Create()
    {
        ViewData["Title"] = "New Role";
        ViewData["Section"] = "Administration";

        return View(new RoleFormViewModel());
    }



    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RoleFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "New Role";
            ViewData["Section"] = "Administration";

            return View(model);
        }

        var exists = await _roleManager.RoleExistsAsync(model.Name);

        if (exists)
        {
            ModelState.AddModelError(nameof(model.Name), "A role with this name already exists.");

            ViewData["Title"] = "New Role";
            ViewData["Section"] = "Administration";

            return View(model);
        }

        var role = new ApplicationRole(model.Name)
        {
            DisplayName = model.DisplayName,
            Description = model.Description
        };

        await _roleManager.CreateAsync(role);

        // طبق «PEMS Authorization Architecture Completion» فاز ۴ بند ۱۰: نقش تازه هنوز
        // Default-Deny کامل است (هیچ RolePermissionGrant/RolePermission ای خودکار نوشته نمی‌شود
        // - نه از روی نام نقش، نه با کپی از SystemAdmin) - مدیر باید صریحاً پیکربندی کند. برای
        // این‌که این مرحله‌ی بعدی گم نشود، مستقیماً به همان بخش نقش تازه در Permission Matrix
        // هدایت می‌شویم، نه فقط به فهرست نقش‌ها.
        TempData["Message"] = "Role created. Configure its permissions below before assigning it to users.";

        return RedirectToAction("Index", "PermissionMatrix", new { area = "Admin" }, $"role-{role.Name}");
    }



    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());

        if (role == null)
            return NotFound();

        if (role.IsSystemRole || SystemRoles.All.Contains(role.Name))
        {
            TempData["Error"] = "System roles cannot be deleted.";

            return RedirectToAction(nameof(Index));
        }

        await _roleManager.DeleteAsync(role);

        TempData["Message"] = "Role deleted";

        return RedirectToAction(nameof(Index));
    }
}
