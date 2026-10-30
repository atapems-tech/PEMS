using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using PEMS.Application.Organization;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Services;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// Organization Import (از Import Center). جریان: Upload → Validate (بدون ذخیره) → Confirm (ذخیره‌ی همه‌یا‌هیچ).
/// فایل تأییدشده در حافظه‌ی سرور (با توکن تصادفی، ۳۰ دقیقه، وابسته به کاربر) نگه داشته می‌شود؛ مرورگر هیچ داده‌ی ساختاری برنمی‌گرداند.
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class OrganizationImportController : Controller
{
    private readonly IOrganizationImportService _import;
    private readonly IMemoryCache _cache;
    private readonly IStringLocalizer<SharedResource> _l;

    public OrganizationImportController(IOrganizationImportService import, IMemoryCache cache, IStringLocalizer<SharedResource> l)
    {
        _import = import;
        _cache = cache;
        _l = l;
    }

    private string CacheKey(string token) => $"orgimport:{User.Identity?.Name}:{token}";

    [HttpGet]
    public IActionResult Index()
    {
        if (!ImportCatalog.CanUse("OrganizationStructure", User)) return Forbid();
        ViewData["Title"] = "Organization Import";
        ViewData["Section"] = "Administration";
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Template()
    {
        if (!ImportCatalog.CanUse("OrganizationStructure", User)) return Forbid();
        return File(await _import.BuildTemplateAsync(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "OrganizationStructureTemplate.xlsx");
    }

    [HttpPost, ValidateAntiForgeryToken]
    [PEMS.Web.Security.Uploads.UploadPolicy(PEMS.Application.Uploads.UploadProfile.Spreadsheet)]
    public async Task<IActionResult> Validate(IFormFile? file)
    {
        if (!ImportCatalog.CanUse("OrganizationStructure", User)) return Forbid();
        if (file == null || file.Length == 0) { TempData["Error"] = _l["Org_Imp_NoFile"].Value; return RedirectToAction(nameof(Index)); }
        if (file.Length > 5 * 1024 * 1024 || !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        { TempData["Error"] = _l["Org_Imp_BadFile"].Value; return RedirectToAction(nameof(Index)); }

        byte[] bytes;
        using (var ms = new MemoryStream()) { await file.CopyToAsync(ms); bytes = ms.ToArray(); }

        OrganizationImportResult result;
        using (var s = new MemoryStream(bytes)) result = await _import.ImportAsync(s, commit: false);

        string? token = null;
        if (result.Errors.Count == 0)
        {
            token = Guid.NewGuid().ToString("N");
            _cache.Set(CacheKey(token), bytes, TimeSpan.FromMinutes(30));
        }
        ViewBag.Token = token;
        ViewData["Title"] = "Organization Import";
        ViewData["Section"] = "Administration";
        return View("Result", result);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(string token)
    {
        if (!ImportCatalog.CanUse("OrganizationStructure", User)) return Forbid();
        if (string.IsNullOrWhiteSpace(token) || !_cache.TryGetValue(CacheKey(token), out byte[]? bytes) || bytes == null)
        { TempData["Error"] = _l["Org_Imp_Expired"].Value; return RedirectToAction(nameof(Index)); }

        OrganizationImportResult result;
        using (var s = new MemoryStream(bytes)) result = await _import.ImportAsync(s, commit: true);
        if (result.Committed) _cache.Remove(CacheKey(token));
        ViewBag.Token = null;
        ViewBag.Committed = result.Committed;
        ViewData["Title"] = "Organization Import";
        ViewData["Section"] = "Administration";
        return View("Result", result);
    }
}
