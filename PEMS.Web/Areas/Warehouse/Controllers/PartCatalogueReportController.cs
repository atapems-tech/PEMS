using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Web.Areas.Warehouse.Models;
using PEMS.Web.Helpers;
using PEMS.Web.Resources;

namespace PEMS.Web.Areas.Warehouse.Controllers;

/// <summary>
/// SCRUM-478: Part Catalogue reports (who did what, equipment coverage, stages) and their Excel workbook.
/// Thin: it builds the actor and checks the module permission; the rows and their visibility come from IPartCatalogueReportService.
/// </summary>
[Area("Warehouse")]
public class PartCatalogueReportController : Controller
{
    public static readonly string[] Tabs = { "summary", "people", "activity", "equipment", "parts", "stages" };

    private readonly IPartCatalogueReportService _reports;
    private readonly IPartCatalogueLookupService _lookups;
    private readonly IRoleModuleActionService _actions;
    private readonly ICurrentUserContext _user;
    private readonly IStringLocalizer<SharedResource> _l;

    public PartCatalogueReportController(IPartCatalogueReportService reports, IPartCatalogueLookupService lookups,
        IRoleModuleActionService actions, ICurrentUserContext user, IStringLocalizer<SharedResource> l)
    {
        _reports = reports;
        _lookups = lookups;
        _actions = actions;
        _user = user;
        _l = l;
    }

    // Reports only read. A user without an employee record (e.g. a system admin) still sees what the permission allows:
    // with Admin/Review/Approve every request, otherwise nothing (employee 0 owns and approves no request).
    private PcActor Actor => new(_user.EmployeeId ?? 0, _user.UserId, _user.Roles);

    /// <summary>Owner decision 2026-10-31: everyone who may view the Part Catalogue may see its reports.</summary>
    private async Task<bool> CanViewAsync()
    {
        foreach (var a in new[] { "View", "Review", "Approve", "Admin" })
            if (await _actions.HasActionAsync(_user.Roles, "PartCatalogue", a)) return true;
        return false;
    }

    private static PartCatalogueReportFilter Normalize(PartCatalogueReportFilter? f)
    {
        f ??= new PartCatalogueReportFilter();
        if (!PartCatalogueReportPeriods.Values.Contains(f.Period)) f.Period = PartCatalogueReportPeriods.ThisYear;
        f.Tab = Tabs.Contains(f.Tab) ? f.Tab : "summary";
        return f;
    }

    public async Task<IActionResult> Index([FromQuery] PartCatalogueReportFilter filter)
    {
        var actor = Actor;
        if (!await CanViewAsync()) return Forbid();

        ViewData["Title"] = "Part Catalogue Reports";
        ViewData["Section"] = "Warehouse";
        var vm = new PcReportViewModel
        {
            Report = await _reports.GetReportAsync(Normalize(filter), actor),
            Sections = await _lookups.GetSectionsAsync(),
            IsPersian = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa"
        };
        return View(vm);
    }

    /// <summary>The whole workbook, or one tab's sheet (<paramref name="sheet"/> = people | activity | equipment | parts | stages).</summary>
    [HttpGet]
    public async Task<IActionResult> ExportXlsx([FromQuery] PartCatalogueReportFilter filter, string? sheet)
    {
        var actor = Actor;
        if (!await CanViewAsync()) return Forbid();

        var report = await _reports.GetReportAsync(Normalize(filter), actor);
        var rtl = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa";
        var bytes = PartCatalogueReportExport.Build(report, new PartCatalogueReportTexts(_l, rtl), sheet);
        var name = $"PartCatalogueReport-{(string.IsNullOrEmpty(sheet) ? "all" : sheet)}-{DateTime.Now:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }
}
