using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق «PEMS Phase 4 — RCM/FMEA/FMECA Decision Intelligence» بند ۳۷/۳۹: View/Create/Edit روی
/// ماژول "RCMFMEA"؛ Approve فقط برای کاربر دارای CanApprove (طبق بند ۲۰: تایید انسانی الزامی
/// است - AI/Rule Engine هرگز مستقیم تایید نمی‌کند). دکمه‌ی Approve هرگز برای کاربر بدون این
/// مجوز رندر نمی‌شود (بند ۳۹) - View خودش هم دوباره Server-Side چک می‌کند (Defense in Depth).
/// </summary>
public class RcmFmeaController : Controller
{
    private readonly IRcmFmeaReadinessService _readinessService;
    private readonly IFMEAService _fmeaService;
    private readonly IRCMService _rcmService;
    private readonly IRcmOperationalBridgeService _rcmBridge;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public RcmFmeaController(IRcmFmeaReadinessService readinessService, IFMEAService fmeaService,
        IRCMService rcmService, IRcmOperationalBridgeService rcmBridge, PEMSDbContext context,
        ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _readinessService = readinessService;
        _fmeaService = fmeaService;
        _rcmService = rcmService;
        _rcmBridge = rcmBridge;
        _context = context;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    private async Task<bool> CanApproveAsync() => await _permissionService.HasPermissionAsync(_currentUser.Roles, "RCMFMEA", "Approve");
    private async Task<bool> CanEditAsync() => await _permissionService.HasPermissionAsync(_currentUser.Roles, "RCMFMEA", "Edit");

    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "RCMFMEA", "View"))
            return Forbid();

        ViewData["Title"] = "RCM / FMEA / FMECA Dashboard";
        ViewData["Section"] = "CMMS Integration";
        ViewData["HelpKey"] = "RcmFmea.Index";

        var matrix = await _readinessService.GetCoverageMatrixAsync();
        return View(matrix);
    }

    public async Task<IActionResult> ClassDetail(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "RCMFMEA", "View"))
            return Forbid();

        var equipmentClass = await _context.EquipmentClasses.AsNoTracking().FirstOrDefaultAsync(x => x.EquipmentClassId == id);
        if (equipmentClass == null) return NotFound();

        ViewData["Title"] = $"RCM/FMEA — {equipmentClass.Name}";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.EquipmentClass = equipmentClass;
        ViewBag.Readiness = await _readinessService.GetForEquipmentClassAsync(id);
        ViewBag.FmeaAnalyses = await _fmeaService.GetForEquipmentClassAsync(id);
        ViewBag.RcmAnalyses = await _rcmService.GetForEquipmentClassAsync(id);
        ViewBag.CanApprove = await CanApproveAsync();
        ViewBag.CanEdit = await CanEditAsync();

        // برای فرم افزودن FMEA Item: فقط داده‌ی مرجع خودِ همین کلاس (طبق بند ۱۰: بازاستفاده)
        ViewBag.FailureModes = await _context.FailureModes.AsNoTracking()
            .Where(x => x.IsActive && x.EquipmentClassId == id).OrderBy(x => x.Name).ToListAsync();
        ViewBag.FunctionalRequirements = await _context.FunctionalRequirements.AsNoTracking()
            .Where(x => x.IsActive && x.EquipmentClassId == id).OrderBy(x => x.Code).ToListAsync();
        ViewBag.FunctionalFailures = await _context.FunctionalFailures.AsNoTracking()
            .Where(x => x.IsActive && x.FunctionalRequirement!.EquipmentClassId == id)
            .Include(x => x.FunctionalRequirement)
            .OrderBy(x => x.Title).ToListAsync();
        ViewBag.MaintenanceStrategyTypes = await _context.MaintenanceStrategyTypes.AsNoTracking()
            .Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        ViewBag.PMTemplates = await _context.PMTemplates.AsNoTracking()
            .Where(x => x.IsActive && x.EquipmentClassId == id).OrderBy(x => x.Name).ToListAsync();

        // طبق «PEMS Phase 6» بند ۱۳/۵۳: برای اعمال یک تصمیم APPROVED روی یک تجهیز واقعی مشخص،
        // فهرست تجهیزات همین کلاس لازم است (پل هرگز خودکار/بی‌هدف روی همه‌ی تجهیزات اعمال نمی‌شود).
        ViewBag.EquipmentInClass = await _context.Equipments.AsNoTracking()
            .Where(x => x.IsActive && x.EquipmentClassId == id)
            .OrderBy(x => x.TagNumber)
            .Select(x => new { x.EquipmentId, x.TagNumber, x.Name })
            .ToListAsync();

        return View();
    }

}
