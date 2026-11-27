using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Web.Areas.Warehouse.Models;
using PEMS.Web.Helpers;
using PEMS.Web.Resources;

namespace PEMS.Web.Areas.Warehouse.Controllers;

/// <summary>
/// درخواست کاتالوگ قطعه (Add/Change) - زیر ماژول انبار. Controller نازک است: هویت (EmployeeId + نقش‌ها) را می‌سازد و
/// به IPartCatalogueService می‌دهد؛ مجوز، مالکیت، گیرنده‌ی گام و همه‌ی قواعد در سرویس اعمال می‌شود (نه این‌جا).
/// </summary>
[Area("Warehouse")]
public class PartCatalogueRequestController : Controller
{
    private readonly IPartCatalogueService _service;
    private readonly IPartCatalogueLookupService _lookups;
    private readonly IRoleModuleActionService _actions;
    private readonly ICurrentUserContext _user;
    private readonly IStringLocalizer<SharedResource> _l;

    public PartCatalogueRequestController(IPartCatalogueService service, IPartCatalogueLookupService lookups,
        IRoleModuleActionService actions, ICurrentUserContext user, IStringLocalizer<SharedResource> l)
    {
        _service = service;
        _lookups = lookups;
        _actions = actions;
        _user = user;
        _l = l;
    }

    private PcActor? Actor => _user.EmployeeId is int emp ? new PcActor(emp, _user.UserId, _user.Roles) : null;

    private IActionResult NoEmployee()
    {
        TempData["Error"] = _l["Pc_Err_NoEmployee"].Value;
        return RedirectToAction("Index", "Home", new { area = "" });
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id, int attachmentId)
    {
        var actor = Actor;
        if (actor == null) return NoEmployee();
        var file = await _service.OpenAttachmentAsync(id, attachmentId, actor);
        if (file == null) return NotFound();
        return File(file.Value.Stream, file.Value.ContentType, file.Value.FileName);
    }
}
