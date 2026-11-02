using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.TagHelpers;
using PEMS.Web.Models;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-337 Chart: <c>&lt;pems-chart spec="..."&gt;</c> is the one chart host (wave C1: the report pages, KPI, letter cartable report
/// and part catalogue reports). The host markup itself is covered by BkChartPagesTests.
/// </summary>
public class ChartComponentTests
{
    private static string Run(BkChartSpec? spec)
    {
        var helper = new PemsChartTagHelper { Spec = spec };
        var context = new TagHelperContext("pems-chart", new TagHelperAttributeList(), new Dictionary<object, object>(), "u1");
        var output = new TagHelperOutput("pems-chart", new TagHelperAttributeList(), (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        helper.Process(context, output);
        using var w = new StringWriter();
        output.WriteTo(w, HtmlEncoder.Default);
        return w.ToString();
    }

    [Fact]
    public void Tag_WritesOnlyTheHostAndTheAccessibleCopy()
    {
        var html = Run(BkChartSpec.MonthlyBars("Letters", "none", new[] { "2026-01" }, BkSeries.Of("In", new[] { 3 })));
        Assert.StartsWith("<div class=\"bk-scope bk-chart min-w-0\" data-bk-chart=", html);
        Assert.DoesNotContain("pems-chart", html);
        Assert.Contains("<caption>Letters</caption>", html);
    }

    [Fact]
    public void Gauge_HasAScreenReaderLine_AndNoValueMeansNoLine()
    {
        Assert.Contains("<span class=\"visually-hidden\">PM compliance: 97.5%</span>", Run(BkChartSpec.Gauge("PM compliance", "none", "PM", 97.5, "%", 1)));
        Assert.DoesNotContain("visually-hidden", Run(BkChartSpec.Gauge("PM compliance", "none", "PM", null)));
    }

    [Fact]
    public void NoSpec_WritesNothing() => Assert.Equal("", Run(null));

    [Fact]
    public void Spec_LivesInTheComponentLibrary_AndThePartialIsGone()
    {
        Assert.Equal("PEMS.UI.Abstractions", typeof(BkChartSpec).Assembly.GetName().Name);
        Assert.False(File.Exists(Path.Combine(RepoRoot(), "PEMS.Web", "Views", "Shared", "_BkChart.cshtml")));
    }

    [Theory]
    [InlineData("Views/Report/Index.cshtml", 3)]
    [InlineData("Views/Report/Cmms.cshtml", 3)]
    [InlineData("Views/Report/Engineering.cshtml", 3)]
    [InlineData("Views/Report/Procurement.cshtml", 3)]
    [InlineData("Views/Report/Vdr.cshtml", 3)]
    [InlineData("Views/Kpi/Index.cshtml", 8)]
    [InlineData("Views/LetterCartable/Report.cshtml", 4)]
    [InlineData("Areas/Warehouse/Views/PartCatalogueReport/Index.cshtml", 4)]
    public void WaveC1_PagesTakeTheirChartsFromTheComponent(string view, int charts)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "PEMS.Web", view));
        Assert.Equal(charts, Regex.Matches(text, "<pems-chart spec=").Count);
        Assert.DoesNotContain("_BkChart\"", text);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.Web", "PEMS.Web.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
