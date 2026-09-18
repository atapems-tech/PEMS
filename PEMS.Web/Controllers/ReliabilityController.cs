using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    // طبق «PEMS Phase 9 — Enterprise Reliability, RAM & Predictive Maintenance Intelligence»
    private readonly IRamWeibullService _ramWeibullService;
    private readonly IReliabilitySimulationService _simulationService;
    private readonly IConditionMonitoringService _conditionMonitoringService;
    private readonly IPredictiveMaintenanceService _predictiveMaintenanceService;
    private readonly IAIReliabilityInsightService _aiInsightService;
    private readonly IClassIntelligenceService _classIntelligenceService;
    private readonly IEquipmentIntelligenceService _equipmentIntelligenceService;
    private readonly PEMS.Persistence.Context.PEMSDbContext _context;

    public ReliabilityController(IReliabilityService reliabilityService,
        IReliabilityAnalyticsService analyticsService, IReliabilityDataQualityService dataQualityService,
        IReliabilityIntelligenceService intelligenceService, IRcmFeedbackService rcmFeedbackService,
        ICurrentUserService currentUser, IPermissionService permissionService,
        IRamWeibullService ramWeibullService, IReliabilitySimulationService simulationService,
        IConditionMonitoringService conditionMonitoringService, IPredictiveMaintenanceService predictiveMaintenanceService,
        IAIReliabilityInsightService aiInsightService, IClassIntelligenceService classIntelligenceService,
        IEquipmentIntelligenceService equipmentIntelligenceService, PEMS.Persistence.Context.PEMSDbContext context)
    {
        _reliabilityService = reliabilityService;
        _analyticsService = analyticsService;
        _dataQualityService = dataQualityService;
        _intelligenceService = intelligenceService;
        _rcmFeedbackService = rcmFeedbackService;
        _currentUser = currentUser;
        _permissionService = permissionService;
        _ramWeibullService = ramWeibullService;
        _simulationService = simulationService;
        _conditionMonitoringService = conditionMonitoringService;
        _predictiveMaintenanceService = predictiveMaintenanceService;
        _aiInsightService = aiInsightService;
        _classIntelligenceService = classIntelligenceService;
        _equipmentIntelligenceService = equipmentIntelligenceService;
        _context = context;
    }

    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "View"))
            return Forbid();

        ViewData["Title"] = "Reliability Analysis";
        ViewData["Section"] = "CMMS Integration";
        // طبق «PEMS Help Phase 4 — Increment 3B»: این صفحه و Dashboard() هر دو همان مفاهیم
        // اصلی (MTBF/MTTR/Availability/Bad Actor) را نشان می‌دهند - یک مقاله‌ی جامع مشترک،
        // نه دو مقاله‌ی نیمه‌تکراری (دقیقاً همان الگوی Imports.StagedBatchOverview در Increment 2)
        ViewData["HelpKey"] = "Reliability.Overview";

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
        ViewBag.CriticalityFailureMaintenanceMatrix = await _intelligenceService.GetCriticalityFailureMaintenanceMatrixAsync();

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

    // ==============================================================================
    // طبق «PEMS Phase 9 — Enterprise Reliability, RAM & Predictive Maintenance Intelligence»:
    // همان ماژول مجوز "Reliability" موجود بازاستفاده شد (بند ۲۳: هیچ ماژول مجوز جدیدی برای
    // یک لایه‌ی توسعه‌ی هوشمندی روی همان داده لازم نیست) - View برای مشاهده، Edit برای تولید
    // بینش AI (نوشتنی ولی بدون تغییر پیکربندی عملیاتی، دقیقاً مثل GenerateRcmEvidence)، Approve
    // فقط برای بازبینی/تایید بینش (دقیقاً همان الگوی ReviewRcmRecommendation).
    // ==============================================================================

    public async Task<IActionResult> Dashboard()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "View"))
            return Forbid();

        ViewData["Title"] = "Enterprise Reliability Dashboard";
        ViewData["Section"] = "CMMS Integration";
        ViewData["HelpKey"] = "Reliability.Overview";

        ViewBag.ClassReliability = await _analyticsService.GetClassReliabilityAsync();
        ViewBag.BadActors = await _analyticsService.GetBadActorsAsync(null, null, top: 10);
        ViewBag.FailureModePareto = await _analyticsService.GetFailureModeParetoAsync(null, null, top: 10);
        ViewBag.PmCompliance = await _intelligenceService.GetPmComplianceAsync(null, null);
        ViewBag.MaintenanceTypeBreakdown = await _intelligenceService.GetMaintenanceTypeBreakdownAsync(null, null);
        ViewBag.RepeatFailures = await _intelligenceService.GetRepeatFailuresAsync(null, null);
        ViewBag.CriticalityFailureMatrix = await _intelligenceService.GetCriticalityFailureMatrixAsync();
        ViewBag.CriticalityFailureMaintenanceMatrix = await _intelligenceService.GetCriticalityFailureMaintenanceMatrixAsync();
        ViewBag.DataQuality = await _dataQualityService.GetDataQualityAsync();
        ViewBag.ConditionMonitoring = await _conditionMonitoringService.GetFoundationStatusAsync();
        ViewBag.Predictive = await _predictiveMaintenanceService.GetReadinessAsync();

        var fleet = await _reliabilityService.GetFleetReliabilityAsync();
        return View(fleet);
    }

    public async Task<IActionResult> EquipmentProfile(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "View"))
            return Forbid();

        var profile = await _equipmentIntelligenceService.GetProfileAsync(id);
        if (profile == null) return NotFound();

        ViewData["Title"] = $"Equipment Intelligence — {profile.TagNumber}";
        ViewData["Section"] = "CMMS Integration";

        return View(profile);
    }

    public async Task<IActionResult> ClassIntelligence(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "View"))
            return Forbid();

        var profile = await _classIntelligenceService.GetClassIntelligenceAsync(id);
        if (profile == null) return NotFound();

        ViewData["Title"] = $"Class Intelligence — {profile.Name}";
        ViewData["Section"] = "CMMS Integration";

        return View(profile);
    }

    public async Task<IActionResult> Ram(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "View"))
            return Forbid();

        var ram = await _ramWeibullService.GetRamAnalysisAsync(id);
        var weibull = await _ramWeibullService.GetWeibullAnalysisAsync(id);

        ViewData["Title"] = $"RAM / Weibull — {ram.TagNumber ?? weibull.TagNumber ?? id.ToString()}";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.EquipmentId = id;
        ViewBag.Weibull = weibull;
        return View(ram);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Simulate(int id, int iterations = 1000, double horizonHours = 8760)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "View"))
            return Forbid();

        var result = await _simulationService.SimulateAvailabilityAsync(id, iterations, horizonHours);
        ViewData["Title"] = "Monte Carlo Simulation Result";
        ViewData["Section"] = "CMMS Integration";
        ViewBag.EquipmentId = id;
        return View(result);
    }

    public async Task<IActionResult> ConditionMonitoring(int? equipmentId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "View"))
            return Forbid();

        ViewData["Title"] = "Condition Monitoring";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.Foundation = await _conditionMonitoringService.GetFoundationStatusAsync();
        ViewBag.EquipmentId = equipmentId;
        ViewBag.SupportedVariableTypes = PEMS.Persistence.Services.ConditionMonitoringService.ValidVariableTypes;

        var points = equipmentId.HasValue
            ? await _conditionMonitoringService.GetPointsForEquipmentAsync(equipmentId.Value)
            : new List<Application.DTOs.ConditionMonitoringPointDto>();

        return View(points);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateConditionMonitoringPoint(int equipmentId, string variableType, string tag,
        string? unit, decimal? alarmLow, decimal? alarmHigh, string? description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "Edit"))
            return Forbid();

        var (success, error, _) = await _conditionMonitoringService.CreatePointAsync(
            equipmentId, variableType, tag, unit, alarmLow, alarmHigh, description, _currentUser.UserName);
        TempData[success ? "Message" : "Error"] = success ? "نقطه‌ی پایش ساخته شد." : error;

        return RedirectToAction(nameof(ConditionMonitoring), new { equipmentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordConditionReading(int pointId, int equipmentId, DateTime readingDate,
        decimal value, string source, string? notes)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "Edit"))
            return Forbid();

        var (success, error) = await _conditionMonitoringService.RecordReadingAsync(
            pointId, readingDate, value, source, notes, _currentUser.UserName);
        TempData[success ? "Message" : "Error"] = success ? "قرائت ثبت شد." : error;

        return RedirectToAction(nameof(ConditionMonitoring), new { equipmentId });
    }

    public async Task<IActionResult> PredictiveMaintenance()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "View"))
            return Forbid();

        ViewData["Title"] = "Predictive Maintenance";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.Readiness = await _predictiveMaintenanceService.GetReadinessAsync();
        var models = await _predictiveMaintenanceService.GetModelsAsync();
        return View(models);
    }

    public async Task<IActionResult> AiInsights(string? status)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "View"))
            return Forbid();

        ViewData["Title"] = "AI Reliability Insights";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.StatusFilter = status;
        ViewBag.CanGenerate = await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "Edit");
        ViewBag.CanReview = await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "Approve");

        var insights = await _aiInsightService.GetInsightsAsync(status);
        return View(insights);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateAiInsights(int? equipmentClassId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "Edit"))
            return Forbid();

        var result = await _aiInsightService.GenerateInsightsAsync(equipmentClassId, _currentUser.UserName);
        TempData[result.Success ? "Message" : "Error"] = result.Success
            ? $"{result.InsightsGenerated} بینش جدید (Draft) تولید شد - نیازمند بازبینی مهندس است."
            : result.Error;

        return RedirectToAction(nameof(AiInsights));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewAiInsight(int insightId, bool approve, string? notes)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Reliability", "Approve"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var (success, error) = await _aiInsightService.ReviewInsightAsync(insightId, approve, employeeId, notes);
        TempData[success ? "Message" : "Error"] = success
            ? (approve ? "بینش تایید شد." : "بینش رد شد.")
            : error;

        return RedirectToAction(nameof(AiInsights));
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }
}
