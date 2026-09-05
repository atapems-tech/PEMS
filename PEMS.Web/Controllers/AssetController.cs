using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق PEMS_CMMS_ARCHITECTURE.md بخش ۴: چرخه‌عمر CMMS یک Equipment - نگهداری، بازرسی،
/// خرابی و هزینه. Equipment والد باقی می‌ماند؛ Asset فقط جزئیات چرخه‌عمر CMMS را نگه می‌دارد،
/// دقیقاً هم‌ارز با الگوی Instrument.
/// </summary>
public class AssetController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly IAssetService _assetService;
    private readonly ICurrentUserService _currentUser;
    private readonly IFunctionalLocationService _functionalLocationService;

    public AssetController(PEMSDbContext context, IAssetService assetService, ICurrentUserService currentUser,
        IFunctionalLocationService functionalLocationService)
    {
        _context = context;
        _assetService = assetService;
        _currentUser = currentUser;
        _functionalLocationService = functionalLocationService;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۵C - Asset Reconciliation): قبلاً
    // pageSize=25 مستقیم و بدون Normalize پاس داده می‌شد، هیچ pageSize از QueryString پذیرفته
    // نمی‌شد، و View هیچ Pager/Column-resize ای نداشت.
    public async Task<IActionResult> Index(string? status, string? q, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Assets";
        ViewData["Section"] = "CMMS Integration";

        // برای مودال "New Asset" روی همین صفحه - دقیقاً همون الگوی Instrument/Index
        var equipmentIdsWithAsset = await _context.Assets.Where(x => x.IsActive).Select(x => x.EquipmentId).ToListAsync();
        ViewBag.Equipments = await _context.Equipments
            .Where(x => x.IsActive && !equipmentIdsWithAsset.Contains(x.EquipmentId))
            .Select(x => new { x.EquipmentId, Label = x.TagNumber + " - " + x.Name })
            .OrderBy(x => x.Label)
            .ToListAsync();

        ViewBag.FunctionalLocations = await _functionalLocationService.GetPickerListAsync();

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var result = await _assetService.GetListAsync(page, normalizedPageSize, status, q);

        ViewBag.SelectedStatus = status;
        ViewBag.SearchTerm = q;
        ViewBag.TotalCount = result.TotalCount;
        ViewBag.ActiveCount = result.Items.Count(x => x.Status == "Active");

        return View(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int equipmentId, string assetNumber, string? functionalLocation, string? maintenanceClass,
        DateTime? commissioningDate, string status, string? description, int? functionalLocationId)
    {
        var (success, error, assetId) = await _assetService.CreateAsync(
            equipmentId, assetNumber, functionalLocation, maintenanceClass, commissioningDate,
            status, description, _currentUser.UserName, functionalLocationId);

        if (!success)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index));
        }

        TempData["Message"] = "Asset created";
        return RedirectToAction(nameof(Details), new { id = assetId });
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "Asset Details";
        ViewData["Section"] = "CMMS Integration";

        var asset = await _assetService.GetDetailsAsync(id);
        if (asset == null) return NotFound();

        ViewBag.FunctionalLocations = await _functionalLocationService.GetPickerListAsync();

        return View(asset);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int assetId, string assetNumber, string? functionalLocation, string? maintenanceClass,
        DateTime? commissioningDate, string status, string? description, int? functionalLocationId)
    {
        var (success, error) = await _assetService.UpdateAsync(
            assetId, assetNumber, functionalLocation, maintenanceClass, commissioningDate,
            status, description, _currentUser.UserName, functionalLocationId);

        TempData[success ? "Message" : "Error"] = success ? "Asset updated" : error;
        return RedirectToAction(nameof(Details), new { id = assetId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        await _assetService.ToggleActiveAsync(id, _currentUser.UserName);
        TempData["Message"] = "Asset status updated";
        return RedirectToAction(nameof(Index));
    }
}
