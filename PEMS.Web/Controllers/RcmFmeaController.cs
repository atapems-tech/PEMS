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
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public RcmFmeaController(IRcmFmeaReadinessService readinessService, IFMEAService fmeaService,
        IRCMService rcmService, PEMSDbContext context, ICurrentUserService currentUser,
        IPermissionService permissionService)
    {
        _readinessService = readinessService;
        _fmeaService = fmeaService;
        _rcmService = rcmService;
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

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFunctionalRequirement(int equipmentClassId, string code, string title, string? description, string? requiredFunction)
    {
        if (!await CanEditAsync()) return Forbid();

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title))
        {
            TempData["Error"] = "کد و عنوان الزامی هستند.";
            return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
        }

        if (await _context.FunctionalRequirements.AnyAsync(x => x.EquipmentClassId == equipmentClassId && x.Code == code.Trim()))
        {
            TempData["Error"] = "Functional Requirement ای با همین کد از قبل روی این کلاس وجود دارد.";
            return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
        }

        _context.FunctionalRequirements.Add(new Domain.Entities.FunctionalRequirement
        {
            EquipmentClassId = equipmentClassId,
            Code = code.Trim(),
            Title = title.Trim(),
            Description = description,
            RequiredFunction = requiredFunction,
            IsActive = true,
            CreatedDate = DateTime.Now
        });
        await _context.SaveChangesAsync();

        TempData["Message"] = "Functional Requirement ساخته شد.";
        return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFunctionalFailure(int equipmentClassId, int functionalRequirementId, string code, string title, string? failureDefinition)
    {
        if (!await CanEditAsync()) return Forbid();

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title))
        {
            TempData["Error"] = "کد و عنوان الزامی هستند.";
            return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
        }

        _context.FunctionalFailures.Add(new Domain.Entities.FunctionalFailure
        {
            FunctionalRequirementId = functionalRequirementId,
            Code = code.Trim(),
            Title = title.Trim(),
            FailureDefinition = failureDefinition,
            IsActive = true,
            CreatedDate = DateTime.Now
        });
        await _context.SaveChangesAsync();

        TempData["Message"] = "Functional Failure ساخته شد.";
        return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFmeaDraft(int equipmentClassId, string title, string? description)
    {
        if (!await CanEditAsync()) return Forbid();

        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["Error"] = "عنوان تحلیل FMEA الزامی است.";
            return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
        }

        await _fmeaService.CreateDraftAsync(equipmentClassId, title.Trim(), description, _currentUser.UserName ?? "Unknown");
        TempData["Message"] = "پیش‌نویس FMEA ساخته شد.";
        return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFmeaItem(int equipmentClassId, int fmeaAnalysisId, int failureModeId,
        int? functionalFailureId, string? detectionMethod, string? existingControl, string? recommendedAction,
        int? severity, int? occurrence, int? detectability, int? maintenanceStrategyTypeId, int? standardReferenceId)
    {
        if (!await CanEditAsync()) return Forbid();

        // طبق بند ۱۰: Cause/Mechanism/Effect/Consequence مستقیماً از خودِ FailureMode (فاز ۲)
        // خوانده می‌شوند - این‌جا فقط اولین Cause/Mechanism مرتبط را پیش‌فرض می‌گیرد؛ اگر یک
        // Failure Mode چند Cause دارد، مهندس می‌تواند بعداً ردیف‌های بیشتری برای همان Mode با
        // Cause های دیگر اضافه کند (بدون تکرار تاکسونومی).
        var failureMode = await _context.FailureModes
            .AsNoTracking()
            .Include(x => x.Causes)
            .FirstOrDefaultAsync(x => x.FailureModeId == failureModeId);

        if (failureMode == null)
        {
            TempData["Error"] = "Failure Mode یافت نشد.";
            return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
        }

        var firstCauseId = failureMode.Causes.Select(x => (int?)x.FailureCauseId).FirstOrDefault();
        int? firstMechanismId = null;
        if (firstCauseId.HasValue)
        {
            firstMechanismId = await _context.FailureCauseMechanisms
                .Where(x => x.FailureCauseId == firstCauseId.Value)
                .Select(x => (int?)x.FailureMechanismId)
                .FirstOrDefaultAsync();
        }

        try
        {
            await _fmeaService.AddItemAsync(fmeaAnalysisId, failureModeId, functionalFailureId,
                firstCauseId, firstMechanismId, failureMode.FailureEffectId, failureMode.FailureConsequenceId,
                detectionMethod, existingControl, recommendedAction, severity, occurrence, detectability,
                maintenanceStrategyTypeId, standardReferenceId);
            TempData["Message"] = "ردیف FMEA اضافه شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitFmea(int equipmentClassId, int fmeaAnalysisId)
    {
        if (!await CanEditAsync()) return Forbid();

        var ok = await _fmeaService.SubmitForReviewAsync(fmeaAnalysisId, _currentUser.UserName ?? "Unknown");
        TempData[ok ? "Message" : "Error"] = ok ? "برای بازبینی ارسال شد." : "این تحلیل در وضعیت DRAFT نیست.";
        return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveFmea(int equipmentClassId, int fmeaAnalysisId)
    {
        if (!await CanApproveAsync()) return Forbid();

        var ok = await _fmeaService.ApproveAsync(fmeaAnalysisId, _currentUser.UserName ?? "Unknown");
        TempData[ok ? "Message" : "Error"] = ok ? "FMEA تایید شد." : "این تحلیل آماده‌ی تایید نیست.";
        return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectFmea(int equipmentClassId, int fmeaAnalysisId)
    {
        if (!await CanApproveAsync()) return Forbid();

        var ok = await _fmeaService.RejectAsync(fmeaAnalysisId, _currentUser.UserName ?? "Unknown");
        TempData[ok ? "Message" : "Error"] = ok ? "به DRAFT بازگشت داده شد." : "این تحلیل آماده‌ی رد نیست.";
        return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRcmDraft(int equipmentClassId, int fmeaAnalysisId)
    {
        if (!await CanEditAsync()) return Forbid();

        await _rcmService.CreateDraftAsync(equipmentClassId, fmeaAnalysisId, _currentUser.UserName ?? "Unknown");
        TempData["Message"] = "پیش‌نویس RCM ساخته شد.";
        return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRcmDecision(int equipmentClassId, int rcmAnalysisId, int fmeaItemId,
        string decisionType, int? maintenanceStrategyTypeId, int? recommendedPMTemplateId, string? rationale, string consequenceCategory)
    {
        if (!await CanEditAsync()) return Forbid();

        try
        {
            await _rcmService.AddDecisionAsync(rcmAnalysisId, fmeaItemId, decisionType, maintenanceStrategyTypeId,
                recommendedPMTemplateId, rationale, consequenceCategory);
            TempData["Message"] = "تصمیم RCM اضافه شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRcmDecision(int equipmentClassId, int rcmDecisionId)
    {
        if (!await CanApproveAsync()) return Forbid();

        var ok = await _rcmService.ApproveDecisionAsync(rcmDecisionId, _currentUser.UserName ?? "Unknown");
        TempData[ok ? "Message" : "Error"] = ok ? "تصمیم RCM تایید شد." : "این تصمیم آماده‌ی تایید نیست.";
        return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitRcm(int equipmentClassId, int rcmAnalysisId)
    {
        if (!await CanEditAsync()) return Forbid();

        var ok = await _rcmService.SubmitForReviewAsync(rcmAnalysisId, _currentUser.UserName ?? "Unknown");
        TempData[ok ? "Message" : "Error"] = ok ? "برای بازبینی ارسال شد." : "این تحلیل در وضعیت DRAFT نیست.";
        return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRcm(int equipmentClassId, int rcmAnalysisId)
    {
        if (!await CanApproveAsync()) return Forbid();

        var ok = await _rcmService.ApproveAsync(rcmAnalysisId, _currentUser.UserName ?? "Unknown");
        TempData[ok ? "Message" : "Error"] = ok ? "RCM تایید شد." : "این تحلیل آماده‌ی تایید نیست.";
        return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRcm(int equipmentClassId, int rcmAnalysisId)
    {
        if (!await CanApproveAsync()) return Forbid();

        var ok = await _rcmService.RejectAsync(rcmAnalysisId, _currentUser.UserName ?? "Unknown");
        TempData[ok ? "Message" : "Error"] = ok ? "به DRAFT بازگشت داده شد." : "این تحلیل آماده‌ی رد نیست.";
        return RedirectToAction(nameof(ClassDetail), new { id = equipmentClassId });
    }
}
