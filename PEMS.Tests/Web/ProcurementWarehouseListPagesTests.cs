using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PEMS.Application.DTOs;
using PEMS.Application.Replenishment;
using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;
using PEMS.Web.Areas.Warehouse.Controllers;
using PEMS.Web.Areas.Warehouse.Models;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-337 wave 3 (procurement and warehouse): Purchase Orders, Tenders, Material Requisitions, Vendors, Contracts,
/// Procurement Packages, Inventory Transactions, Store Requisitions, Incoming Receipts, Replenishment, the Material Master
/// and Part Catalogue Requests are built from the PEMS.UI list-page component. Each page keeps its columns, filters,
/// actions and modals; the old hand-written frame (content cards, filter rows, offcanvas, inline column script) is gone.
/// </summary>
public class ProcurementWarehouseListPagesTests
{
    private static readonly string[] Pages =
    {
        "Views/Tender", "Views/Contract",
        "Areas/Warehouse/Views/PartCatalogueRequest",
    };

    private static Task<string> Render<TController>(string view, object model, Action<ViewDataDictionary>? data = null) where TController : Controller
    {
        var controller = (Controller)RuntimeHelpers.GetUninitializedObject(typeof(TController));
        data?.Invoke(controller.ViewData);
        var result = new ViewResult { ViewData = new ViewDataDictionary(controller.ViewData) { Model = model } };
        return RazorViewRenderer.RenderAsync(controller, result, view);
    }

    private static PagedResult<T> Page<T>(params T[] items) => new() { Items = items.ToList(), Page = 1, PageSize = 25, TotalCount = items.Length };

    private static dynamic Obj(params (string Key, object? Value)[] values)
    {
        IDictionary<string, object?> o = new ExpandoObject();
        foreach (var (k, v) in values) o[k] = v;
        return o;
    }

    private static void AssertBuiltFromTheComponent(string html, string gridKey, int rows)
    {
        Assert.Contains("class=\"lp-page\"", html);
        Assert.Contains("class=\"lp-toolbar\"", html);
        Assert.Equal(rows, Regex.Matches(html, "<tr class=\"lp-row").Count);
        Assert.Matches($"<table class=\"[^\"]*lp-table[^\"]*\" data-grid-key=\"{gridKey}\"", html);
        Assert.Contains($"data-lp-reset-columns=\"{gridKey}\"", html);
        Assert.DoesNotContain("content-card", html);
        Assert.DoesNotContain("PemsDataGrid.initColumnResize", html);
        Assert.DoesNotContain("class=\"badge ", html);                                    // statuses are tone pills
    }

    private static string Chips(string html) => Regex.Match(html, "<div class=\"lp-chips\">(.*?)</div>", RegexOptions.Singleline).Value;

    // ---------- procurement ----------

    [Fact]
    public async Task Tenders_FromAPackage_ShowThePackageChip_AndOfferNewForThatPackage()
    {
        var tender = new Tender { TenderId = 5, TenderNumber = "TND-5", Title = "Pumps", Status = "Open", ProcurementPackage = new ProcurementPackage { PackageNumber = "PKG-9" } };
        var filtered = await Render<TenderController>("/Views/Tender/Index.cshtml", new List<Tender> { tender }, v => v["ProcurementPackageId"] = 9);
        var all = await Render<TenderController>("/Views/Tender/Index.cshtml", new List<Tender> { tender }, v => v["ProcurementPackageId"] = null);

        AssertBuiltFromTheComponent(filtered, "Tender", 1);
        Assert.Contains("PKG-9", Chips(filtered));
        Assert.Contains("lp-compose", filtered);                                          // New starts a tender in this package
        Assert.DoesNotContain("lp-compose", all);                                         // without a package there is no New
        Assert.Contains("vs-pill vs-pill--info", all);
    }

    [Fact]
    public async Task MaterialRequisitions_Vendors_Contracts_AndPackages_KeepTheirModalsAndPaging()
    {
        // Contract and Procurement Package keep their create modals (opened by New); the modals call the numbering view
        // component, so these two are checked in the view files here and in the app.
        foreach (var (view, modal) in new[] { ("Contract", "createContractModal") })
        {
            var text = File.ReadAllText(Path.Combine(RepoWeb(), "Views", view, "Index.cshtml"));
            Assert.Contains($"<pems-list-new modal=\"{modal}\"", text);
            Assert.Contains($"id=\"{modal}\"", text);
            Assert.Contains($"grid-key=\"{view}\"", text);
        }
    }

    // ---------- warehouse ----------

    [Fact]
    public async Task PartCatalogueRequests_ViewAndSidePanelFiltersAreInTheDropdown_AndTheCountsShowFirst()
    {
        var vm = new PcIndexViewModel
        {
            Page = Page(new PartCatalogueListItemDto { Id = 1, RequestNumber = "PCR-1", RequestType = "Add", Status = "InApproval", Urgent = true, CreatedDate = new DateTime(2026, 10, 4) }),
            Filter = new PartCatalogueListFilter { View = "awaiting", Status = "InApproval" },
            Summary = new PartCatalogueSummaryDto { Total = 1, InApproval = 1, Urgent = 1, AwaitingMe = 1 },
            CanCreate = true
        };
        var html = await Render<PartCatalogueRequestController>("/Areas/Warehouse/Views/PartCatalogueRequest/Index.cshtml", vm);

        AssertBuiltFromTheComponent(html, "PartCatalogueRequests", 1);
        Assert.DoesNotContain("offcanvas", html);
        Assert.DoesNotContain("nav-pills", html);
        Assert.Contains("id=\"pcKpis\"", html);
        Assert.DoesNotMatch("id=\"pcKpis\" hidden", html);                                 // shown first
        Assert.Equal(2, Regex.Matches(Chips(html), "class=\"pems-filter-chip\"").Count);  // view + status
        Assert.Contains("vs-subnav", html);
        Assert.Contains("pc-urgent-row", html);
    }

    private static string RepoWeb()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.Web", "PEMS.Web.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "PEMS.Web");
    }

    [Fact]
    public void EveryWave3Page_UsesTheComponentAndItsAssets()
    {
        foreach (var page in Pages)
        {
            var view = File.ReadAllText(Path.Combine(RepoWeb(), page.Replace('/', Path.DirectorySeparatorChar), "Index.cshtml"));
            Assert.Contains("<pems-list-page", view);
            Assert.Contains("_content/PEMS.UI/pems-list.css", view);
            Assert.Contains("_content/PEMS.UI/pems-list.js", view);
            Assert.DoesNotContain("PemsDataGrid.initColumnResize", view);
            Assert.DoesNotContain("class=\"alert alert-", view);
        }
    }
}
