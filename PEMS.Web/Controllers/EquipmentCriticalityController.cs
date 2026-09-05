using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق بازنگری CMMS: تحلیل بحرانی‌بودن تجهیز (Criticality Analysis) - طبق ISO 55000/RCM یکی
/// از پایه‌های اولویت‌بندی نگهداری است. Entity از قبل در مدل دیتابیس بود ولی هیچ Controller/
/// View/راه دسترسی‌ای نداشت - کاملاً بلااستفاده مونده بود.
/// </summary>
public class EquipmentCriticalityController : Controller
{
    private readonly PEMSDbContext _context;

    public EquipmentCriticalityController(PEMSDbContext context)
    {
        _context = context;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): قبلاً کل جدول فیلترشده با
    // ToListAsync (بدون Skip/Take) بارگذاری می‌شد؛ شمارنده‌های Dashboard هم با یک بارگذاری کامل
    // دوم (allActive.ToListAsync سپس شمارش در حافظه) محاسبه می‌شدند - این‌جا با GroupBy/CountAsync
    // در خودِ SQL انجام می‌شود. شمارنده‌ها عمداً کلی/بدون فیلتر باقی مانده‌اند (رفتار قبلی).
    public async Task<IActionResult> Index(string? criticalityClass, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Equipment Criticality Analysis";
        ViewData["Section"] = "CMMS Integration";

        var query = _context.EquipmentCriticalities.Where(x => x.IsActive).AsQueryable();

        if (!string.IsNullOrWhiteSpace(criticalityClass))
            query = query.Where(x => x.CriticalityClass == criticalityClass);

        var totalCount = await query.CountAsync();

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);

        var items = await query
            .OrderBy(x => x.CriticalityClass)
            .ThenBy(x => x.Equipment!.TagNumber)
            .ThenBy(x => x.EquipmentCriticalityId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new EquipmentCriticalityListDto
            {
                EquipmentCriticalityId = x.EquipmentCriticalityId,
                EquipmentId = x.EquipmentId,
                EquipmentTagNumber = x.Equipment!.TagNumber,
                CriticalityClass = x.CriticalityClass,
                RiskLevel = x.RiskLevel,
                SafetyImpact = x.SafetyImpact,
                ProductionImpact = x.ProductionImpact,
                EnvironmentImpact = x.EnvironmentImpact
            })
            .ToListAsync();

        var classCounts = await _context.EquipmentCriticalities.Where(x => x.IsActive)
            .GroupBy(x => x.CriticalityClass)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count);

        ViewBag.ClassACount = classCounts.GetValueOrDefault("A");
        ViewBag.ClassBCount = classCounts.GetValueOrDefault("B");
        ViewBag.ClassCCount = classCounts.GetValueOrDefault("C");

        // تجهیزاتی که هنوز هیچ تحلیل بحرانی‌بودنی ندارند - طبق اصل «تحلیل شکاف» که جای دیگه هم
        // استفاده کردیم؛ به‌جای بارگذاری کامل EquipmentCriticalities در حافظه، با EXISTS در SQL.
        ViewBag.UnassessedCount = await _context.Equipments.CountAsync(x => x.IsActive &&
            !_context.EquipmentCriticalities.Any(c => c.EquipmentId == x.EquipmentId && c.IsActive));

        ViewBag.SelectedClass = criticalityClass;

        return View(new PagedResult<EquipmentCriticalityListDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assess(
        int equipmentId, string criticalityClass, string? riskLevel,
        string? safetyImpact, string? productionImpact, string? environmentImpact, string? description)
    {
        var existing = await _context.EquipmentCriticalities
            .FirstOrDefaultAsync(x => x.EquipmentId == equipmentId && x.IsActive);

        if (existing != null)
        {
            // طبق اصل «یک تحلیل فعال به‌ازای هر تجهیز» - تحلیل قبلی جایگزین می‌شود، نه تکرار می‌شود
            existing.IsActive = false;
        }

        await _context.EquipmentCriticalities.AddAsync(new EquipmentCriticality
        {
            EquipmentId = equipmentId,
            CriticalityClass = criticalityClass,
            RiskLevel = riskLevel,
            SafetyImpact = safetyImpact,
            ProductionImpact = productionImpact,
            EnvironmentImpact = environmentImpact,
            Description = description,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = "تحلیل بحرانی‌بودن ثبت شد.";
        return RedirectToAction(nameof(Index));
    }
}
