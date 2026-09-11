using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق CMMS Production Baseline v1 - CMMS-08 (Cost &amp; KPI): داشبورد یکپارچه‌ی Maintenance
/// KPI + Cost KPI (طبق بند ۲۴: نباید همه‌ی KPI ها روی یک داشبورد بار شوند - این‌جا فقط دو
/// دسته‌ی هم‌نام با عنوان فاز). معیارهای Reliability از IReliabilityService (CMMS-06) خونده
/// می‌شوند - دوباره‌سازی نمی‌شوند.
/// </summary>
public class KpiController : Controller
{
    private readonly IKpiService _kpiService;
    private readonly IReliabilityService _reliabilityService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public KpiController(IKpiService kpiService, IReliabilityService reliabilityService,
        ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _kpiService = kpiService;
        _reliabilityService = reliabilityService;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    // همان ماژول مجوز "Reliability" موجود بازاستفاده شد (نه یک ماژول جدید) - این داشبورد هم
    // فقط-مشاهده است و از همان الگوی ReliabilityController پیروی می‌کند.
    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "View"))
            return Forbid();

        ViewData["Title"] = "KPI Dashboard";
        ViewData["Section"] = "CMMS Integration";

        var cost = await _kpiService.GetCostKpisAsync(fromDate, toDate);
        var maintenance = await _kpiService.GetMaintenanceKpisAsync(fromDate, toDate);
        var fleet = await _reliabilityService.GetFleetReliabilityAsync();

        var dashboard = new KpiDashboardDto
        {
            Cost = cost,
            Maintenance = maintenance,
            Reliability = new ReliabilitySummaryDto
            {
                EquipmentWithFailuresCount = fleet.Count,
                TotalFailureCount = fleet.Sum(x => x.FailureCount),
                AverageAvailabilityPercent = fleet.Any(x => x.AvailabilityPercent.HasValue)
                    ? fleet.Where(x => x.AvailabilityPercent.HasValue).Average(x => x.AvailabilityPercent!.Value)
                    : null
            },
            FromDate = fromDate,
            ToDate = toDate
        };

        return View(dashboard);
    }
}
