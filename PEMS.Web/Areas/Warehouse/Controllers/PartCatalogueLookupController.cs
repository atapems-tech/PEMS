using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Application.PartCatalogue;

namespace PEMS.Web.Areas.Warehouse.Controllers;

/// <summary>
/// Endpointهای JSON جستجوی سمت سرور برای فرم (Technical Class، Manufacturer، Supplier، Equipment، Commodity Group).
/// فقط GET، با سقف نتیجه‌ی سرویس؛ همه نیازمند یکی از مجوزهای PartCatalogue (View/Create/Review/Approve/Admin) هستند.
/// </summary>
[Area("Warehouse")]
public class PartCatalogueLookupController : Controller
{
    private readonly IPartCatalogueLookupService _lookups;
    private readonly IRoleModuleActionService _actions;
    private readonly ICurrentUserContext _user;

    public PartCatalogueLookupController(IPartCatalogueLookupService lookups, IRoleModuleActionService actions, ICurrentUserContext user)
    {
        _lookups = lookups;
        _actions = actions;
        _user = user;
    }

    private async Task<bool> AllowedAsync()
    {
        foreach (var a in new[] { "Create", "View", "Review", "Approve", "Admin" })
            if (await _actions.HasActionAsync(_user.Roles, "PartCatalogue", a)) return true;
        return false;
    }

    private async Task<IActionResult> Run<T>(Func<Task<T>> query)
    {
        if (!await AllowedAsync()) return Forbid();
        Response.Headers.CacheControl = "no-store";
        return Json(await query());
    }

    [HttpGet] public Task<IActionResult> TechnicalClasses(string? q) => Run(() => _lookups.SearchTechnicalClassesAsync(q));
    [HttpGet] public Task<IActionResult> TechnicalClass(int id) => Run(() => _lookups.GetTechnicalClassAsync(id));
    /// <summary>پیشنهاد Technical Class از متن FREE FORMAT TEXT (فقط خواندنی؛ مجوز همان Lookupها). متن بیش از حد مجاز کوتاه می‌شود.</summary>
    [HttpGet] public Task<IActionResult> ResolveTechnicalClass(string? text) => Run(() => _lookups.ResolveTechnicalClassAsync(
        text != null && text.Length > TechnicalClassMatcher.MaxTextLength ? text[..TechnicalClassMatcher.MaxTextLength] : text));
    [HttpGet] public Task<IActionResult> Manufacturers(string? q) => Run(() => _lookups.SearchManufacturersAsync(q));
    [HttpGet] public Task<IActionResult> Suppliers(string? q) => Run(() => _lookups.SearchSuppliersAsync(q));
    [HttpGet] public Task<IActionResult> Equipment(string? q) => Run(() => _lookups.SearchEquipmentAsync(q));
    [HttpGet] public Task<IActionResult> Group1(int? sectionId) => Run(() => _lookups.GetCommodityGroup1Async(sectionId));
    [HttpGet] public Task<IActionResult> Group2(int? group1Id) => Run(() => _lookups.GetCommodityGroup2Async(group1Id));

    [HttpGet]
    public async Task<IActionResult> CheckManufacturer(string? name)
    {
        if (!await AllowedAsync()) return Forbid();
        var (exact, similar) = await _lookups.CheckManufacturerNameAsync(name ?? string.Empty);
        return Json(new { exact, similar });
    }
}
