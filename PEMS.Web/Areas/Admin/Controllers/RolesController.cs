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

        var list = roles.Select(role => new RoleListItemViewModel
        {
            Id = role.Id,
            Name = role.Name ?? "",
            DisplayName = role.DisplayName,
            Description = role.Description,
            UserCount = userCountByRoleId.TryGetValue(role.Id, out var count) ? count : 0,
            IsSystemRole = SystemRoles.All.Contains(role.Name)
        }).ToList();

        return View(list.OrderBy(x => x.Name).ToList());
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

        TempData["Message"] = "Role created successfully";

        return RedirectToAction(nameof(Index));
    }



    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());

        if (role == null)
            return NotFound();

        if (SystemRoles.All.Contains(role.Name))
        {
            TempData["Error"] = "System roles cannot be deleted.";

            return RedirectToAction(nameof(Index));
        }

        await _roleManager.DeleteAsync(role);

        TempData["Message"] = "Role deleted";

        return RedirectToAction(nameof(Index));
    }
}
