using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۶/بند ۳۶:
/// دقیقاً یکی از ConditionReadingId/InspectionRecordId باید پر باشد - اعتبارسنجی سطح سرویس
/// (این Controller، تنها مسیر نوشتن) نه محدودیت پایگاه‌داده. هرگز WorkRequest را تکرار نمی‌کند؛
/// فقط بعد از فاز ۷ (پیوند به WorkRequest واقعی) WorkRequestId پر می‌شود.
/// </summary>
public class FindingController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly IWorkRequestService _workRequestService;

    public FindingController(PEMSDbContext context, ICurrentUserService currentUser,
        IPermissionService permissionService, IWorkRequestService workRequestService)
    {
        _context = context;
        _currentUser = currentUser;
        _permissionService = permissionService;
        _workRequestService = workRequestService;
    }

    public async Task<IActionResult> Index(int? equipmentId, string? status)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "CMFinding", "View"))
            return Forbid();

        ViewData["Title"] = "Findings";
        ViewData["Section"] = "CMMS Integration";

        var query = _context.Findings
            .Include(x => x.Equipment)
            .Include(x => x.FailureMode)
            .Include(x => x.FailureCause)
            .Include(x => x.ConditionReading)
            .Include(x => x.InspectionRecord)
            .Include(x => x.WorkRequest)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (equipmentId.HasValue) query = query.Where(x => x.EquipmentId == equipmentId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

        ViewBag.SelectedEquipmentId = equipmentId;
        ViewBag.SelectedStatus = status;
        ViewBag.FailureModes = await _context.FailureModes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        ViewBag.FailureCauses = await _context.FailureCauses.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();

        var items = await query.OrderByDescending(x => x.ObservedAt).ToListAsync();
        return View(items);
    }

    // طبق بند ۲۹: Condition دقیقاً از EvaluationStatus منجمدشده‌ی همان قرائت کپی می‌شود - هرگز
    // دوباره محاسبه نمی‌شود.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFromReading(int conditionReadingId, string description,
        string? recommendedAction, int? failureModeId, int? failureCauseId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "CMFinding", "Create"))
            return Forbid();

        var reading = await _context.ConditionReadings
            .Include(x => x.ConditionMonitoringPoint)
            .FirstOrDefaultAsync(x => x.ConditionReadingId == conditionReadingId);
        if (reading?.ConditionMonitoringPoint == null) return NotFound();

        if (string.IsNullOrWhiteSpace(description))
        {
            TempData["Error"] = "توضیح یافته الزامی است.";
            return RedirectToAction("ConditionMonitoring", "Reliability", new { equipmentId = reading.ConditionMonitoringPoint.EquipmentId });
        }

        await _context.Findings.AddAsync(new Finding
        {
            EquipmentId = reading.ConditionMonitoringPoint.EquipmentId,
            ConditionReadingId = conditionReadingId,
            ObservedAt = reading.ReadingDate,
            Description = description.Trim(),
            RecommendedAction = recommendedAction,
            Condition = reading.EvaluationStatus,
            FailureModeId = failureModeId,
            FailureCauseId = failureCauseId,
            Status = "Open",
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Message"] = "یافته ثبت شد.";
        return RedirectToAction("ConditionMonitoring", "Reliability", new { equipmentId = reading.ConditionMonitoringPoint.EquipmentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFromInspection(int inspectionRecordId, string description,
        string? recommendedAction, int? failureModeId, int? failureCauseId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "CMFinding", "Create"))
            return Forbid();

        var inspection = await _context.InspectionRecords.FirstOrDefaultAsync(x => x.InspectionRecordId == inspectionRecordId);
        if (inspection == null) return NotFound();

        if (string.IsNullOrWhiteSpace(description))
        {
            TempData["Error"] = "توضیح یافته الزامی است.";
            return RedirectToAction(nameof(Index), new { equipmentId = inspection.EquipmentId });
        }

        await _context.Findings.AddAsync(new Finding
        {
            EquipmentId = inspection.EquipmentId,
            InspectionRecordId = inspectionRecordId,
            ObservedAt = inspection.InspectionDate,
            Description = description.Trim(),
            RecommendedAction = recommendedAction,
            Condition = inspection.Condition ?? inspection.Result ?? "Unknown",
            FailureModeId = failureModeId,
            FailureCauseId = failureCauseId,
            Status = "Open",
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Message"] = "یافته ثبت شد.";
        return RedirectToAction(nameof(Index), new { equipmentId = inspection.EquipmentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "CMFinding", "Create"))
            return Forbid();

        var finding = await _context.Findings.FirstOrDefaultAsync(x => x.FindingId == id);
        if (finding == null) return NotFound();

        finding.Status = "Closed";
        await _context.SaveChangesAsync();

        TempData["Message"] = "یافته بسته شد.";
        return RedirectToAction(nameof(Index), new { equipmentId = finding.EquipmentId });
    }

    // ==============================================================================
    // طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۷:
    // دقیقاً همون الگوی InspectionController.RaiseWorkRequest - هیچ مسیر موازی WorkOrder ساخته
    // نمی‌شود؛ فقط از همان IWorkRequestService.CreateAsync موجود استفاده می‌شود. طبق بند ۳۶:
    // Finding هرگز فیلدهای اجرایی WorkRequest را تکرار نمی‌کند - فقط بعد از ساخت واقعی،
    // WorkRequestId روی Finding پر می‌شود و SourceType/SourceIdentifier (فاز ۱) روی WorkRequest.
    // ==============================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RaiseWorkRequest(int findingId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkRequest", "Create"))
            return Forbid();

        var finding = await _context.Findings.FirstOrDefaultAsync(x => x.FindingId == findingId);
        if (finding == null) return NotFound();

        if (finding.WorkRequestId.HasValue)
        {
            TempData["Error"] = "این یافته از قبل به یک Work Request متصل است.";
            return RedirectToAction(nameof(Index), new { equipmentId = finding.EquipmentId });
        }

        var employeeId = await GetCurrentEmployeeIdAsync();
        if (!employeeId.HasValue)
        {
            TempData["Error"] = "کارمند مرتبط با این کاربر پیدا نشد.";
            return RedirectToAction(nameof(Index), new { equipmentId = finding.EquipmentId });
        }

        // طبق «PEMS — Equipment Passport Audit & CBM» فاز ۱ ممیزی §3.7: این نگاشت قبلاً مستقیم
        // در کد C# Hard-Code شده بود - دقیقاً همون نقضی که سند صریحاً منع می‌کند («Do not
        // automatically equate Critical=Priority X unless such mapping is explicitly configured
        // and approved»). حالا از ConditionPriorityMapping (قابل‌مشاهده/قابل‌ویرایش از
        // MaintenanceMasterData) خوانده می‌شود؛ پیش‌فرض "Medium" فقط وقتی هیچ ردیفی برای این
        // Condition پیکربندی نشده - همون پیش‌فرض قبلی، نه یک مقدار تازه‌ی حدسی.
        var priorityMapping = await _context.ConditionPriorityMappings
            .FirstOrDefaultAsync(x => x.Condition == finding.Condition && x.IsActive);
        var priority = priorityMapping?.Priority ?? "Medium";

        var title = $"Finding - {finding.Condition} - Equipment #{finding.EquipmentId}";
        var reportedCondition = $"یافته ({finding.Condition}) در تاریخ {finding.ObservedAt:yyyy-MM-dd}: {finding.Description}";

        var (success, error, workRequestId) = await _workRequestService.CreateAsync(
            title, reportedCondition, employeeId.Value, finding.EquipmentId, null, null,
            reportedCondition, priority, null);

        if (!success || !workRequestId.HasValue)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index), new { equipmentId = finding.EquipmentId });
        }

        finding.WorkRequestId = workRequestId.Value;
        finding.Status = "Linked";

        var workRequest = await _context.WorkRequests.FirstOrDefaultAsync(x => x.WorkRequestId == workRequestId.Value);
        if (workRequest != null)
        {
            workRequest.SourceType = "Finding";
            workRequest.SourceIdentifier = $"Finding#{finding.FindingId}";
        }

        await _context.SaveChangesAsync();

        TempData["Message"] = "Work Request از یافته ساخته شد.";
        return RedirectToAction("Details", "WorkRequest", new { id = workRequestId });
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }
}
