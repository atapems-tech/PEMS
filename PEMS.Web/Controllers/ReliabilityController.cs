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
    private readonly IReliabilityIntelligenceService _intelligenceService;
    private readonly IRcmFeedbackService _rcmFeedbackService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public ReliabilityController(IReliabilityService reliabilityService,
        IReliabilityAnalyticsService analyticsService, IReliabilityDataQualityService dataQualityService,
        IReliabilityIntelligenceService intelligenceService, IRcmFeedbackService rcmFeedbackService,
        ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _reliabilityService = reliabilityService;
        _analyticsService = analyticsService;
        _dataQualityService = dataQualityService;
        _intelligenceService = intelligenceService;
        _rcmFeedbackService = rcmFeedbackService;
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

    // ==============================================================================
    // طبق «PEMS Phase 7 — Maintenance Intelligence & Reliability Operationalization» بند ۲۳/۲۸:
    // همان ماژول مجوز "Reliability" موجود بازاستفاده شد (نه یک ماژول جدید) - View برای مشاهده،
    // Edit برای تولید شواهد (یک عملیات نوشتنی ولی بدون تغییر پیکربندی عملیاتی)، Approve فقط
    // برای بازبینی/تایید توصیه (دقیقاً همان الگوی RCMFMEA.Approve).
    // ==============================================================================
    public async Task<IActionResult> Intelligence(DateTime? from, DateTime? to, int? equipmentClassId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "View"))
            return Forbid();

        ViewData["Title"] = "Maintenance Intelligence";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.EquipmentClassId = equipmentClassId;
        ViewBag.PmCompliance = await _intelligenceService.GetPmComplianceAsync(from, to, equipmentClassId);
        ViewBag.MaintenanceTypeBreakdown = await _intelligenceService.GetMaintenanceTypeBreakdownAsync(from, to);
        ViewBag.RepeatFailures = await _intelligenceService.GetRepeatFailuresAsync(from, to);
        ViewBag.Effectiveness = await _intelligenceService.GetMaintenanceEffectivenessAsync();
        ViewBag.CriticalityMatrix = await _intelligenceService.GetCriticalityFailureMatrixAsync();

        return View();
    }

    public async Task<IActionResult> RcmFeedback(string? status)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "View"))
            return Forbid();

        ViewData["Title"] = "RCM Feedback";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.StatusFilter = status;
        ViewBag.CanGenerate = await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "Edit");
        ViewBag.CanReview = await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "Approve");

        var recommendations = await _rcmFeedbackService.GetRecommendationsAsync(status);
        return View(recommendations);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateRcmEvidence()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "Edit"))
            return Forbid();

        var result = await _rcmFeedbackService.GenerateEvidenceAsync();
        TempData["Message"] = $"{result.RecommendationsGenerated} توصیه‌ی جدید تولید شد (از {result.StrategiesEvaluated} استراتژی بررسی‌شده)؛ {result.StrategiesSkippedAlreadyPending} مورد به‌دلیل داشتن توصیه‌ی PENDING از قبل، رد شد.";
        return RedirectToAction(nameof(RcmFeedback));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewRcmRecommendation(int recommendationId, bool accept, string? notes)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "Approve"))
            return Forbid();

        var (success, error) = await _rcmFeedbackService.ReviewRecommendationAsync(recommendationId, accept, _currentUser.UserName ?? "Unknown", notes);
        TempData[success ? "Message" : "Error"] = success
            ? (accept ? "توصیه پذیرفته شد." : "توصیه رد شد.")
            : error;

        return RedirectToAction(nameof(RcmFeedback));
    }
}
