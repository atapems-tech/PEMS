using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.TagHelpers;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-337 KpiTile: one number with its label (<c>pems-kpi-tile</c>), the same kpi-card markup as the <c>_KpiCard</c> partial,
/// and the first pages built from it (wave K1: the six report pages).
/// </summary>
public class KpiTileComponentTests
{
    private static async Task<string> Run(PemsKpiTileTagHelper helper, string childHtml = "")
    {
        var context = new TagHelperContext("pems-kpi-tile", new TagHelperAttributeList(), new Dictionary<object, object>(), "u1");
        var output = new TagHelperOutput("pems-kpi-tile", new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(childHtml)));
        helper.Init(context);
        await helper.ProcessAsync(context, output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task Tile_RendersTheKpiCardMarkup()
    {
        var html = await Run(new PemsKpiTileTagHelper { Label = "Open", Icon = "bi-tools", Color = "green" }, "12");
        Assert.Equal("<div class=\"kpi-card\"><div class=\"kpi-icon bg-icon-green\"><i aria-hidden=\"true\" class=\"bi bi-tools\"></i></div>"
                     + "<div><div class=\"kpi-value\">12</div><div class=\"kpi-label\">Open</div></div></div>", html);
    }

    [Fact]
    public async Task Tile_WithHref_IsALinkWithItsHint()
    {
        var html = await Run(new PemsKpiTileTagHelper { Label = "Open", Icon = "bi-tools", Href = "/WorkOrder?status=open", Hint = "Not yet closed" }, "3");
        Assert.StartsWith("<a class=\"kpi-card kpi-link text-decoration-none\" href=\"/WorkOrder?status=open\" title=\"Not yet closed\">", html);
        Assert.EndsWith("</a>", html);
    }

    [Fact]
    public async Task Tile_EmptyValueShowsADash_NoIconNoSquare_AndASubLine()
    {
        var html = await Run(new PemsKpiTileTagHelper { Label = "Held runs", Sub = "since Monday" });
        Assert.Contains("<div class=\"kpi-value\">-</div>", html);
        Assert.DoesNotContain("kpi-icon", html);
        Assert.Contains("<div class=\"kpi-sub\">since Monday</div>", html);
    }

    [Theory]
    [InlineData(null, "bg-icon-blue")]
    [InlineData("amber", "bg-icon-amber")]
    [InlineData("bg-icon-teal", "bg-icon-teal")]
    public void Color_BecomesTheIconClass(string? color, string expected) => Assert.Equal(expected, PemsKpiTileTagHelper.ColorClass(color));

    [Fact]
    public async Task Tile_EncodesItsLabel_AndKeepsTheValueMarkup()
    {
        var html = await Run(new PemsKpiTileTagHelper { Label = "R&D <x>" }, "42 <small>%</small>");
        Assert.Contains("<div class=\"kpi-label\">R&amp;D &lt;x&gt;</div>", html);
        Assert.Contains("<div class=\"kpi-value\">42 <small>%</small></div>", html);
    }

    [Theory]
    [InlineData("Index", 6)]
    [InlineData("Cmms", 6)]
    [InlineData("Engineering", 6)]
    [InlineData("Operations", 6)]
    [InlineData("Procurement", 10)]
    [InlineData("Vdr", 6)]
    public void WaveK1_ReportPages_TakeTheirTilesFromTheComponent(string page, int tiles)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "PEMS.Web", "Views", "Report", page + ".cshtml"));
        Assert.Equal(tiles, Regex.Matches(text, "<pems-kpi-tile ").Count);
        Assert.DoesNotContain("kpi-card", text);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.Web", "PEMS.Web.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
