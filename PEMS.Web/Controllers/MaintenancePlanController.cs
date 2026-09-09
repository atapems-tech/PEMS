using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق درخواست: تکمیل بخش‌های CMMS - برنامه‌ی نگهداری واقعی (چه زمانی، چند روز یک‌بار) که از
/// یک Maintenance Strategy می‌آید و می‌تواند Work Order بسازد.
/// </summary>
public class MaintenancePlanController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly INumberingService _numberingService;
    private readonly IMaintenancePlanService _maintenancePlanService;
    private readonly IPMGenerationService _pmGenerationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public MaintenancePlanController(PEMSDbContext context, INumberingService numberingService,
        IMaintenancePlanService maintenancePlanService, IPMGenerationService pmGenerationService,
        ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _context = context;
        _numberingService = numberingService;
        _maintenancePlanService = maintenancePlanService;
        _pmGenerationService = pmGenerationService;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    // طبق سند «PEMS — Global Data Grid Standard»: قبلاً کل جدول MaintenancePlans با
    // Include(Equipment)+Include(MaintenanceStrategy)+ToListAsync (بدون Skip/Take) بارگذاری
    // می‌شد. این Grid فیلتری نداشت؛ فقط صفحه‌بندی اضافه شد.
    // طبق ممیزی مستقل فاز ۶B Wave B1 (نهایی) بند P0: این کنترلر هیچ بررسی مجوزی نداشت - هر
    // کاربر احرازهویت‌شده‌ای می‌توانست Plan بسازد، غیرفعال کند، از آن WorkOrder بسازد، یا
    // قرائت Meter ثبت کند.
    public async Task<IActionResult> Index(int page = 1, int? pageSize = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenancePlan", "View"))
            return Forbid();

        ViewData["Title"] = "Maintenance Plans";
        ViewData["Section"] = "CMMS Integration";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var result = await _maintenancePlanService.GetListAsync(page, normalizedPageSize);

        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? strategyId, int? equipmentId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenancePlan", "Create"))
            return Forbid();

        ViewData["Title"] = "New Maintenance Plan";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.Strategies = await _context.MaintenanceStrategies.Where(x => x.IsActive)
            .Select(x => new { x.MaintenanceStrategyId, x.EquipmentId, x.StrategyType, x.Frequency, x.RecommendedTriggerType }).ToListAsync();

        ViewBag.JobPlans = await _context.JobPlans.Where(x => x.IsActive)
            .OrderBy(x => x.JobPlanNumber)
            .Select(x => new { x.JobPlanId, x.JobPlanNumber, x.Title }).ToListAsync();

        ViewBag.PreselectedStrategyId = strategyId;
        ViewBag.PreselectedEquipmentId = equipmentId;

        if (strategyId.HasValue)
        {
            ViewBag.PreselectedTriggerType = await _context.MaintenanceStrategies
                .Where(x => x.MaintenanceStrategyId == strategyId.Value)
                .Select(x => x.RecommendedTriggerType)
                .FirstOrDefaultAsync();
        }

        if (equipmentId.HasValue)
        {
            ViewBag.PreselectedEquipmentLabel = await _context.Equipments
                .Where(x => x.EquipmentId == equipmentId.Value)
                .Select(x => x.TagNumber + " - " + x.Name)
                .FirstOrDefaultAsync();
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int equipmentId, int maintenanceStrategyId, string title,
        string triggerType, int? intervalDays, string? meterType, decimal? meterInterval,
        DateTime? nextDueDate, string? description, int? jobPlanId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenancePlan", "Create"))
            return Forbid();

        var equipment = await _context.Equipments.FirstOrDefaultAsync(x => x.EquipmentId == equipmentId);
        var planNumber = await _numberingService.GetNextNumberAsync("MaintenancePlan", equipment?.TagNumber ?? equipmentId.ToString());

        await _context.MaintenancePlans.AddAsync(new MaintenancePlan
        {
            EquipmentId = equipmentId,
            MaintenanceStrategyId = maintenanceStrategyId,
            PlanNumber = planNumber,
            Title = title,
            TriggerType = string.IsNullOrWhiteSpace(triggerType) ? "Calendar" : triggerType,
            IntervalDays = intervalDays,
            MeterType = meterType,
            MeterInterval = meterInterval,
            NextDueDate = nextDueDate,
            Description = description,
            JobPlanId = jobPlanId,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = "Maintenance plan created";
        return RedirectToAction(nameof(Index));
    }

    // ==============================================================================
    // طبق بازنگری CMMS/RCM: اگه تریگر مبتنی بر Meter باشه، اجرا با ثبت قرائت فعلی Meter
    // انجام می‌شه، نه فقط تاریخ - سررسید بعدی از روی همین قرائت محاسبه می‌شه
    // ==============================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordMeterReading(int planId, decimal currentReading)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenancePlan", "Edit"))
            return Forbid();

        var plan = await _context.MaintenancePlans.FirstOrDefaultAsync(x => x.MaintenancePlanId == planId);
        if (plan == null) return NotFound();

        if (plan.TriggerType is "Meter" or "Both" && plan.MeterInterval.HasValue)
        {
            plan.LastExecutionMeterReading = currentReading;
            plan.NextDueMeterReading = currentReading + plan.MeterInterval.Value;
        }

        await _context.SaveChangesAsync();

        TempData["Message"] = "قرائت Meter ثبت شد.";
        return RedirectToAction(nameof(Index));
    }

    // طبق «PEMS Phase 6» بند ۱۵/۵۵-۵۶: منطق واقعی به PMGenerationService منتقل شد (Controller
    // نازک‌تر شود) و حالا Idempotent است - کلیک تکراری/Double-Submit دیگر WorkOrder تکراری نمی‌سازد.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateWorkOrder(int planId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenancePlan", "Edit"))
            return Forbid();

        var workOrderNumber = await _pmGenerationService.GenerateForPlanAsync(planId);

        if (workOrderNumber == null)
        {
            TempData["Error"] = "این Plan یا وجود ندارد یا از قبل یک Work Order فعال (تکمیل‌نشده) دارد - برای جلوگیری از تکرار، Work Order جدیدی ساخته نشد.";
            return RedirectToAction(nameof(Index));
        }

        var workOrderId = await _context.WorkOrders
            .Where(x => x.WorkOrderNumber == workOrderNumber)
            .Select(x => x.WorkOrderId)
            .FirstOrDefaultAsync();

        TempData["Message"] = $"Work order {workOrderNumber} generated from plan";
        return RedirectToAction("Details", "WorkOrder", new { id = workOrderId });
    }

    // طبق «PEMS Phase 6» بند ۱۵/۳۹/۵۶: اجرای دستی (انسان‌محور) روی همه‌ی Plan های سررسیدرسیده -
    // Hangfire زیرساخت موجود است اما بند ۵۶ صراحتاً هیچ زمان‌بند خودکار جدیدی را الزامی نمی‌کند؛
    // این دکمه دقیقاً همان GenerateForPlanAsync Idempotent را برای هر Plan سررسیدرسیده صدا می‌زند.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateDueWorkOrders()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenancePlan", "Edit"))
            return Forbid();

        var result = await _pmGenerationService.GenerateDueWorkOrdersAsync();

        TempData["Message"] = $"{result.WorkOrdersGenerated} Work Order جدید ساخته شد؛ {result.PlansSkippedAlreadyHasOpenWorkOrder} مورد به‌دلیل داشتن Work Order فعال از قبل، رد شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int planId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MaintenancePlan", "Edit"))
            return Forbid();

        var plan = await _context.MaintenancePlans.FirstOrDefaultAsync(x => x.MaintenancePlanId == planId);
        if (plan == null) return NotFound();

        plan.IsActive = false;
        await _context.SaveChangesAsync();

        TempData["Message"] = "Plan deactivated";
        return RedirectToAction(nameof(Index));
    }
}
