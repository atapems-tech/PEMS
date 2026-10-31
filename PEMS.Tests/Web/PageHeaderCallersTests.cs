using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PEMS.Tests.TestSupport;
using PEMS.Web.Areas.Admin.Controllers;
using PEMS.Web.Security;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-425: pages that set a title but showed no heading at all now show the shared title row (_PageHeader).
/// The layout still does not render it (GlobalPageHeaderTests); each page calls it itself.
/// SCRUM-337 W2: Finding/Index left this list; its heading now comes from the list-page toolbar (CmmsListPagesTests).
/// </summary>
public class PageHeaderCallersTests
{
    public static readonly string[] Callers =
    [
        "ImportCenter/Index.cshtml", "ShiftHandover/Create.cshtml",
        "CriticalityAssessment/Create.cshtml", "Barcode/Verify.cshtml"
    ];

    private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here())!, "..", ".."));
    private static string Here([CallerFilePath] string p = "") => p;

    [Fact]
    public void EachCallerPage_CallsThePartial_OnceAndDoesNotHideTheTitle()
    {
        foreach (var rel in Callers)
        {
            var t = File.ReadAllText(Path.Combine(Root(), "PEMS.Web", "Views", rel.Replace('/', Path.DirectorySeparatorChar)));
            Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(t, "_PageHeader").Count);
            Assert.DoesNotContain("HidePageTitle", t);
        }
    }

    [Fact]
    public async Task ImportCenterIndex_RendersTheTitleRowAboveItsContent()
    {
        var host = new PermissionMatrixController(null!, null!, null!);
        var model = Array.Empty<ImportCatalog.ImportEntry>().ToList().AsReadOnly();
        var result = new ViewResult { ViewData = new ViewDataDictionary(host.ViewData) { Model = model } };
        var html = await RazorViewRenderer.RenderAsync(host, result, "~/Views/ImportCenter/Index.cshtml");
        var head = html.IndexOf("class=\"pems-page-head\"", StringComparison.Ordinal);
        Assert.True(head >= 0, "title row missing");
        Assert.Contains("<h1 class=\"pems-page-title\">", html, StringComparison.Ordinal);
        Assert.True(head < html.IndexOf("text-muted small mb-3", StringComparison.Ordinal), "the title row must come before the subtitle");
    }
}
