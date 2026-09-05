using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق تطبیق با RCM (SAE JA1011/JA1012): استراتژی نگهداری (Preventive/Predictive/
/// Corrective/CBM) پایه‌ی ساخت Maintenance Plan است. طبق درخواست: تعریف استراتژی برای چند
/// تجهیز هم‌زمان (نه فقط یکی‌یکی) و پیشنهاد نوع استراتژی بر اساس کلاس بحرانی‌بودن تجهیز.
/// </summary>
public class MaintenanceStrategyController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly IMaintenanceStrategyService _maintenanceStrategyService;

    public MaintenanceStrategyController(PEMSDbContext context, IMaintenanceStrategyService maintenanceStrategyService)
    {
        _context = context;
        _maintenanceStrategyService = maintenanceStrategyService;
    }

    // طبق سند «PEMS — Global Data Grid Standard»: قبلاً کل جدول (یا زیرمجموعه‌ی فیلترشده با
    // equipmentId) با Include(Equipment)+ToListAsync (بدون Skip/Take) بارگذاری می‌شد.
    public async Task<IActionResult> Index(int? equipmentId, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Maintenance Strategies";
        ViewData["Section"] = "CMMS Integration";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var result = await _maintenanceStrategyService.GetListAsync(page, normalizedPageSize, equipmentId);

        ViewBag.SelectedEquipmentId = equipmentId;

        if (equipmentId.HasValue)
        {
            ViewBag.SelectedEquipmentLabel = await _context.Equipments
                .Where(x => x.EquipmentId == equipmentId.Value)
                .Select(x => x.TagNumber + " - " + x.Name)
                .FirstOrDefaultAsync();
        }

        return View(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int equipmentId, string strategyType, string? failureModeCode,
        string consequenceCategory, string? justification, string recommendedTriggerType,
        string? frequency, string? description, DateTime? startDate)
    {
        await _context.MaintenanceStrategies.AddAsync(new MaintenanceStrategy
        {
            EquipmentId = equipmentId,
            StrategyType = strategyType,
            FailureModeCode = failureModeCode,
            ConsequenceCategory = string.IsNullOrWhiteSpace(consequenceCategory) ? "Operational" : consequenceCategory,
            Justification = justification,
            RecommendedTriggerType = string.IsNullOrWhiteSpace(recommendedTriggerType) ? "Calendar" : recommendedTriggerType,
            Frequency = frequency,
            Description = description,
            StartDate = startDate,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = "Maintenance strategy added";
        return RedirectToAction(nameof(Index), new { equipmentId });
    }

    // ==============================================================================
    // طبق درخواست: تعریف استراتژی برای چند تجهیز هم‌زمان - چون در عمل RCM معمولاً روی
    // خانواده‌ای از تجهیزات مشابه (مثلاً همه‌ی پمپ‌های سانتریفیوژ) اعمال می‌شه، نه یکی‌یکی.
    // ==============================================================================
    [HttpGet]
    public async Task<IActionResult> BulkCreate(int? disciplineId, string? equipmentType)
    {
        ViewData["Title"] = "Bulk Define Maintenance Strategy";
        ViewData["Section"] = "CMMS Integration";

        var query = _context.Equipments
            .Include(x => x.Discipline)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (disciplineId.HasValue) query = query.Where(x => x.DisciplineId == disciplineId.Value);
        if (!string.IsNullOrWhiteSpace(equipmentType)) query = query.Where(x => x.EquipmentType == equipmentType);

        var equipmentList = await query.OrderBy(x => x.TagNumber).ToListAsync();

        // طبق RCM: کلاس بحرانی‌بودن هر تجهیز رو کنارش نشون می‌دیم تا انتخاب استراتژی
        // آگاهانه باشه (مثلاً کلاس A بهتره Preventive/Predictive بگیره، نه Corrective)
        var criticalityByEquipment = await _context.EquipmentCriticalities
            .Where(x => x.IsActive)
            .ToDictionaryAsync(x => x.EquipmentId, x => x.CriticalityClass);

        ViewBag.Equipments = equipmentList;
        ViewBag.CriticalityByEquipment = criticalityByEquipment;

        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();

        ViewBag.EquipmentTypes = await _context.Equipments.Where(x => x.IsActive && x.EquipmentType != "")
            .Select(x => x.EquipmentType).Distinct().OrderBy(x => x).ToListAsync();

        ViewBag.SelectedDisciplineId = disciplineId;
        ViewBag.SelectedEquipmentType = equipmentType;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkCreate(
        List<int> equipmentIds, string strategyType, string? failureModeCode,
        string consequenceCategory, string? justification, string recommendedTriggerType,
        string? frequency, string? description, DateTime? startDate)
    {
        if (equipmentIds == null || equipmentIds.Count == 0)
        {
            TempData["Error"] = "حداقل یک تجهیز باید انتخاب بشه.";
            return RedirectToAction(nameof(BulkCreate));
        }

        foreach (var equipmentId in equipmentIds)
        {
            await _context.MaintenanceStrategies.AddAsync(new MaintenanceStrategy
            {
                EquipmentId = equipmentId,
                StrategyType = strategyType,
                FailureModeCode = failureModeCode,
                ConsequenceCategory = string.IsNullOrWhiteSpace(consequenceCategory) ? "Operational" : consequenceCategory,
                Justification = justification,
                RecommendedTriggerType = string.IsNullOrWhiteSpace(recommendedTriggerType) ? "Calendar" : recommendedTriggerType,
                Frequency = frequency,
                Description = description,
                StartDate = startDate,
                IsActive = true,
                CreatedDate = DateTime.Now
            });
        }

        await _context.SaveChangesAsync();

        TempData["Message"] = $"استراتژی برای {equipmentIds.Count} تجهیز ثبت شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int strategyId)
    {
        var strategy = await _context.MaintenanceStrategies.FirstOrDefaultAsync(x => x.MaintenanceStrategyId == strategyId);
        if (strategy == null) return NotFound();

        strategy.IsActive = false;
        await _context.SaveChangesAsync();

        TempData["Message"] = "Strategy deactivated";
        return RedirectToAction(nameof(Index));
    }
}
