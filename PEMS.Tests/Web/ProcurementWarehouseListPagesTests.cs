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
        "Views/PurchaseOrder", "Views/Tender", "Views/MaterialRequisition", "Views/Vendor", "Views/Contract", "Views/ProcurementPackage",
        "Areas/Warehouse/Views/InventoryTransaction", "Areas/Warehouse/Views/StoreRequisition", "Areas/Warehouse/Views/IrnReceipt",
        "Areas/Warehouse/Views/Replenishment", "Areas/Warehouse/Views/MaterialItem", "Areas/Warehouse/Views/PartCatalogueRequest",
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
    public async Task PurchaseOrders_StatusIsALocalisedPill_AndNewOpensTheCreatePage()
    {
        var html = await Render<PurchaseOrderController>("/Views/PurchaseOrder/Index.cshtml",
            new List<PurchaseOrderDto> { new() { PoId = 3, PoNumber = "PO-0003", VendorName = "ACME", Value = 1200, Status = "Submitted" } });

        AssertBuiltFromTheComponent(html, "PurchaseOrder", 1);
        Assert.Contains("vs-pill vs-pill--warning", html);
        Assert.Contains("class=\"lp-subject\" title=\"PO-0003\"", html);
        Assert.Contains("lp-compose", html);
    }

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
        var mr = await Render<MaterialRequisitionController>("/Views/MaterialRequisition/Index.cshtml",
            Page(new MRDto { MRId = 1, MRNumber = "MR-1", Status = "Sent", QuotationsCount = 2 }));
        AssertBuiltFromTheComponent(mr, "MaterialRequisition", 1);
        Assert.Contains("class=\"lp-footer\"", mr);

        var vendors = await Render<VendorController>("/Views/Vendor/Index.cshtml",
            Page(new VendorDto { VendorId = 2, Name = "ACME", VendorCode = "V-2", OverdueVDRItemsCount = 1, RiskLevel = "High" }));
        AssertBuiltFromTheComponent(vendors, "Vendor", 1);
        Assert.Matches("<button[^>]*data-bs-target=\"#createVendorModal\"", vendors);
        Assert.Contains("id=\"createVendorModal\"", vendors);

        // Contract and Procurement Package keep their create modals (opened by New); the modals call the numbering view
        // component, so these two are checked in the view files here and in the app.
        foreach (var (view, modal) in new[] { ("Contract", "createContractModal"), ("ProcurementPackage", "createPackageModal") })
        {
            var text = File.ReadAllText(Path.Combine(RepoWeb(), "Views", view, "Index.cshtml"));
            Assert.Contains($"<pems-list-new modal=\"{modal}\"", text);
            Assert.Contains($"id=\"{modal}\"", text);
            Assert.Contains($"grid-key=\"{view}\"", text);
        }
    }

    // ---------- warehouse ----------

    [Fact]
    public async Task InventoryTransactions_FiltersMoveIntoTheDropdown_EachActiveOneIsAChip_AndPagingKeepsThem()
    {
        var html = await Render<InventoryTransactionController>("/Areas/Warehouse/Views/InventoryTransaction/Index.cshtml",
            Page(new InventoryTransactionRowDto { MaterialCode = "M-1", MaterialDescription = "Bearing", TransactionType = "Issue", WarehouseCode = "W1", Quantity = 2, TransactionDate = new DateTime(2026, 10, 1) }),
            v =>
            {
                v["Warehouses"] = new List<dynamic> { Obj(("WarehouseId", 1), ("Code", "W1")) };
                v["WarehouseId"] = 1; v["TransactionType"] = "Issue"; v["FromDate"] = new DateTime(2026, 10, 1); v["ToDate"] = null;
            });

        AssertBuiltFromTheComponent(html, "InventoryTransaction", 1);
        Assert.Matches("<span class=\"lp-badge\">3<", html);
        var chips = Chips(html);
        Assert.Equal(3, Regex.Matches(chips, "class=\"pems-filter-chip\"").Count);
        Assert.Contains("W1", chips);
        Assert.Matches("<option selected=\"selected\" value=\"Issue\">", html);
    }

    [Fact]
    public async Task StoreRequisitions_And_IncomingReceipts_FilterByStatus_AndKeepTheCreateModal()
    {
        var warehouses = new List<dynamic> { Obj(("WarehouseId", 1), ("Code", "W1")) };
        var sr = await Render<StoreRequisitionController>("/Areas/Warehouse/Views/StoreRequisition/Index.cshtml",
            Page(new StoreRequisitionListDto { StoreRequisitionId = 1, RequisitionNumber = "SR-1", Status = "PartiallyIssued", RequestedDate = new DateTime(2026, 10, 2) }),
            v => { v["Warehouses"] = warehouses; v["SelectedStatus"] = "PartiallyIssued"; });
        AssertBuiltFromTheComponent(sr, "StoreRequisition", 1);
        Assert.Contains("id=\"createRequisitionModal\"", sr);
        Assert.Single(Regex.Matches(Chips(sr), "class=\"pems-filter-chip\""));
        Assert.Contains("status=PartiallyIssued", sr);                                   // paging keeps the filter

        var irn = await Render<IrnReceiptController>("/Areas/Warehouse/Views/IrnReceipt/Index.cshtml",
            Page(new IrnReceiptListDto { IrnReceiptId = 2, IrnNumber = "IRN-2", Status = "PendingApproval", ReceivingDate = new DateTime(2026, 10, 3), LineCount = 4 }),
            v => { v["Warehouses"] = warehouses; v["Vendors"] = new List<dynamic>(); v["SelectedStatus"] = null; });
        AssertBuiltFromTheComponent(irn, "IrnReceipt", 1);
        Assert.Contains("id=\"createIrnModal\"", irn);
        Assert.Contains("vs-pill vs-pill--info", irn);
        Assert.DoesNotContain("pems-filter-chip", irn);
    }

    [Fact]
    public async Task Replenishment_RunIsAToolOnlyForWhoMayRun_AndTheNotesStay()
    {
        Task<string> Run(bool canRun) => Render<ReplenishmentController>("/Areas/Warehouse/Views/Replenishment/Index.cshtml",
            Page(new ReplenishmentListItem { Id = 1, MaterialCode = "M-1", Status = "Open", IsCurrentlyDefinitive = false }),
            v =>
            {
                v["LastRun"] = null; v["CanRun"] = canRun; v["AttributionEntries"] = new List<string> { "WO-1" };
                v["SelectedStatus"] = "Open"; v["Search"] = "bear"; v["DefinitiveOnly"] = false; v["RequestKey"] = "k";
            });

        var html = await Run(true);
        AssertBuiltFromTheComponent(html, "Replenishment", 1);
        Assert.Contains("bi-calculator", html);
        Assert.Contains("alert-info", html);                                              // stock-only note
        Assert.Contains("alert-warning", html);                                           // attribution note
        Assert.Equal(2, Regex.Matches(Chips(html), "class=\"pems-filter-chip\"").Count);  // search + status
        Assert.DoesNotContain("bi-calculator", await Run(false));
    }

    [Fact]
    public async Task MaterialItems_CountCardsAreKpis_AndPagingKeepsLowStockAndCritical()
    {
        var page = new PagedResult<MaterialItemListDto>
        {
            Items = new List<MaterialItemListDto> { new() { MaterialItemId = 1, MaterialCode = "M-1", Description = "Bearing", IsLowStock = true, IsCritical = true } },
            Page = 1, PageSize = 25, TotalCount = 60
        };
        var html = await Render<MaterialItemController>("/Areas/Warehouse/Views/MaterialItem/Index.cshtml", page, v =>
        {
            v["Categories"] = new List<dynamic> { Obj(("MaterialCategoryId", 2), ("Name", "Bearings")) };
            v["Disciplines"] = new List<dynamic>(); v["Uoms"] = new List<dynamic>(); v["Vendors"] = new List<dynamic>();
            v["SearchTerm"] = null; v["CategoryId"] = 2; v["LowStockOnly"] = true; v["CriticalOnly"] = true;
        });

        AssertBuiltFromTheComponent(html, "MaterialItem", 1);
        Assert.Contains("id=\"miKpis\"", html);
        Assert.Equal(3, Regex.Matches(Chips(html), "class=\"pems-filter-chip\"").Count);
        Assert.Contains("lowStockOnly=true", html);
        Assert.Contains("criticalOnly=true", html);
        Assert.Contains("id=\"createMaterialModal\"", html);
        Assert.DoesNotContain("class=\"pagination pagination-sm justify-content-center\"", html);   // the old pager that lost the filters
    }

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
