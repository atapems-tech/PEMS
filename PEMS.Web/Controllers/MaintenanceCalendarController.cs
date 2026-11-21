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
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly PEMSDbContext _context;

    public MaintenanceCalendarController(ICmmsCalendarService calendarService,
        ICurrentUserService currentUser, IPermissionService permissionService, PEMSDbContext context)
    {
        _calendarService = calendarService;
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
