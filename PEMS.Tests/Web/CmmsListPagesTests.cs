using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;
using PEMS.Web.ViewModels;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-337 wave 2 (CMMS): Work Orders, Work Requests, Equipment, Functional Locations, Instruments, Letter Tags,
/// Maintenance Plans, Maintenance Strategies, Job Plans, Spare Parts, Permits, Inspections, Findings and Assets are built from
/// the PEMS.UI list-page component. Each page keeps its own filters, columns, modals, actions and permissions; the old
/// hand-written frame (content cards, count cards, status button rows, inline column-resize script) is gone.
/// Wave 2b adds Criticality Assessments, Equipment Criticality, Mechanical Equipment, the Failure Mode Catalogue, Shift
/// Handovers, RCM / FMEA coverage and the Equipment / Functional Location assignment batches.
/// </summary>
public class CmmsListPagesTests
{
    private static readonly string[] Pages =
    {
        "WorkOrder",
        "MaintenanceStrategy",
        // wave 2b
    };

    private static Task<string> Render<TController>(string view, object model, Action<ViewDataDictionary> data) where TController : Controller
    {
        // rendering only needs the controller's ViewData; its constructor (services) is not used
        var controller = (Controller)RuntimeHelpers.GetUninitializedObject(typeof(TController));
        data(controller.ViewData);
        var result = new ViewResult { ViewData = new ViewDataDictionary(controller.ViewData) { Model = model } };
        return RazorViewRenderer.RenderAsync(controller, result, view);
    }

    private static PagedResult<T> Page<T>(params T[] items) => new() { Items = items.ToList(), Page = 1, PageSize = 25, TotalCount = items.Length };

    // the controllers pass anonymous objects read through dynamic; ExpandoObject behaves the same across assemblies
    private static dynamic Obj(params (string Key, object? Value)[] values)
    {
        IDictionary<string, object?> o = new ExpandoObject();
        foreach (var (k, v) in values) o[k] = v;
        return o;
    }

    /// <summary>The shared frame. <paramref name="gridKey"/> null = a short list without stored column widths or paging.</summary>
    private static void AssertBuiltFromTheComponent(string html, string? gridKey, int rows, bool keepsDataBadges = false)
    {
        Assert.Contains("class=\"lp-page\"", html);
        Assert.Contains("class=\"lp-toolbar\"", html);
        Assert.Equal(rows, Regex.Matches(html, "<tr class=\"lp-row[ \"]").Count);
        if (gridKey != null)
        {
            Assert.Matches($"<table class=\"[^\"]*lp-table[^\"]*\" data-grid-key=\"{gridKey}\"", html);
            Assert.Contains($"data-lp-reset-columns=\"{gridKey}\"", html);
            Assert.Contains("class=\"lp-footer\"", html);
        }
        // the old hand-written frame is gone
        Assert.DoesNotContain("content-card", html);
        Assert.DoesNotContain("PemsDataGrid.initColumnResize", html);
        Assert.DoesNotContain("btn-group btn-group-sm flex-wrap", html);       // the old status button row (paging keeps its own group)
        // status badges became tone pills; a badge stays only where its colour is data (tag colour, RCM criticality helpers)
        if (!keepsDataBadges) Assert.DoesNotContain("class=\"badge ", html);
    }

    private static string Chips(string html) => Regex.Match(html, "<div class=\"lp-chips\">(.*?)</div>", RegexOptions.Singleline).Value;
    private static string Kpis(string html, string id) => Regex.Match(html, $"id=\"{id}\" hidden(?:=\"hidden\")?>(.*?)</div>", RegexOptions.Singleline).Groups[1].Value;

    // ---------- Work orders ----------

