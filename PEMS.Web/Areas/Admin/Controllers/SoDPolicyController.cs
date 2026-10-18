using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Web.Areas.Admin.Models;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// طبق «PEMS Authorization Architecture Completion» فاز ۴ بند ۲۱: مدیریت Admin برای SoDPolicy
/// موجود (نه یک مدل SoD تازه). سیاست‌های Seed‌شده (خودتاییدی EngineeringDocuments/WorkOrder/...)
/// دست‌نخورده باقی می‌مانند مگر مدیر خودش از همین صفحه تغییرشان دهد. حذف فیزیکی وجود ندارد -
/// فقط IsActive (Enable/Disable)، همان الگوی نرم‌حذف پروژه.
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class SoDPolicyController : Controller
{
    private readonly PEMSDbContext _context;

    public SoDPolicyController(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Separation of Duties";
        ViewData["Section"] = "Administration";

        var list = await _context.SoDPolicies
            .Include(x => x.PermissionA)
            .Include(x => x.PermissionB)
            .OrderBy(x => x.PermissionA!.Name)
            .Select(x => new SoDPolicyListItemVm
            {
                Id = x.SoDPolicyId,
                PermissionA = x.PermissionA!.Name,
                PermissionB = x.PermissionB!.Name,
                Description = x.Description,
                IsActive = x.IsActive
            })
            .ToListAsync();

        return View(list);
    }

    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "New SoD Policy";
        ViewData["Section"] = "Administration";
        return View(await BuildFormAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SoDPolicyFormVm model)
    {
        ViewData["Title"] = "New SoD Policy";
        ViewData["Section"] = "Administration";

        if (model.PermissionAId.HasValue && model.PermissionBId.HasValue && model.PermissionAId == model.PermissionBId)
            ModelState.AddModelError(nameof(model.PermissionBId), "A permission cannot conflict with itself.");

        if (ModelState.IsValid)
        {
            // طبق همان چک موجود در AccessControlSeeder.SeedPhase5GovernanceAsync: تعارض دوطرفه است
            // (A‌-با‌B همان B‌-با‌A است) - نه فقط ایندکس یکتای دیتابیس که فقط ترتیب دقیق را می‌گیرد.
            var exists = await _context.SoDPolicies.AnyAsync(x => x.IsActive &&
                ((x.PermissionAId == model.PermissionAId && x.PermissionBId == model.PermissionBId) ||
                 (x.PermissionAId == model.PermissionBId && x.PermissionBId == model.PermissionAId)));

            if (exists)
            {
                ModelState.AddModelError(string.Empty, "An active policy for this permission pair already exists.");
            }
            else
            {
                _context.SoDPolicies.Add(new SoDPolicy
                {
                    PermissionAId = model.PermissionAId!.Value,
                    PermissionBId = model.PermissionBId!.Value,
                    Description = model.Description,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                });
                await _context.SaveChangesAsync();
                TempData["Message"] = "SoD policy created.";
                return RedirectToAction(nameof(Index));
            }
        }

        var populated = await BuildFormAsync();
        populated.PermissionAId = model.PermissionAId;
        populated.PermissionBId = model.PermissionBId;
        populated.Description = model.Description;
        return View(populated);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var policy = await _context.SoDPolicies.FirstOrDefaultAsync(x => x.SoDPolicyId == id);
        if (policy == null) return NotFound();

        policy.IsActive = !policy.IsActive;
        await _context.SaveChangesAsync();

        TempData["Message"] = policy.IsActive ? "Policy enabled." : "Policy disabled.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<SoDPolicyFormVm> BuildFormAsync()
    {
        var permissions = await _context.Permissions
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new { p.PermissionId, p.Name })
            .ToListAsync();

        return new SoDPolicyFormVm { AllPermissions = permissions.Select(p => (p.PermissionId, p.Name)).ToList() };
    }
}
