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

        // ==============================================================================
        // طبق «PEMS — Enterprise Maintenance Master Data & Standards Implementation» بند ۱۶/۲۱:
        // پروجکشن سبک (بدون Include سنگین کاتالوگ کامل Applicability) - فقط برای نمایش لیست/
        // Toggle لازم است؛ AsNoTracking چون فقط‌خوانی است.
        // ==============================================================================
        ViewBag.MaintenanceTypes = await _context.MaintenanceTypes.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .ToListAsync();

        ViewBag.MaintenanceStrategyTypes = await _context.MaintenanceStrategyTypes.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .ToListAsync();

        ViewBag.EquipmentClasses = await _context.EquipmentClasses.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .ToListAsync();

        ViewBag.StandardReferences = await _context.StandardReferences.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Organization).ThenBy(x => x.StandardNumber)
            .ToListAsync();

        ViewBag.MaintenanceActivities = await _context.MaintenanceActivities.AsNoTracking()
            .Include(x => x.MaintenanceGroup)
            .Include(x => x.MaintenanceType)
            .Where(x => x.IsActive)
            .OrderBy(x => x.MaintenanceGroup!.Name).ThenBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .ToListAsync();

        // ==============================================================================
        // طبق «PEMS Phase 2 — Equipment Classification + ISO 14224 Failure Taxonomy + PM/Job Plan
        // Intelligence» بند ۳۵/۴۱: دو تب جدید (Failure Modes/PM Templates) - همان ماژول مجوز
        // "MaintenanceMasterData" استفاده می‌شود (طبق بند ۴۲ - بدون ماژول مجوز جدید).
        // ==============================================================================
        ViewBag.FailureModes = await _context.FailureModes.AsNoTracking()
            .Include(x => x.EquipmentClass)
            .Include(x => x.FailureEffect)
            .Include(x => x.FailureConsequence)
            .Where(x => x.IsActive)
            .OrderBy(x => x.EquipmentClass!.Name).ThenBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .ToListAsync();

        ViewBag.PMTemplates = await _context.PMTemplates.AsNoTracking()
            .Include(x => x.EquipmentClass)
            .Include(x => x.MaintenanceStrategyType)
            .Where(x => x.IsActive)
            .OrderBy(x => x.EquipmentClass!.Name).ThenBy(x => x.DisplayOrder).ThenBy(x => x.Name)
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

    // ==============================================================================
    // طبق «PEMS — Enterprise Maintenance Master Data & Standards Implementation» بند ۱۶: پنج تب
    // جدید (Maintenance Type/Strategy Type/Equipment Class/Standard Reference/Activity) - همون
    // الگوی Create/Toggle بالا؛ رکوردهای IsSystemDefault=true فقط Deactivate می‌شوند، هرگز
    // Hard-Delete (طبق بند ۱۹) - این Controller از ابتدا هیچ اکشن Delete ای برای هیچ تبی نداشت،
    // پس نیازی به محافظت اضافه نیست.
    // ==============================================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMaintenanceType(string code, string name, string? nameFa, string? description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Create"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "کد و نام Maintenance Type الزامی هستند.";
            return RedirectToAction(nameof(Index));
        }

        if (await _context.MaintenanceTypes.AnyAsync(x => x.Code == code.Trim()))
        {
            TempData["Error"] = "Maintenance Type ای با همین کد از قبل وجود دارد.";
            return RedirectToAction(nameof(Index));
        }

        await _context.MaintenanceTypes.AddAsync(new MaintenanceType
        {
            Code = code.Trim(),
            Name = name.Trim(),
            NameFa = nameFa,
            Description = description,
            IsSystemDefault = false,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Message"] = "Maintenance Type ساخته شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleMaintenanceTypeActive(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Edit"))
            return Forbid();

        var item = await _context.MaintenanceTypes.FirstOrDefaultAsync(x => x.MaintenanceTypeId == id);
        if (item == null) return NotFound();

        item.IsActive = !item.IsActive;
        await _context.SaveChangesAsync();

        TempData["Message"] = "وضعیت Maintenance Type به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMaintenanceStrategyType(string code, string name, string? nameFa, string? description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Create"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "کد و نام Strategy Type الزامی هستند.";
            return RedirectToAction(nameof(Index));
        }

        if (await _context.MaintenanceStrategyTypes.AnyAsync(x => x.Code == code.Trim()))
        {
            TempData["Error"] = "Strategy Type ای با همین کد از قبل وجود دارد.";
            return RedirectToAction(nameof(Index));
        }

        await _context.MaintenanceStrategyTypes.AddAsync(new MaintenanceStrategyType
        {
            Code = code.Trim(),
            Name = name.Trim(),
            NameFa = nameFa,
            Description = description,
            IsSystemDefault = false,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Message"] = "Maintenance Strategy Type ساخته شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleMaintenanceStrategyTypeActive(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Edit"))
            return Forbid();

        var item = await _context.MaintenanceStrategyTypes.FirstOrDefaultAsync(x => x.MaintenanceStrategyTypeId == id);
        if (item == null) return NotFound();

        item.IsActive = !item.IsActive;
        await _context.SaveChangesAsync();

        TempData["Message"] = "وضعیت Strategy Type به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEquipmentClass(string code, string name, string? nameFa, string? description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Create"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "کد و نام Equipment Class الزامی هستند.";
            return RedirectToAction(nameof(Index));
        }

        if (await _context.EquipmentClasses.AnyAsync(x => x.Code == code.Trim()))
        {
            TempData["Error"] = "Equipment Class ای با همین کد از قبل وجود دارد.";
            return RedirectToAction(nameof(Index));
        }

        await _context.EquipmentClasses.AddAsync(new EquipmentClass
        {
            Code = code.Trim(),
            Name = name.Trim(),
            NameFa = nameFa,
            Description = description,
            IsSystemDefault = false,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Message"] = "Equipment Class ساخته شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleEquipmentClassActive(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Edit"))
            return Forbid();

        var item = await _context.EquipmentClasses.FirstOrDefaultAsync(x => x.EquipmentClassId == id);
        if (item == null) return NotFound();

        item.IsActive = !item.IsActive;
        await _context.SaveChangesAsync();

        TempData["Message"] = "وضعیت Equipment Class به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStandardReference(string code, string organization, string standardNumber, string title, string? category, string? description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Create"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(organization) ||
            string.IsNullOrWhiteSpace(standardNumber) || string.IsNullOrWhiteSpace(title))
        {
            TempData["Error"] = "کد، سازمان، شماره استاندارد و عنوان الزامی هستند.";
            return RedirectToAction(nameof(Index));
        }

        // طبق بند ۱۹: «duplicate standard references are prohibited»
        if (await _context.StandardReferences.AnyAsync(x => x.Code == code.Trim() ||
            (x.Organization == organization.Trim() && x.StandardNumber == standardNumber.Trim())))
        {
            TempData["Error"] = "این استاندارد (کد یا ترکیب سازمان/شماره) از قبل ثبت شده است.";
            return RedirectToAction(nameof(Index));
        }

        await _context.StandardReferences.AddAsync(new StandardReference
        {
            Code = code.Trim(),
            Organization = organization.Trim(),
            StandardNumber = standardNumber.Trim(),
            Title = title.Trim(),
            Category = category,
            Description = description,
            IsSystemDefault = false,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Message"] = "Standard Reference ساخته شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStandardReferenceActive(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Edit"))
            return Forbid();

        var item = await _context.StandardReferences.FirstOrDefaultAsync(x => x.StandardReferenceId == id);
        if (item == null) return NotFound();

        item.IsActive = !item.IsActive;
        await _context.SaveChangesAsync();

        TempData["Message"] = "وضعیت Standard Reference به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMaintenanceActivity(string code, string name, string? nameFa,
        int maintenanceGroupId, int? maintenanceTypeId, string? description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Create"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "کد و نام Maintenance Activity الزامی هستند.";
            return RedirectToAction(nameof(Index));
        }

        // طبق بند ۱۹: «orphan Maintenance Activities are prohibited» - Group باید معتبر و فعال باشد
        var groupExists = await _context.MaintenanceGroups.AnyAsync(x => x.MaintenanceGroupId == maintenanceGroupId && x.IsActive);
        if (!groupExists)
        {
            TempData["Error"] = "گروه تعمیراتی انتخاب‌شده معتبر یا فعال نیست.";
            return RedirectToAction(nameof(Index));
        }

        if (await _context.MaintenanceActivities.AnyAsync(x => x.Code == code.Trim()))
        {
            TempData["Error"] = "Maintenance Activity ای با همین کد از قبل وجود دارد.";
            return RedirectToAction(nameof(Index));
        }

        await _context.MaintenanceActivities.AddAsync(new MaintenanceActivity
        {
            Code = code.Trim(),
            Name = name.Trim(),
            NameFa = nameFa,
            MaintenanceGroupId = maintenanceGroupId,
            MaintenanceTypeId = maintenanceTypeId,
            Description = description,
            IsSystemDefault = false,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Message"] = "Maintenance Activity ساخته شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleMaintenanceActivityActive(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Edit"))
            return Forbid();

        var item = await _context.MaintenanceActivities.FirstOrDefaultAsync(x => x.MaintenanceActivityId == id);
        if (item == null) return NotFound();

        item.IsActive = !item.IsActive;
        await _context.SaveChangesAsync();

        TempData["Message"] = "وضعیت Maintenance Activity به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    // ==============================================================================
    // طبق «PEMS Phase 2 — Equipment Classification + ISO 14224 Failure Taxonomy + PM/Job Plan
    // Intelligence» بند ۴۲: خط‌مبنای Failure Mode/PM Template فقط از طریق Seeder ساخته می‌شود
    // (نه فرم Create دستی - چون هر دو به روابط چندگانه/داده‌محور نیاز دارند که فرم ساده نمی‌تواند
    // درست مدل کند)؛ فقط Toggle Active همان الگوی محافظت از Master Data (بند ۳۰) را دنبال می‌کند.
    // ==============================================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFailureModeActive(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Edit"))
            return Forbid();

        var item = await _context.FailureModes.FirstOrDefaultAsync(x => x.FailureModeId == id);
        if (item == null) return NotFound();

        item.IsActive = !item.IsActive;
        await _context.SaveChangesAsync();

        TempData["Message"] = "وضعیت Failure Mode به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePMTemplateActive(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenanceMasterData", "Edit"))
            return Forbid();

        var item = await _context.PMTemplates.FirstOrDefaultAsync(x => x.PMTemplateId == id);
        if (item == null) return NotFound();

        item.IsActive = !item.IsActive;
        await _context.SaveChangesAsync();

        TempData["Message"] = "وضعیت PM Template به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }
}
