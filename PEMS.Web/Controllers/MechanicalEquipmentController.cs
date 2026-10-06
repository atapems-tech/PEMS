using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۳:
/// دقیقاً همون الگوی InstrumentController (PEMS.Web/Controllers/InstrumentController.cs) - Equipment
/// والد باقی می‌ماند، MechanicalEquipment فقط جزئیات اختصاصی مکانیکال. برخلاف Instrument (یک‌به‌یک
/// تخت با Equipment)، این‌جا یک زنجیره‌ی دو سطحی وجود دارد: MechanicalEquipment (پایه) و به‌صورت
/// اختیاری دقیقاً یکی از RotatingEquipment/StaticEquipment (زیررده) - هردو با MechanicalEquipmentId.
/// طبق ممیزی Phase 0: هیچ ماژول مجوز اختصاصی امروز برای Instrument/Equipment وجود ندارد
/// (NavigationCatalog.CmmsIntegrationItemResourceMap["Instrument"]=null) - این Controller هم همون
/// «no reliable permission signal» را دنبال می‌کند، نه یک استثنای تازه.
/// </summary>
public class MechanicalEquipmentController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly IMechanicalEquipmentService _mechanicalEquipmentService;

    public MechanicalEquipmentController(PEMSDbContext context, IMechanicalEquipmentService mechanicalEquipmentService)
    {
        _context = context;
        _mechanicalEquipmentService = mechanicalEquipmentService;
    }

    public async Task<IActionResult> Index(string? subType, string? q, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Master Mechanical Equipment List";
        ViewData["Section"] = "CMMS Integration";

        var equipmentIdsWithMechanical = await _context.MechanicalEquipments
            .Where(x => x.EquipmentId != null)
            .Select(x => x.EquipmentId)
            .ToListAsync();
        ViewBag.Equipments = await _context.Equipments
            .Where(x => x.IsActive && !equipmentIdsWithMechanical.Contains(x.EquipmentId))
            .Select(x => new { x.EquipmentId, Label = x.TagNumber + " - " + x.Name })
            .OrderBy(x => x.Label)
            .ToListAsync();

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var result = await _mechanicalEquipmentService.GetListAsync(page, normalizedPageSize, subType, q);
        var dashboard = await _mechanicalEquipmentService.GetDashboardAsync(q);

        ViewBag.SelectedSubType = subType;
        ViewBag.SearchTerm = q;
        ViewBag.RotatingCount = dashboard.RotatingCount;
        ViewBag.StaticCount = dashboard.StaticCount;
        ViewBag.UnclassifiedCount = dashboard.UnclassifiedCount;
        ViewBag.TotalCount = dashboard.TotalCount;

        return View(result);
    }

    public async Task<IActionResult> Passport(int id)
    {
        ViewData["Title"] = "Mechanical Equipment Passport";
        ViewData["Section"] = "CMMS Integration";

        var mechanical = await _context.MechanicalEquipments
            .Include(x => x.Equipment)
            .FirstOrDefaultAsync(x => x.MechanicalEquipmentId == id);

        if (mechanical == null) return NotFound();

        ViewBag.Rotating = await _context.RotatingEquipments
            .FirstOrDefaultAsync(x => x.MechanicalEquipmentId == id && x.IsActive);
        ViewBag.Static = await _context.StaticEquipments
            .FirstOrDefaultAsync(x => x.MechanicalEquipmentId == id && x.IsActive);

        return View(mechanical);
    }

    // طبق درخواست/الگوی Instrument.Create: بدون صفحه‌ی جدا - فرم ایجاد یک مودال روی Index.cshtml است.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int? equipmentId, string tagNumber, string equipmentType,
        string? manufacturer, string? model, string? description)
    {
        if (string.IsNullOrWhiteSpace(tagNumber) || string.IsNullOrWhiteSpace(equipmentType))
        {
            TempData["Error"] = "Tag Number و Equipment Type الزامی هستند.";
            return RedirectToAction(nameof(Index));
        }

        // طبق فاز ۳/بند ۲ ممیزی: EquipmentId اختیاری است (زنجیره‌ی Nullable موجود) - اما اگر
        // انتخاب شد، یک‌به‌یک با Equipment است (همون قرارداد Instrument).
        if (equipmentId.HasValue)
        {
            var duplicate = await _context.MechanicalEquipments.AnyAsync(x => x.EquipmentId == equipmentId.Value);
            if (duplicate)
            {
                TempData["Error"] = "این تجهیز از قبل یک پروفایل Mechanical Equipment دارد.";
                return RedirectToAction(nameof(Index));
            }
        }

        var mechanical = new MechanicalEquipment
        {
            EquipmentId = equipmentId,
            TagNumber = tagNumber.Trim(),
            EquipmentType = equipmentType.Trim(),
            Manufacturer = manufacturer,
            Model = model,
            Description = description,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.MechanicalEquipments.AddAsync(mechanical);
        await _context.SaveChangesAsync();

        TempData["Message"] = "Mechanical Equipment ساخته شد.";
        return RedirectToAction(nameof(Passport), new { id = mechanical.MechanicalEquipmentId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "Edit Mechanical Equipment";
        ViewData["Section"] = "CMMS Integration";

        var mechanical = await _context.MechanicalEquipments
            .Include(x => x.Equipment)
            .FirstOrDefaultAsync(x => x.MechanicalEquipmentId == id);
        if (mechanical == null) return NotFound();

        return View(mechanical);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int mechanicalEquipmentId, MechanicalEquipment form)
    {
        var mechanical = await _context.MechanicalEquipments.FirstOrDefaultAsync(x => x.MechanicalEquipmentId == mechanicalEquipmentId);
        if (mechanical == null) return NotFound();

        mechanical.EquipmentType = form.EquipmentType;
        mechanical.DesignPressure = form.DesignPressure;
        mechanical.DesignTemperature = form.DesignTemperature;
        mechanical.OperatingPressure = form.OperatingPressure;
        mechanical.OperatingTemperature = form.OperatingTemperature;
        mechanical.Material = form.Material;
        mechanical.Manufacturer = form.Manufacturer;
        mechanical.Model = form.Model;
        mechanical.Description = form.Description;

        await _context.SaveChangesAsync();

        TempData["Message"] = "Mechanical Equipment به‌روزرسانی شد.";
        return RedirectToAction(nameof(Passport), new { id = mechanicalEquipmentId });
    }

    // طبق فاز ۳: زیررده‌ی اختیاری - دقیقاً یکی از Rotating/Static مجاز است (بررسی سطح سرویس،
    // نه محدودیت پایگاه‌داده) - همون قرارداد XOR که در طراحی Finding هم استفاده شد.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClassifyRotating(int mechanicalEquipmentId, string equipmentType,
        decimal? flowRate, decimal? head, int? speedRPM, decimal? powerKW, string? driverType,
        string? bearingType, string? lubricationType, string? description)
    {
        var mechanical = await _context.MechanicalEquipments.FirstOrDefaultAsync(x => x.MechanicalEquipmentId == mechanicalEquipmentId);
        if (mechanical == null) return NotFound();

        if (await _context.StaticEquipments.AnyAsync(x => x.MechanicalEquipmentId == mechanicalEquipmentId))
        {
            TempData["Error"] = "این تجهیز از قبل زیررده‌ی Static دارد - یک تجهیز نمی‌تواند هم Rotating هم Static باشد.";
            return RedirectToAction(nameof(Passport), new { id = mechanicalEquipmentId });
        }

        var rotating = await _context.RotatingEquipments.FirstOrDefaultAsync(x => x.MechanicalEquipmentId == mechanicalEquipmentId);
        if (rotating == null)
        {
            rotating = new RotatingEquipment { MechanicalEquipmentId = mechanicalEquipmentId, CreatedDate = DateTime.Now, IsActive = true };
            await _context.RotatingEquipments.AddAsync(rotating);
        }

        rotating.EquipmentType = string.IsNullOrWhiteSpace(equipmentType) ? rotating.EquipmentType : equipmentType.Trim();
        rotating.FlowRate = flowRate;
        rotating.Head = head;
        rotating.SpeedRPM = speedRPM;
        rotating.PowerKW = powerKW;
        rotating.DriverType = driverType;
        rotating.BearingType = bearingType;
        rotating.LubricationType = lubricationType;
        rotating.Description = description;

        await _context.SaveChangesAsync();
        TempData["Message"] = "زیررده‌ی Rotating Equipment ثبت شد.";
        return RedirectToAction(nameof(Passport), new { id = mechanicalEquipmentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClassifyStatic(int mechanicalEquipmentId, string equipmentType,
        decimal? volume, decimal? designPressure, decimal? designTemperature, string? material,
        string? orientation, string? codeStandard, string? description)
    {
        var mechanical = await _context.MechanicalEquipments.FirstOrDefaultAsync(x => x.MechanicalEquipmentId == mechanicalEquipmentId);
        if (mechanical == null) return NotFound();

        if (await _context.RotatingEquipments.AnyAsync(x => x.MechanicalEquipmentId == mechanicalEquipmentId))
        {
            TempData["Error"] = "این تجهیز از قبل زیررده‌ی Rotating دارد - یک تجهیز نمی‌تواند هم Rotating هم Static باشد.";
            return RedirectToAction(nameof(Passport), new { id = mechanicalEquipmentId });
        }

        var staticEq = await _context.StaticEquipments.FirstOrDefaultAsync(x => x.MechanicalEquipmentId == mechanicalEquipmentId);
        if (staticEq == null)
        {
            staticEq = new StaticEquipment { MechanicalEquipmentId = mechanicalEquipmentId, CreatedDate = DateTime.Now, IsActive = true };
            await _context.StaticEquipments.AddAsync(staticEq);
        }

        staticEq.EquipmentType = string.IsNullOrWhiteSpace(equipmentType) ? staticEq.EquipmentType : equipmentType.Trim();
        staticEq.Volume = volume;
        staticEq.DesignPressure = designPressure;
        staticEq.DesignTemperature = designTemperature;
        staticEq.Material = material;
        staticEq.Orientation = orientation;
        staticEq.CodeStandard = codeStandard;
        staticEq.Description = description;

        await _context.SaveChangesAsync();
        TempData["Message"] = "زیررده‌ی Static Equipment ثبت شد.";
        return RedirectToAction(nameof(Passport), new { id = mechanicalEquipmentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var mechanical = await _context.MechanicalEquipments.FirstOrDefaultAsync(x => x.MechanicalEquipmentId == id);
        if (mechanical == null) return NotFound();

        mechanical.IsActive = !mechanical.IsActive;
        await _context.SaveChangesAsync();

        TempData["Message"] = "وضعیت Mechanical Equipment به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }
}
