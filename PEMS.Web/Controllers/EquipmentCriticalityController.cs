using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق بازنگری CMMS: تحلیل بحرانی‌بودن تجهیز (Criticality Analysis) - طبق ISO 55000/RCM یکی
/// از پایه‌های اولویت‌بندی نگهداری است. Entity از قبل در مدل دیتابیس بود ولی هیچ Controller/
/// View/راه دسترسی‌ای نداشت - کاملاً بلااستفاده مونده بود.
/// </summary>
public class EquipmentCriticalityController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUser;

    public EquipmentCriticalityController(PEMSDbContext context, IPermissionService permissionService,
        ICurrentUserService currentUser)
    {
        _context = context;
        _permissionService = permissionService;
        _currentUser = currentUser;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): قبلاً کل جدول فیلترشده با
    // ToListAsync (بدون Skip/Take) بارگذاری می‌شد؛ شمارنده‌های Dashboard هم با یک بارگذاری کامل
    // دوم (allActive.ToListAsync سپس شمارش در حافظه) محاسبه می‌شدند - این‌جا با GroupBy/CountAsync
    // در خودِ SQL انجام می‌شود. شمارنده‌ها عمداً کلی/بدون فیلتر باقی مانده‌اند (رفتار قبلی).
    // طبق ممیزی مستقل فاز ۶B Wave A (P0): این Action هیچ بررسی مجوزی نداشت.
    public async Task<IActionResult> Index(string? criticalityClass, int page = 1, int? pageSize = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "EquipmentCriticality", "View"))
            return Forbid();

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
}