    private static Task<string> WorkOrders(string? status = null, int? equipmentId = null, bool mine = false, params WorkOrderListDto[] items)
        => Render<WorkOrderController>("/Views/WorkOrder/Index.cshtml", Page(items), v =>
        {
            v["SelectedStatus"] = status;
            v["SelectedEquipmentId"] = equipmentId;
            v["SelectedEquipmentLabel"] = equipmentId.HasValue ? "P-101A" : null;
            v["AssignedToMe"] = mine;
            v["OpenCount"] = 3; v["InProgressCount"] = 2; v["CompletedCount"] = 9; v["CriticalOpenCount"] = 1;
        });

    [Fact]
    public async Task WorkOrders_CountsAreKpiLinks_StatusesAreLocalisedPills_AndTheEquipmentSearchFeedsTheFilter()
    {
        var html = await WorkOrders(items: new WorkOrderListDto { WorkOrderId = 4, WorkOrderNumber = "WO-0004", EquipmentTagNumber = "P-101A", WorkOrderType = "CM", Priority = "Critical", Status = "InProgress", CreatedDate = new DateTime(2026, 10, 5) });

        AssertBuiltFromTheComponent(html, "WorkOrder", 1);
        var kpis = Kpis(html, "woKpis");
        Assert.Equal(4, Regex.Matches(kpis, "class=\"vs-chip").Count);
        Assert.Equal(3, Regex.Matches(kpis, "<a ").Count);                     // open / in progress / completed stay filter links
        Assert.Equal(2, Regex.Matches(kpis, "vs-chip--danger").Count);           // open and critical-open above zero
        Assert.Contains(">In progress<", html.Replace("> ", ">"));               // Wo_St_InProgress, not the stored value
        Assert.Contains("vs-pill vs-pill--danger", html);                        // critical priority
        Assert.Contains("data-search-type=\"Equipment\"", html);
        Assert.Contains("id=\"woEquipmentFilterHidden\"", html);
        Assert.Contains("lp-compose", html);
    }

    [Fact]
    public async Task WorkOrders_ActiveFilters_ShowTheBadgeAndOneChipEach()
    {
        var html = await WorkOrders("Open", 7, mine: true);

        Assert.Matches("<span class=\"lp-badge\">3<", html);
        var chips = Chips(html);
        Assert.Equal(3, Regex.Matches(chips, "class=\"pems-filter-chip\"").Count);
        Assert.Contains("P-101A", chips);
        Assert.Matches("id=\"assignedToMeCheck\"[^>]*checked=\"checked\"", html);
        Assert.Contains("class=\"lp-empty\"", html);
    }

    // ---------- Work requests ----------

    // ---------- Letter tags: the page is a definition since SCRUM-754 (tags, tag-create, tag-delete) ----------

    // ---------- Inspections ----------

    // ---------- Findings ----------

    // ---------- Maintenance plans ----------

    // ---------- Job plans ----------

    // ---------- Permits ----------

    // ---------- Functional locations ----------

    // ---------- Instruments ----------

    // ---------- Maintenance strategies ----------

    private static Task<string> Strategies(bool canCreate, bool canApprove)
        => Render<MaintenanceStrategyController>("/Views/MaintenanceStrategy/Index.cshtml", new StrategyIndexViewModel
        {
            Page = Page(new MaintenanceStrategyListDto { MaintenanceStrategyId = 1, VersionNumber = 2, Status = "Active", EquipmentClassName = "Centrifugal pump", EquipmentTagNumber = "", StrategyType = "TimeBased", ConsequenceCategory = "Operational", Frequency = "Monthly" }),
            Filter = new StrategyListFilter { EquipmentClassId = 4 },
            Classes = new() { new LookupItem { Id = 4, Text = "Centrifugal pump" } },
            CanCreate = canCreate, CanApprove = canApprove, CanEdit = true,
        }, _ => { });

