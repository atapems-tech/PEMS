using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق «PEMS — Work Request/Work Order Scheduling &amp; Maintenance Calendar»: نمای تصویری روی
/// WorkOrder واقعی (PlannedStart/PlannedFinish) - هیچ Entity/گردش‌کار/مجوز تازه‌ای این‌جا نیست؛
/// روی همان ماژول مجوز "WorkOrder" موجود سوار شده (همان الگوی CmmsOperationsController).
/// تغییر زمان‌بندی (فرم یا Drag&amp;Drop) هر دو از طریق همان
/// IWorkOrderLifecycleService.ScheduleWorkOrderAsync انجام می‌شود - همان مجوز/اسکوپ دیسیپلین/Audit
/// خودکار سراسری؛ این Controller خودش هیچ اعتبارسنجی دامنه‌ای انجام نمی‌دهد.
/// </summary>
public class MaintenanceCalendarController : Controller
{
    private readonly ICmmsCalendarService _calendarService;
    private readonly IWorkOrderLifecycleService _lifecycleService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly PEMSDbContext _context;

    public MaintenanceCalendarController(ICmmsCalendarService calendarService, IWorkOrderLifecycleService lifecycleService,
        ICurrentUserService currentUser, IPermissionService permissionService, PEMSDbContext context)
    {
        _calendarService = calendarService;
        _lifecycleService = lifecycleService;
        _currentUser = currentUser;
        _permissionService = permissionService;
        _context = context;
    }

    public async Task<IActionResult> Index(string view = "month", DateTime? date = null, int? teamId = null, int? employeeId = null,
        string? status = null, string? workOrderType = null, string? priority = null, int? equipmentId = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "View")) return Forbid();

        ViewData["Title"] = "Maintenance Calendar";
        ViewData["Section"] = "CMMS Integration";
        ViewData["HelpKey"] = "MaintenanceCalendar.Index";

        var anchor = (date ?? DateTime.Today).Date;
        view = view is "day" or "week" or "month" ? view : "month";

        var (rangeStart, rangeEnd) = view switch
        {
            "day" => (anchor, anchor.AddDays(1)),
            "week" => WeekRange(anchor),
            _ => MonthRange(anchor)
        };

        var result = await _calendarService.GetCalendarAsync(rangeStart, rangeEnd, teamId, employeeId, status, workOrderType, priority, equipmentId,
            includeUnscheduled: view != "day");

        ViewBag.View = view;
        ViewBag.Anchor = anchor;
        ViewBag.RangeStart = rangeStart;
        ViewBag.RangeEnd = rangeEnd;
        ViewBag.Teams = await _calendarService.GetActiveTeamsAsync();
        ViewBag.Employees = await _context.Employees.Where(x => x.IsActive)
            .Select(x => new { x.EmployeeId, FullName = x.FirstName + " " + x.LastName }).OrderBy(x => x.FullName).ToListAsync();
        ViewBag.TeamId = teamId;
        ViewBag.EmployeeId = employeeId;
        ViewBag.Status = status;
        ViewBag.WorkOrderType = workOrderType;
        ViewBag.Priority = priority;
        ViewBag.EquipmentId = equipmentId;

        return View(result);
    }

    /// <summary>طبق بند ۱۳ درخواست: نمای «کار من» - کار برنامه‌ریزی‌شده‌ی امروزِ کارمندِ کاربر جاری.</summary>
    public async Task<IActionResult> MyWork(DateTime? date = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "View")) return Forbid();

        ViewData["Title"] = "My Work";
        ViewData["Section"] = "CMMS Integration";

        var employeeId = await GetCurrentEmployeeIdAsync();
        var day = (date ?? DateTime.Today).Date;
        ViewBag.Day = day;

        if (!employeeId.HasValue)
            return View(new List<WorkOrderCalendarItemDto>());

        var items = await _calendarService.GetMyWorkAsync(employeeId.Value, day);
        return View(items);
    }

    /// <summary>
    /// طبق بند ۱۶ درخواست: Drag&amp;Drop از همین مسیر عبور می‌کند (نه یک مسیر جدا با اعتبارسنجی
    /// جدا) - JSON فقط برای فراخوانی Ajax؛ خودِ WorkOrder/Details.SetSchedule جایگزین بدون
    /// Drag&amp;Drop همین Action دامنه‌ای است (نه یک منطق موازی).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(int workOrderId, DateTime plannedStart, DateTime plannedFinish)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "View")) return Forbid();
        var actor = new WorkOrderActor(await GetCurrentEmployeeIdAsync(), _currentUser.Roles);
        var (success, error) = await _lifecycleService.ScheduleWorkOrderAsync(workOrderId, plannedStart, plannedFinish, actor);
        return Json(new { success, error });
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }

    private static (DateTime Start, DateTime End) WeekRange(DateTime anchor)
    {
        // طبق قرارداد تقویم میلادی پروژه (بدون پشتیبانی از تقویم فارسی در جای دیگری از PEMS) - هفته از شنبه شروع می‌شود؟
        // خیر: چون هیچ قرارداد صریح شروع هفته‌ای در ریپازیتوری یافت نشد، از DayOfWeek.Monday (استاندارد ISO) استفاده می‌شود.
        var diff = (7 + (anchor.DayOfWeek - DayOfWeek.Monday) % 7) % 7;
        var start = anchor.AddDays(-diff);
        return (start, start.AddDays(7));
    }

    private static (DateTime Start, DateTime End) MonthRange(DateTime anchor)
    {
        var start = new DateTime(anchor.Year, anchor.Month, 1);
        return (start, start.AddMonths(1));
    }
}
