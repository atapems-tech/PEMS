using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق «PEMS — Enterprise Barcode System»: یک کنترلر واحد و قابل‌گسترش برای کل قابلیت بارکد -
/// نه یک کنترلر جدا به‌ازای هر ماژول (EquipmentBarcodeController/WarehouseBarcodeController).
/// Resolve هرگز رکورد هدف را مستقیم برنمی‌گرداند - فقط به صفحه‌ی Details موجود همان منبع
/// Redirect می‌کند تا همون Authorization/UI موجود آن صفحه (هرچه که باشد) دوباره اعمال شود؛
/// یک بارکد معتبر هرگز به‌خودی‌خود دسترسی اضافه نمی‌دهد (طبق بند «Security»).
/// </summary>
public class BarcodeController : Controller
{
    private const string Module = "Barcode";

    private readonly IBarcodeService _barcodeService;
    private readonly IBarcodeRenderingService _renderingService;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public BarcodeController(IBarcodeService barcodeService, IBarcodeRenderingService renderingService,
        PEMSDbContext context, ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _barcodeService = barcodeService;
        _renderingService = renderingService;
        _context = context;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, Module, "View"))
            return Forbid();

        ViewData["Title"] = "Barcode";
        ViewData["Section"] = "CMMS Integration";

        var recent = await _context.BarcodeIdentities
            .OrderByDescending(x => x.CreatedDate)
            .Take(50)
            .Select(x => new { x.BarcodeIdentityId, x.Code, x.Symbology, x.TargetType, x.TargetId, x.Status, x.CreatedDate, x.PrintCount })
            .ToListAsync();

        ViewBag.RecentBarcodes = recent;
        return View();
    }

    // طبق بند «Scanning»: نقطه‌ی ورود مشترک برای هر مصرف‌کننده‌ای که یک بارکد را Scan/باز
    // می‌کند - وب، موبایل آینده، دوربین. فقط Decode+اعتبارسنجی+حداقل هویت را انجام می‌دهد،
    // سپس به صفحه‌ی Details موجودِ همان منبع Redirect می‌کند (طبق بند «Barcode Resolution»).
    [HttpGet]
    public async Task<IActionResult> Resolve(string code)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, Module, "View"))
            return Forbid();

        var result = await _barcodeService.ResolveAsync(code);

        if (!result.Success)
        {
            ViewBag.ErrorCode = result.ErrorCode;
            ViewBag.ErrorMessage = result.ErrorMessage;
            ViewBag.TargetType = result.TargetType;
            return View("ResolveError");
        }

        return result.TargetType switch
        {
            "Equipment" => RedirectToAction("Details", "Equipment", new { id = result.TargetId }),
            "MaterialItem" => RedirectToAction("Details", "MaterialItem", new { id = result.TargetId }),
            "Warehouse" or "StorageLocation" => RedirectToAction("Index", "InventoryMasterData"),
            _ => NotFound()
        };
    }

    // طبق بند «Barcode Verification»: تفاوت این اکشن با Resolve بالا - این‌جا کاربر صراحتاً
    // می‌خواهد هویت را با یک هدف مورد انتظار تطبیق دهد (مثلاً هنگام نصب برچسب روی تجهیز
    // اشتباه)، نه فقط باز کردن صفحه.
    [HttpGet]
    public async Task<IActionResult> Verify(string code, string? expectedTargetType, int? expectedTargetId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, Module, "View"))
            return Forbid();

        var result = await _barcodeService.ResolveAsync(code);

        ViewBag.Mismatch = result.Success && expectedTargetType != null && expectedTargetId.HasValue &&
                            (!string.Equals(result.TargetType, expectedTargetType, StringComparison.OrdinalIgnoreCase) || result.TargetId != expectedTargetId);

        var barcode = result.BarcodeIdentityId.HasValue ? await _barcodeService.GetByIdAsync(result.BarcodeIdentityId.Value) : null;
        ViewBag.Resolution = result;
        return View(barcode);
    }

    // طبق «PEMS — Warehouse and Inventory Barcode Integration» بند «Barcode Resolution Rules»:
    // Resolve بالا یک صفحه‌ی کامل را Redirect می‌کند - برای فرم‌های موجود Receive/Issue که با
    // اسکن باید فقط یک فیلد را از قبل پر کنند (بدون ترک صفحه)، یک نسخه‌ی JSON از همون
    // IBarcodeService.ResolveAsync لازم است - نه یک Resolver دوم، همون منطق، فقط قالب خروجی
    // متفاوت. هیچ داده‌ی کسب‌وکاری حساسی برنمی‌گرداند - فقط TargetType/TargetId/برچسب خوانا
    // (که خودش از قبل هرگز در بارکد کدگذاری نشده - طبق بند «Barcode Identity Model»).
    [HttpGet]
    public async Task<IActionResult> ResolveJson(string code)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, Module, "View"))
            return Forbid();

        var result = await _barcodeService.ResolveAsync(code);
        if (!result.Success)
            return Json(new { success = false, errorCode = result.ErrorCode, errorMessage = result.ErrorMessage });

        var barcode = await _barcodeService.GetByIdAsync(result.BarcodeIdentityId!.Value);
        return Json(new
        {
            success = true,
            targetType = result.TargetType,
            targetId = result.TargetId,
            label = barcode?.TargetDisplayLabel
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(string targetType, int targetId, string symbology = "Code128")
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, Module, "Create"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();

        try
        {
            var (barcode, wasExisting) = await _barcodeService.GenerateAsync(targetType, targetId, symbology, employeeId);
            TempData["Message"] = wasExisting
                ? "این هدف از قبل یک بارکد Active دارد - همان بازگردانده شد."
                : $"بارکد جدید ساخته شد: {barcode.Code}";
        }
        catch (InvalidOperationException ex)
        {
            // طبق ممیزی نهایی: رکورد هدف حذف/نامعتبر شده - به‌جای خطای ۵۰۰، پیام روشن
            TempData["Error"] = ex.Message;
        }

        return RedirectToDetails(targetType, targetId);
    }

    [HttpGet]
    public async Task<IActionResult> BulkGenerate(List<int>? disciplineIds, List<string>? equipmentTypes, string? tagSearch)
    {
        // طبق بند «Bulk Barcode Generation»: پرریسک‌ترین اکشن این پایپ‌لاین - Approve
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, Module, "Approve"))
            return Forbid();

        ViewData["Title"] = "Bulk Generate Equipment Barcodes";
        ViewData["Section"] = "CMMS Integration";

        disciplineIds ??= new List<int>();
        equipmentTypes ??= new List<string>();

        const int maxResults = 200;
        List<PEMS.Domain.Entities.Equipment> equipmentList;

        var hasFilter = disciplineIds.Count > 0 || equipmentTypes.Count > 0 || !string.IsNullOrWhiteSpace(tagSearch);
        if (!hasFilter)
        {
            equipmentList = new List<PEMS.Domain.Entities.Equipment>();
            ViewBag.NoFilterApplied = true;
        }
        else
        {
            var query = _context.Equipments.Where(x => x.IsActive).AsQueryable();
            if (disciplineIds.Count > 0) query = query.Where(x => disciplineIds.Contains(x.DisciplineId));
            if (equipmentTypes.Count > 0) query = query.Where(x => equipmentTypes.Contains(x.EquipmentType));
            if (!string.IsNullOrWhiteSpace(tagSearch)) query = query.Where(x => x.TagNumber.Contains(tagSearch.Trim()));

            ViewBag.MatchingCount = await query.CountAsync();
            equipmentList = await query.OrderBy(x => x.TagNumber).Take(maxResults).ToListAsync();
            ViewBag.NoFilterApplied = false;
        }

        ViewBag.MaxResults = maxResults;
        ViewBag.Equipments = equipmentList;
        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();
        ViewBag.EquipmentTypes = await _context.Equipments.Where(x => x.IsActive && x.EquipmentType != "")
            .Select(x => x.EquipmentType).Distinct().OrderBy(x => x).ToListAsync();

        // طبق بند «Duplicate Protection»: نشان می‌دهد کدام تجهیزات فیلترشده از قبل بارکد Active دارند
        var equipmentIds = equipmentList.Select(x => x.EquipmentId).ToList();
        ViewBag.AlreadyBarcodedIds = await _context.BarcodeIdentities
            .Where(x => x.TargetType == "Equipment" && equipmentIds.Contains(x.TargetId) && x.Status == "Active")
            .Select(x => x.TargetId).ToListAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkGenerateCommit(List<int> equipmentIds, string symbology = "Code128")
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, Module, "Approve"))
            return Forbid();

        if (equipmentIds == null || equipmentIds.Count == 0)
        {
            TempData["Error"] = "حداقل یک تجهیز باید انتخاب بشه.";
            return RedirectToAction(nameof(BulkGenerate));
        }

        var employeeId = await GetCurrentEmployeeIdAsync();
        var result = await _barcodeService.BulkGenerateAsync("Equipment", equipmentIds, symbology, employeeId);

        TempData["Message"] = $"{result.Created} بارکد جدید ساخته شد، {result.AlreadyExisted} مورد از قبل داشت.";
        return RedirectToAction(nameof(BulkLabelSheet), new { targetType = "Equipment", ids = string.Join(",", result.BarcodeIdentityIds) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Print(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, Module, "Create"))
            return Forbid();

        var (success, error) = await _barcodeService.RecordPrintAsync(id);
        if (!success) return NotFound(error);

        return await LabelPdf(id);
    }

    [HttpGet]
    public async Task<IActionResult> LabelPdf(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, Module, "View"))
            return Forbid();

        var barcode = await _barcodeService.GetByIdAsync(id);
        if (barcode == null) return NotFound();

        var pdf = _renderingService.RenderLabelSheetPdf(new List<BarcodeLabelContent>
        {
            new()
            {
                Code = barcode.Code,
                Symbology = barcode.Symbology,
                TitleLine = barcode.TargetDisplayLabel ?? $"{barcode.TargetType} #{barcode.TargetId}",
                SubtitleLine = barcode.TargetType,
                MetaLine = $"Generated {barcode.CreatedDate:yyyy-MM-dd}"
            }
        });

        return File(pdf, "application/pdf", $"{barcode.Code}.pdf");
    }

    // طبق بند «Bulk Barcode Generation»: چاپ دسته‌ای بلافاصله بعد از BulkGenerateCommit -
    // یک PDF واحد با یک برچسب به‌ازای هر بارکد ساخته‌شده (نه یک درخواست جدا به‌ازای هر برچسب).
    [HttpGet]
    public async Task<IActionResult> BulkLabelSheet(string targetType, string ids)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, Module, "View"))
            return Forbid();

        var idList = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var v) ? v : (int?)null)
            .Where(x => x.HasValue).Select(x => x!.Value).ToList();

        if (idList.Count == 0) return NotFound();

        var labels = new List<BarcodeLabelContent>();
        foreach (var id in idList)
        {
            var barcode = await _barcodeService.GetByIdAsync(id);
            if (barcode == null) continue;

            labels.Add(new BarcodeLabelContent
            {
                Code = barcode.Code,
                Symbology = barcode.Symbology,
                TitleLine = barcode.TargetDisplayLabel ?? $"{barcode.TargetType} #{barcode.TargetId}",
                SubtitleLine = barcode.TargetType,
                MetaLine = $"Generated {barcode.CreatedDate:yyyy-MM-dd}"
            });

            await _barcodeService.RecordPrintAsync(id);
        }

        var pdf = _renderingService.RenderLabelSheetPdf(labels);
        return File(pdf, "application/pdf", $"barcodes-{targetType}-{DateTime.Now:yyyyMMddHHmm}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> Svg(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, Module, "View"))
            return Forbid();

        var barcode = await _barcodeService.GetByIdAsync(id);
        if (barcode == null) return NotFound();

        var svg = _renderingService.RenderSvg(barcode.Code, barcode.Symbology);
        return Content(svg, "image/svg+xml");
    }

    [HttpGet]
    public async Task<IActionResult> Png(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, Module, "View"))
            return Forbid();

        var barcode = await _barcodeService.GetByIdAsync(id);
        if (barcode == null) return NotFound();

        var png = _renderingService.RenderPng(barcode.Code, barcode.Symbology);
        return File(png, "image/png");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(int id, string? reason, string targetType, int targetId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, Module, "Delete"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var (success, error) = await _barcodeService.DisableAsync(id, reason, employeeId);
        TempData[success ? "Message" : "Error"] = success ? "بارکد غیرفعال شد." : error;
        return RedirectToDetails(targetType, targetId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int id, string? reason, string targetType, int targetId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, Module, "Delete"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var (success, error) = await _barcodeService.RevokeAsync(id, reason, employeeId);
        TempData[success ? "Message" : "Error"] = success ? "بارکد Revoke شد." : error;
        return RedirectToDetails(targetType, targetId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Replace(int id, string targetType, int targetId, string symbology = "Code128")
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, Module, "Delete"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var (newBarcode, error) = await _barcodeService.ReplaceAsync(id, symbology, employeeId);
        TempData[newBarcode != null ? "Message" : "Error"] = newBarcode != null ? $"بارکد جدید ساخته شد: {newBarcode.Code}" : error;
        return RedirectToDetails(targetType, targetId);
    }

    private IActionResult RedirectToDetails(string targetType, int targetId) => targetType switch
    {
        "Equipment" => RedirectToAction("Details", "Equipment", new { id = targetId }),
        "MaterialItem" => RedirectToAction("Details", "MaterialItem", new { id = targetId }),
        "Warehouse" or "StorageLocation" => RedirectToAction("Index", "InventoryMasterData"),
        _ => RedirectToAction(nameof(Index))
    };

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;

        return await _context.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();
    }
}
