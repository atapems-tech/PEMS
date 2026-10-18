using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Web.Security;
using WarehouseEntity = PEMS.Domain.Entities.Warehouse; // نام Area «Warehouse» با نام Entity هم‌پوشانی دارد

namespace PEMS.Web.Areas.Warehouse.Controllers;

/// <summary>
/// طبق سند Inventory بخش ۶-۸-۲۵: به‌جای سه صفحه‌ی جدا برای Lookup های کوچک (Category/UOM/
/// Warehouse)، یک صفحه با تب - همون الگوی MaintenanceMasterDataController.
/// </summary>
/// <remarks>
/// طبق «PEMS — P1 Organization &amp; Master Data Authorization Hardening»: این Controller قبلاً
/// هیچ بررسی مجوزی نداشت - Create*/Toggle*Active برای هر کاربر احرازهویت‌شده‌ای باز بودند.
/// ماژول جدید "InventoryMasterData" چون هیچ ماژول موجودی این قابلیت (پیکربندی مرجع دامنه‌ی
/// انبار/موجودی) را پوشش نمی‌دهد - همون نقش‌های عملیاتی CMMS که سایدبار «Inventory & Materials»
/// از قبل به آن‌ها نشان می‌دهد (MenuAccess.CmmsIntegration).
/// </remarks>
[Area("Warehouse")]
public class InventoryMasterDataController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public InventoryMasterDataController(PEMSDbContext context, ICurrentUserService currentUser,
        IPermissionService permissionService)
    {
        _context = context;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "InventoryMasterData", "View"))
            return Forbid();

        ViewData["Title"] = "Inventory Master Data";
        ViewData["Section"] = "Inventory & Materials";

        var categories = await _context.MaterialCategories
            .Include(x => x.Parent)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        ViewBag.Categories = categories;
        ViewBag.TopLevelCategories = categories.Where(x => x.ParentCategoryId == null).ToList();

        ViewBag.Uoms = await _context.MaterialUOMs.Where(x => x.IsActive).OrderBy(x => x.Code).ToListAsync();

        var warehouses = await _context.Warehouses
            .Include(x => x.Locations)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .ToListAsync();

        return View(warehouses);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(string code, string name, int? parentCategoryId, string? description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "InventoryMasterData", "Create"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "کد و نام دسته‌بندی الزامی هستند.";
            return RedirectToAction(nameof(Index));
        }

        if (await _context.MaterialCategories.AnyAsync(x => x.Code == code.Trim()))
        {
            TempData["Error"] = "دسته‌بندی با همین کد از قبل وجود دارد.";
            return RedirectToAction(nameof(Index));
        }

        await _context.MaterialCategories.AddAsync(new MaterialCategory
        {
            Code = code.Trim(),
            Name = name.Trim(),
            ParentCategoryId = parentCategoryId,
            Description = description,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Message"] = "دسته‌بندی ساخته شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCategoryActive(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "InventoryMasterData", "Edit"))
            return Forbid();

        var category = await _context.MaterialCategories.FirstOrDefaultAsync(x => x.MaterialCategoryId == id);
        if (category == null) return NotFound();

        category.IsActive = !category.IsActive;
        await _context.SaveChangesAsync();
        TempData["Message"] = "وضعیت دسته‌بندی به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUom(string code, string name, decimal? conversionFactor, string? description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "InventoryMasterData", "Create"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "کد و نام واحد الزامی هستند.";
            return RedirectToAction(nameof(Index));
        }

        if (await _context.MaterialUOMs.AnyAsync(x => x.Code == code.Trim()))
        {
            TempData["Error"] = "واحدی با همین کد از قبل وجود دارد.";
            return RedirectToAction(nameof(Index));
        }

        await _context.MaterialUOMs.AddAsync(new MaterialUOM
        {
            Code = code.Trim(),
            Name = name.Trim(),
            ConversionFactor = conversionFactor,
            Description = description,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Message"] = "واحد اندازه‌گیری ساخته شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUomActive(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "InventoryMasterData", "Edit"))
            return Forbid();

        var uom = await _context.MaterialUOMs.FirstOrDefaultAsync(x => x.MaterialUOMId == id);
        if (uom == null) return NotFound();

        uom.IsActive = !uom.IsActive;
        await _context.SaveChangesAsync();
        TempData["Message"] = "وضعیت واحد به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateWarehouse(string code, string name, string? description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "InventoryMasterData", "Create"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "کد و نام انبار الزامی هستند.";
            return RedirectToAction(nameof(Index));
        }

        if (await _context.Warehouses.AnyAsync(x => x.Code == code.Trim()))
        {
            TempData["Error"] = "انباری با همین کد از قبل وجود دارد.";
            return RedirectToAction(nameof(Index));
        }

        await _context.Warehouses.AddAsync(new WarehouseEntity
        {
            Code = code.Trim(),
            Name = name.Trim(),
            Description = description,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Message"] = "انبار ساخته شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleWarehouseActive(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "InventoryMasterData", "Edit"))
            return Forbid();

        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(x => x.WarehouseId == id);
        if (warehouse == null) return NotFound();

        warehouse.IsActive = !warehouse.IsActive;
        await _context.SaveChangesAsync();
        TempData["Message"] = "وضعیت انبار به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStorageLocation(int warehouseId, string code, string name, int? parentLocationId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "InventoryMasterData", "Create"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "کد و نام محل نگهداری الزامی هستند.";
            return RedirectToAction(nameof(Index));
        }

        if (await _context.StorageLocations.AnyAsync(x => x.WarehouseId == warehouseId && x.Code == code.Trim()))
        {
            TempData["Error"] = "محل نگهداری با همین کد در این انبار از قبل وجود دارد.";
            return RedirectToAction(nameof(Index));
        }

        await _context.StorageLocations.AddAsync(new StorageLocation
        {
            WarehouseId = warehouseId,
            Code = code.Trim(),
            Name = name.Trim(),
            ParentLocationId = parentLocationId,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Message"] = "محل نگهداری ساخته شد.";
        return RedirectToAction(nameof(Index));
    }
}
