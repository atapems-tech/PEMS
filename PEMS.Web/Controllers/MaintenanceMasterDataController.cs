using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق PEMS_CMMS_MAINTENANCE_ORGANIZATION_BLUEPRINT.md بخش ۲۶: به‌جای چهار صفحه‌ی جدا و
/// تقریباً یکسان برای هر Lookup کوچک (MaintenanceGroup/Skill/Qualification/Certification)،
/// یک صفحه با تب - همون روحیه‌ی ممیزی سایدباری که قبلاً روی این پروژه انجام شد.
/// </summary>
/// <remarks>
/// طبق «PEMS — P1 Organization &amp; Master Data Authorization Hardening»: این Controller قبلاً
/// هیچ بررسی مجوزی نداشت - Create*/Toggle*Active برای هر کاربر احرازهویت‌شده‌ای باز بودند.
/// ماژول جدید "MaintenanceMasterData" چون هیچ ماژول موجودی این قابلیت (پیکربندی مرجع دامنه‌ی
/// نیروی کار تعمیرات) را پوشش نمی‌دهد - همون نقش‌های عملیاتی CMMS که سایدبار از قبل نشان می‌دهد.
/// </remarks>
public class MaintenanceMasterDataController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public MaintenanceMasterDataController(PEMSDbContext context, ICurrentUserService currentUser,
        IPermissionService permissionService)
    {
        _context = context;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "View"))
            return Forbid();

        ViewData["Title"] = "Maintenance Master Data";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();

        var groups = await _context.MaintenanceGroups
            .Include(x => x.Discipline)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.MaintenanceGroupId)
            .ToListAsync();

        ViewBag.Skills = await _context.Skills
            .Include(x => x.Discipline)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.SkillId)
            .ToListAsync();

        ViewBag.Qualifications = await _context.Qualifications
            .Include(x => x.Discipline)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.QualificationId)
            .ToListAsync();

        ViewBag.Certifications = await _context.Certifications
            .Include(x => x.Discipline)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.CertificationId)
            .ToListAsync();

        return View(groups);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGroup(string code, string name, int disciplineId, string? description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Create"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "کد و نام گروه تعمیراتی الزامی هستند.";
            return RedirectToAction(nameof(Index));
        }

        var duplicate = await _context.MaintenanceGroups.AnyAsync(x => x.Name == name.Trim() || x.Code == code.Trim());
        if (duplicate)
        {
            TempData["Error"] = "گروه تعمیراتی با همین کد یا نام از قبل وجود دارد.";
            return RedirectToAction(nameof(Index));
        }

        await _context.MaintenanceGroups.AddAsync(new MaintenanceGroup
        {
            Code = code.Trim(),
            Name = name.Trim(),
            DisciplineId = disciplineId,
            Description = description,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = "گروه تعمیراتی ساخته شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleGroupActive(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Edit"))
            return Forbid();

        var group = await _context.MaintenanceGroups.FirstOrDefaultAsync(x => x.MaintenanceGroupId == id);
        if (group == null) return NotFound();

        group.IsActive = !group.IsActive;
        await _context.SaveChangesAsync();

        TempData["Message"] = "وضعیت گروه تعمیراتی به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSkill(string name, int? disciplineId, string? description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Create"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "نام مهارت الزامی است.";
            return RedirectToAction(nameof(Index));
        }

        var duplicate = await _context.Skills.AnyAsync(x => x.Name == name.Trim());
        if (duplicate)
        {
            TempData["Error"] = "مهارتی با همین نام از قبل وجود دارد.";
            return RedirectToAction(nameof(Index));
        }

        await _context.Skills.AddAsync(new Skill
        {
            Name = name.Trim(),
            DisciplineId = disciplineId,
            Description = description,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = "مهارت ساخته شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSkillActive(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Edit"))
            return Forbid();

        var skill = await _context.Skills.FirstOrDefaultAsync(x => x.SkillId == id);
        if (skill == null) return NotFound();

        skill.IsActive = !skill.IsActive;
        await _context.SaveChangesAsync();

        TempData["Message"] = "وضعیت مهارت به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateQualification(string name, int? disciplineId, string? description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Create"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "نام صلاحیت الزامی است.";
            return RedirectToAction(nameof(Index));
        }

        if (await _context.Qualifications.AnyAsync(x => x.Name == name.Trim()))
        {
            TempData["Error"] = "صلاحیتی با همین نام از قبل وجود دارد.";
            return RedirectToAction(nameof(Index));
        }

        await _context.Qualifications.AddAsync(new Qualification
        {
            Name = name.Trim(),
            DisciplineId = disciplineId,
            Description = description,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = "صلاحیت ساخته شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleQualificationActive(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Edit"))
            return Forbid();

        var qualification = await _context.Qualifications.FirstOrDefaultAsync(x => x.QualificationId == id);
        if (qualification == null) return NotFound();

        qualification.IsActive = !qualification.IsActive;
        await _context.SaveChangesAsync();

        TempData["Message"] = "وضعیت صلاحیت به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCertification(string name, int? disciplineId, string? description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Create"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "نام گواهینامه الزامی است.";
            return RedirectToAction(nameof(Index));
        }

        if (await _context.Certifications.AnyAsync(x => x.Name == name.Trim()))
        {
            TempData["Error"] = "گواهینامه‌ای با همین نام از قبل وجود دارد.";
            return RedirectToAction(nameof(Index));
        }

        await _context.Certifications.AddAsync(new Certification
        {
            Name = name.Trim(),
            DisciplineId = disciplineId,
            Description = description,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = "گواهینامه ساخته شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCertificationActive(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Edit"))
            return Forbid();

        var certification = await _context.Certifications.FirstOrDefaultAsync(x => x.CertificationId == id);
        if (certification == null) return NotFound();

        certification.IsActive = !certification.IsActive;
        await _context.SaveChangesAsync();

        TempData["Message"] = "وضعیت گواهینامه به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }
}
