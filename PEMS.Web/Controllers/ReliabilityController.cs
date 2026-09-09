using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق CMMS Production Baseline v1 - CMMS-06 (Reliability): نمای ناوگانی MTBF/MTTR/Availability،
/// محاسبه‌شده از روی FailureRecord های واقعی (IReliabilityService).
///
/// طبق «PEMS Phase 5 — Final Reliability Intelligence» بند ۴۶: این کنترلر پیش از این فاز هیچ
/// بررسی مجوزی نداشت (طبق اصل «Hidden Navigation != Security» همین پروژه، این یک خلأ امنیتی
/// واقعی بود، نه فقط یک نقص UI) - همزمان با معرفی ماژول Reliability سخت‌سازی شد. اکشن‌های
/// جدید (ClassDetail/Pareto/BadActors/Trends/DataQuality) از IReliabilityAnalyticsService و
/// IReliabilityDataQualityService (بند ۹/۱۴) می‌آیند - هیچ منطق محاسباتی این‌جا نیست.
/// </summary>
public class ReliabilityController : Controller
{
    private readonly IReliabilityService _reliabilityService;
    private readonly IReliabilityAnalyticsService _analyticsService;
    private readonly IReliabilityDataQualityService _dataQualityService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public ReliabilityController(IReliabilityService reliabilityService,
        IReliabilityAnalyticsService analyticsService, IReliabilityDataQualityService dataQualityService,
        ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _reliabilityService = reliabilityService;
        _analyticsService = analyticsService;
        _dataQualityService = dataQualityService;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "View"))
            return Forbid();

        ViewData["Title"] = "Reliability Analysis";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.ClassReliability = await _analyticsService.GetClassReliabilityAsync();
        ViewBag.FailureModePareto = await _analyticsService.GetFailureModeParetoAsync(from, to);
        ViewBag.FailureCausePareto = await _analyticsService.GetFailureCauseParetoAsync(from, to);
        ViewBag.BadActors = await _analyticsService.GetBadActorsAsync(from, to);
        ViewBag.MonthlyTrend = await _analyticsService.GetMonthlyTrendAsync(from, to);

        var fleet = await _reliabilityService.GetFleetReliabilityAsync();
        return View(fleet);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "View"))
            return Forbid();

        ViewData["Title"] = "Equipment Reliability";
        ViewData["Section"] = "CMMS Integration";

        var reliability = await _reliabilityService.GetEquipmentReliabilityAsync(id);
        if (reliability == null) return NotFound();

        return View(reliability);
    }

    public async Task<IActionResult> ClassDetail(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "View"))
            return Forbid();

        var dto = await _analyticsService.GetClassReliabilityAsync(id);
        if (dto == null) return NotFound();

        ViewData["Title"] = $"Reliability — {dto.Name}";
        ViewData["Section"] = "CMMS Integration";

        return View(dto);
    }

    public async Task<IActionResult> DataQuality()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "View"))
            return Forbid();

        ViewData["Title"] = "Reliability Data Quality";
        ViewData["Section"] = "CMMS Integration";

        var dto = await _dataQualityService.GetDataQualityAsync();
        return View(dto);
    }
}
