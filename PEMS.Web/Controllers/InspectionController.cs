using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق CMMS Production Baseline v1 - CMMS-07 (Inspection): بازرسی دوره‌ای کمی تجهیز
/// (Condition Monitoring) - نقطه‌ی ورود مستقل از پذیرش ITP (که QCController مدیریت می‌کند).
/// </summary>
public class InspectionController : Controller
{
    private readonly IInspectionService _service;
    private readonly IWorkRequestService _workRequestService;
    private readonly PEMSDbContext _context;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUser;

    public InspectionController(IInspectionService service, IWorkRequestService workRequestService,
        PEMSDbContext context, IPermissionService permissionService, ICurrentUserService currentUser)
    {
        _service = service;
        _workRequestService = workRequestService;
        _context = context;
        _permissionService = permissionService;
        _currentUser = currentUser;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): قبلاً کل جدول فعال با
    // ToListAsync (بدون Skip/Take) بارگذاری می‌شد.
    public async Task<IActionResult> Index(int page = 1, int? pageSize = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Inspection", "View"))
            return Forbid();

        ViewData["Title"] = "Inspections";
        ViewData["Section"] = "CMMS Integration";

        var items = await _service.GetPagedFleetAsync(page, DataGridPageSize.Normalize(pageSize));
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int equipmentId, string inspectionType, DateTime inspectionDate, string? inspector,
        decimal? measurementValue, string? measurementUnit, decimal? lowerLimit, decimal? upperLimit,
        string? recommendation, DateTime? nextInspectionDate, string? description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Inspection", "Create"))
            return Forbid();

        var (success, error, _) = await _service.CreateAsync(
            equipmentId, inspectionType, inspectionDate, inspector,
            measurementValue, measurementUnit, lowerLimit, upperLimit,
            recommendation, nextInspectionDate, description);

        TempData[success ? "Message" : "Error"] = success ? "بازرسی ثبت شد." : error;
        return RedirectToAction("Details", "Equipment", new { id = equipmentId });
    }

    // طبق بند ۲۵/۲۶ سند Architecture Gate: Alert → WorkRequest - اقدام یک‌کلیکی انسانی، نه
    // ساخت خودکار/بی‌صدا. از همون IWorkRequestService واقعی که در CMMS-03 ساخته شد استفاده می‌کند.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RaiseWorkRequest(int inspectionRecordId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkRequest", "Create"))
            return Forbid();

        var record = await _context.InspectionRecords.FirstOrDefaultAsync(x => x.InspectionRecordId == inspectionRecordId);
        if (record == null) return NotFound();

        var employeeId = await GetCurrentEmployeeIdAsync();
        if (!employeeId.HasValue)
        {
            TempData["Error"] = "کارمند مرتبط با این کاربر پیدا نشد.";
            return RedirectToAction("Details", "Equipment", new { id = record.EquipmentId });
        }

        var condition = record.LowerLimit.HasValue && record.MeasurementValue < record.LowerLimit
            ? $"کمتر از حد مجاز ({record.MeasurementValue} {record.MeasurementUnit} < {record.LowerLimit} {record.MeasurementUnit})"
            : $"بیشتر از حد مجاز ({record.MeasurementValue} {record.MeasurementUnit} > {record.UpperLimit} {record.MeasurementUnit})";

        var title = $"Inspection Alert - {record.InspectionType} - Equipment #{record.EquipmentId}";
        var reportedCondition = $"بازرسی {record.InspectionType} در تاریخ {record.InspectionDate:yyyy-MM-dd}: {condition}.";

        var (success, error, workRequestId) = await _workRequestService.CreateAsync(
            title, reportedCondition, employeeId.Value, record.EquipmentId, null, null,
            reportedCondition, "High", null);

        TempData[success ? "Message" : "Error"] = success ? "Work Request ساخته شد." : error;

        if (success && workRequestId.HasValue)
            return RedirectToAction("Details", "WorkRequest", new { id = workRequestId });

        return RedirectToAction("Details", "Equipment", new { id = record.EquipmentId });
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }
}
