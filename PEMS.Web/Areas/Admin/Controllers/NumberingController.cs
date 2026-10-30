using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PEMS.Web.Resources;
using PEMS.Application.Interfaces;
using PEMS.Application.Numbering;
using PEMS.Persistence.Identity;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// SCRUM-40: Admin > Numbering. The system administrator sets the numbering pattern of each procurement document type;
/// validation, versioning and the preview live in <see cref="INumberingSchemeService"/>.
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class NumberingController : Controller
{
    private readonly INumberingSchemeService _service;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public NumberingController(INumberingSchemeService service, IStringLocalizer<SharedResource> localizer)
    {
        _service = service;
        _localizer = localizer;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Num_Title";
        ViewData["Section"] = "Administration";
        return View(await _service.GetAllAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(NumberingSchemeInput input)
    {
        var error = await _service.SaveAsync(input);
        if (error != null) TempData["Error"] = error;
        else TempData["Message"] = "Num_Saved";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Live preview of an unsaved pattern (sample codes for {PROJECT}/{DISC}); consumes no number.</summary>
    [HttpGet]
    public IActionResult Preview(string documentType, string pattern, int padWidth, string calendar, string resetPeriod)
    {
        var (number, error) = _service.Preview(new NumberingSchemeInput(documentType, pattern, padWidth, calendar, resetPeriod, true, false, null),
            new NumberingContext("PRJ", "MEC"));
        return Json(new { number, error = error == null ? null : _localizer[error].Value });
    }
}
