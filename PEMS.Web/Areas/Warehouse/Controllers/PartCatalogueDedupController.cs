using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Areas.Warehouse.Controllers;

/// <summary>
/// جستجوی قطعه‌ی موجود و کاندیدهای تکراری MPN (JSON، فقط GET و فقط‌خواندنی؛ بدون هیچ تغییر در Schema). همان مجوزهای Lookup ماژول
/// PartCatalogue (View/Create/Review/Approve/Admin) لازم است؛ همه‌ی قواعد در IPartCatalogueDuplicateService اعمال می‌شود.
/// </summary>
[Area("Warehouse")]
public class PartCatalogueDedupController : Controller
{
    private readonly IPartCatalogueDuplicateService _duplicates;
    private readonly IRoleModuleActionService _actions;
    private readonly ICurrentUserContext _user;

    public PartCatalogueDedupController(IPartCatalogueDuplicateService duplicates, IRoleModuleActionService actions, ICurrentUserContext user)
    {
        _duplicates = duplicates;
        _actions = actions;
        _user = user;
    }

    private async Task<bool> AllowedAsync()
    {
        foreach (var a in new[] { "Create", "View", "Review", "Approve", "Admin" })
            if (await _actions.HasActionAsync(_user.Roles, "PartCatalogue", a)) return true;
        return false;
    }

    [HttpGet]
    public async Task<IActionResult> ExistingParts(string? q)
    {
        if (!await AllowedAsync()) return Forbid();
        Response.Headers.CacheControl = "no-store";
        return Json(await _duplicates.SearchExistingPartsAsync(q));
    }

    [HttpGet]
    public async Task<IActionResult> DuplicateCandidates(string? manufacturerPartNumber, string? supplierPartNumber,
        int? manufacturerId, string? newManufacturerName, int? excludeRequestId)
    {
        if (!await AllowedAsync()) return Forbid();
        Response.Headers.CacheControl = "no-store";
        return Json(await _duplicates.FindDuplicateCandidatesAsync(new PcDuplicateProbe
        {
            ManufacturerPartNumber = manufacturerPartNumber, SupplierPartNumber = supplierPartNumber,
            ManufacturerId = manufacturerId, NewManufacturerName = newManufacturerName, ExcludeRequestId = excludeRequestId
        }));
    }
}
