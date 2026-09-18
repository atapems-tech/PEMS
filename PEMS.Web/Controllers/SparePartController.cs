using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق بازنگری CMMS: کاتالوگ قطعات یدکی + موجودی انبار واقعی (QuantityOnHand، حداقل
/// موجودی، نقطه‌ی سفارش مجدد) - قبلاً این ماژول فقط کاتالوگ بود، بدون هیچ ردیابی موجودی.
/// </summary>
public class SparePartController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly ISparePartService _sparePartService;

    public SparePartController(PEMSDbContext context, ISparePartService sparePartService)
    {
        _context = context;
        _sparePartService = sparePartService;
    }

    // طبق سند «PEMS — Global Data Grid Standard»: قبلاً کل جدول SpareParts دو بار با
    // ToListAsync بارگذاری می‌شد (یک بار برای Grid، یک بار جداگانه فقط برای شمارنده‌ی
    // LowStockCount) و فیلتر lowStockOnly در حافظه اعمال می‌شد. اکنون همه در SQL انجام می‌شود.
    public async Task<IActionResult> Index(string? q, bool lowStockOnly = false, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Spare Parts";
        ViewData["Section"] = "CMMS Integration";
        ViewData["HelpKey"] = "SparePart.Index";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var result = await _sparePartService.GetListAsync(page, normalizedPageSize, q, lowStockOnly);

        ViewBag.LowStockCount = await _sparePartService.GetLowStockCountAsync();
        ViewBag.LowStockOnly = lowStockOnly;
        ViewBag.SearchTerm = q;

        return View(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string partNumber, string partName, string? manufacturer,
        string? model, string? unit, string? description,
        decimal quantityOnHand, decimal? minStockLevel, decimal? reorderPoint, string? storageLocation)
    {
        if (string.IsNullOrWhiteSpace(partNumber) || string.IsNullOrWhiteSpace(partName))
        {
            TempData["Error"] = "Part number and name are required";
            return RedirectToAction(nameof(Index));
        }

        await _context.SpareParts.AddAsync(new SparePart
        {
            PartNumber = partNumber.Trim(),
            PartName = partName.Trim(),
            Manufacturer = manufacturer,
            Model = model,
            Unit = unit,
            Description = description,
            QuantityOnHand = quantityOnHand,
            MinStockLevel = minStockLevel,
            ReorderPoint = reorderPoint,
            StorageLocation = storageLocation,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = "Spare part added";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int sparePartId, string partNumber, string partName,
        string? manufacturer, string? model, string? unit, string? description,
        decimal quantityOnHand, decimal? minStockLevel, decimal? reorderPoint, string? storageLocation)
    {
        var part = await _context.SpareParts.FirstOrDefaultAsync(x => x.SparePartId == sparePartId);
        if (part == null) return NotFound();

        part.PartNumber = partNumber.Trim();
        part.PartName = partName.Trim();
        part.Manufacturer = manufacturer;
        part.Model = model;
        part.Unit = unit;
        part.Description = description;
        part.QuantityOnHand = quantityOnHand;
        part.MinStockLevel = minStockLevel;
        part.ReorderPoint = reorderPoint;
        part.StorageLocation = storageLocation;

        await _context.SaveChangesAsync();

        TempData["Message"] = "Spare part updated";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int sparePartId)
    {
        var part = await _context.SpareParts.FirstOrDefaultAsync(x => x.SparePartId == sparePartId);
        if (part == null) return NotFound();

        part.IsActive = false;
        await _context.SaveChangesAsync();

        TempData["Message"] = "Spare part deactivated";
        return RedirectToAction(nameof(Index));
    }
}