    [Fact]
    public async Task MaintenanceStrategies_KeepTheirPermissions_AndMoveTheExtraLinksIntoTheMoreMenu()
    {
        var full = await Strategies(true, true);
        AssertBuiltFromTheComponent(full, "MaintenanceStrategy", 1, keepsDataBadges: true);
        var menu = Regex.Match(full, "<ul[^>]*lp-menu[^>]*>(.*?)</ul>", RegexOptions.Singleline).Value;
        Assert.Contains("bi-pie-chart", menu);                                  // coverage
        Assert.Contains("bi-journal-text", menu);                               // failure-mode catalogue
        Assert.Contains("bi-collection", menu);                                 // bulk assign (approvers only)
        Assert.Contains("lp-compose", full);
        Assert.Contains("Centrifugal pump", Chips(full));

        var readOnly = await Strategies(false, false);
        Assert.DoesNotContain("bi-collection", readOnly);
        Assert.DoesNotContain("lp-compose", readOnly);
    }

    // ---------- Spare parts ----------

    // ---------- Assets ----------

    // ================= wave 2b: the remaining CMMS lists =================

    // ---------- Mechanical equipment ----------

    // ---------- Shift handovers ----------

    // ---------- Production orders and the procurement cartable (SCRUM-337) ----------

    [Fact]
    public async Task ReferredLetters_AreAListPage()
    {
        var html = await Render<LetterController>("/Views/Letter/MyReferrals.cshtml", new List<LetterDto>
        {
            new() { LetterId = 5, LetterType = "Incoming", LetterNumber = "L-005", Subject = "Pump quote", LetterDate = new DateTime(2026, 10, 9), Status = "Draft" },
        }, _ => { });
        AssertBuiltFromTheComponent(html, null, 1, keepsDataBadges: true);
        Assert.Contains("L-005", html);
    }

    [Fact]
    public async Task LetterTemplates_AreAListPage_AndKeepTheirToggleForm()
    {
        var html = await Render<LetterTemplateController>("/Views/LetterTemplate/Index.cshtml", new List<LetterTemplateDto>
        {
            new() { LetterTemplateId = 1, Name = "Ack", Subject = "S1", Priority = "Normal", ConfidentialityLevel = "Internal", IsActive = true },
        }, _ => { });
        AssertBuiltFromTheComponent(html, null, 1, keepsDataBadges: true);
        Assert.Contains("name=\"active\" value=\"false\"", html);
    }

    [Fact]
    public async Task Tasks_AreAListPage_WithTheirTabFilterAndTheNewTaskForm()
    {
        var html = await Render<TaskController>("/Views/Task/Index.cshtml", Page(
            new TaskItemDto { TaskItemId = 9, Title = "Call vendor", Priority = "Normal", Status = "Open", IsOverdue = true, DueDate = new DateTime(2026, 10, 20) }),
            v => { v["Tab"] = "action"; v["CanEdit"] = true; v["CanCreate"] = true; v["Employees"] = new List<EligibleAssigneeDto> { new() { EmployeeId = 4, Name = "A. Karimi" } }; });

        AssertBuiltFromTheComponent(html, "Task", 1, keepsDataBadges: true);
        Assert.Contains("Call vendor", html);
        Assert.Contains("name=\"tab\"", html);
        Assert.Contains("name=\"assignedToEmployeeId\"", html);   // the new-task form moved into the toolbar popover
        Assert.Contains("name=\"taskItemId\" value=\"9\"", html);   // status change form kept
        Assert.DoesNotContain("pagination-sm", html);
    }

    // ---------- Equipment <-> functional location assignment ----------

    [Fact]
    public void EveryWave2Page_UsesTheComponentAndItsAssets()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        foreach (var page in Pages)
        {
            var view = File.ReadAllText(Path.Combine(dir!.FullName, "PEMS.Web", "Views", page, "Index.cshtml"));
            Assert.True(view.Contains("<pems-list-page texts="), page);
            Assert.True(view.Contains("~/_content/PEMS.UI/pems-list.css"), page);
            Assert.True(view.Contains("~/_content/PEMS.UI/pems-list.js"), page);
            Assert.False(view.Contains("initColumnResize"), page);
            Assert.False(view.Contains("_PageHeader"), page);           // the toolbar heading is the page title
        }
    }
}
