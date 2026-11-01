using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Tests.TestSupport;
using PEMS.Web.Areas.Admin.Controllers;
using PEMS.Web.Areas.Admin.Models;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-337 wave 4b (Administration settings lists): SoD policies, storage providers, storage routes, workflow
/// definitions, help articles and the PO authority table are built from the PEMS.UI list-page component. The inline
/// editors (contract approval authority, numbering, routing rules) wait for the RelatedList / DataTable row-form mode.
/// </summary>
public class AdminSettingsListPagesTests
{
    private static readonly string[] Pages =
        { "SoDPolicy", "StorageProviders", "StorageRouting", "WorkflowManagement", "Help", "PurchaseOrderAuthority" };

    private static Task<string> Render<TController>(string view, object? model, Action<ViewDataDictionary>? data = null) where TController : Controller
    {
        var controller = (Controller)RuntimeHelpers.GetUninitializedObject(typeof(TController));
        data?.Invoke(controller.ViewData);
        var result = new ViewResult { ViewData = new ViewDataDictionary(controller.ViewData) { Model = model } };
        return RazorViewRenderer.RenderAsync(controller, result, view);
    }

    private static void AssertBuiltFromTheComponent(string html, int rows)
    {
        Assert.Contains("class=\"lp-page\"", html);
        Assert.Contains("class=\"lp-toolbar\"", html);
        Assert.Equal(rows, Regex.Matches(html, "<tr class=\"lp-row").Count);
        Assert.DoesNotContain("content-card", html);
        Assert.DoesNotContain("admin-hero", html);
        Assert.DoesNotContain("class=\"badge ", html);
    }

    [Fact]
    public async Task StorageProviders_CountCardsAreKpis_HealthIsAPill_AndDeleteOnlyWhenUnused()
    {
        var html = await Render<StorageProvidersController>("/Areas/Admin/Views/StorageProviders/Index.cshtml", new List<StorageProviderDto>
        {
            new() { StorageProviderId = 1, Name = "Main", Code = "MAIN", ProviderType = "Local", RootPath = "D:/files", IsDefault = true, IsEnabled = true, LastHealthStatus = "Online", ActiveRouteCount = 2 },
            new() { StorageProviderId = 2, Name = "Backup", Code = "BK", ProviderType = "Sftp", Host = "h", Port = 22, RootPath = "/", LastHealthStatus = "AuthenticationFailed" },
        });

        AssertBuiltFromTheComponent(html, 2);
        Assert.Contains("id=\"stpKpis\"", html);
        Assert.Equal(6, Regex.Matches(Regex.Match(html, "id=\"stpKpis\"[^>]*>(.*?)</div>", RegexOptions.Singleline).Value, "class=\"vs-chip").Count);
        Assert.Contains("vs-pill vs-pill--danger", html);                                // authentication failed
        Assert.Single(Regex.Matches(html, "bi-trash"));                                  // only the unused, non-default one
        Assert.Single(Regex.Matches(html, "bi-star\""));                                 // set default only for the other one
    }

    [Fact]
    public async Task PoAuthority_TheRuleFormOpensFromTheToolbar_AndRulesToggleInTheRow()
    {
        var html = await Render<PurchaseOrderAuthorityController>("/Areas/Admin/Views/PurchaseOrderAuthority/Index.cshtml",
            new List<PoAuthorityRuleView> { new(1, 1, 0, 1000, "IRR", "PRC.Manager", "up to 1000", true) },
            v => v["Roles"] = new[] { "PRC.Manager", "SystemAdmin" });

        AssertBuiltFromTheComponent(html, 1);
        Assert.Contains("id=\"poRuleNewBtn\"", html);
        var panel = html[html.IndexOf("id=\"poRuleNew\"", StringComparison.Ordinal)..];
        Assert.Contains("name=\"approverRole\"", panel);
        Assert.Contains("name=\"minAmount\"", panel);
        Assert.Contains("bi-toggle-on", html);
    }

    [Fact]
    public async Task SoD_Routes_Workflows_AndHelp_UseTheListFrame()
    {
        var sod = await Render<SoDPolicyController>("/Areas/Admin/Views/SoDPolicy/Index.cshtml",
            new List<SoDPolicyListItemVm> { new() { Id = 1, PermissionA = "PO.Create", PermissionB = "PO.Approve", IsActive = true } });
        AssertBuiltFromTheComponent(sod, 1);
        Assert.Contains("lp-compose", sod);

        var routes = await Render<StorageRoutingController>("/Areas/Admin/Views/StorageRouting/Index.cshtml",
            new List<StorageRouteDto> { new() { StorageRouteId = 1, Module = "VDR", StorageProviderName = "Main", IsEnabled = true } });
        AssertBuiltFromTheComponent(routes, 1);
        Assert.Contains("data-confirm=\"Delete this storage route?\"", routes);

        var help = await Render<HelpController>("/Areas/Admin/Views/Help/Index.cshtml", null, v =>
        {
            v["Articles"] = new List<HelpArticleSummaryDto> { new() { HelpArticleId = 1, Code = "H-1", Title = "Start", IsPublished = false } };
            v["Categories"] = new List<dynamic>(); v["SelectedCategoryId"] = null; v["SelectedIsPublished"] = false;
        });
        AssertBuiltFromTheComponent(help, 1);
        Assert.Single(Regex.Matches(Regex.Match(help, "<div class=\"lp-chips\">(.*?)</div>", RegexOptions.Singleline).Value, "class=\"pems-filter-chip\""));
        Assert.Contains("bi-send-check", help);                                           // publish a draft
    }

    [Fact]
    public void EveryWave4bPage_AndTheInlineEditors_UseTheComponents()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.Web", "PEMS.Web.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var views = Path.Combine(dir!.FullName, "PEMS.Web", "Areas", "Admin", "Views");
        foreach (var page in Pages)
        {
            var view = File.ReadAllText(Path.Combine(views, page, "Index.cshtml"));
            Assert.Contains("<pems-list-page", view);
            Assert.Contains("_content/PEMS.UI/pems-list.js", view);
            Assert.DoesNotContain("class=\"btn btn-", view);                             // buttons come from the components
        }
        // the inline editors moved with the RelatedList row-form mode (SCRUM-337): list frame, rows edited in place
        foreach (var editor in new[] { "ContractApprovalAuthority", "Numbering", "RoutingRules" })
        {
            var view = File.ReadAllText(Path.Combine(views, editor, "Index.cshtml"));
            Assert.Contains("<pems-list-page", view);
            Assert.Contains("<pems-related-row-form", view);
            Assert.Contains("<pems-related-save", view);
            Assert.DoesNotContain("class=\"btn btn-", view);
        }
    }
}
