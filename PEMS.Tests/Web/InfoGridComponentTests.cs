using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.TagHelpers;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-337 InfoGrid: the label / value block of a detail page (<c>pems-info-grid</c> / <c>pems-info-item</c>) and the
/// first pages built from it (wave I1: user, engineering document, equipment, VDR and vendor details).
/// </summary>
public class InfoGridComponentTests
{
    private static async Task<string> Run(TagHelper helper, string tag, string childHtml = "", TagHelperAttributeList? attributes = null)
    {
        var context = new TagHelperContext(tag, attributes ?? new TagHelperAttributeList(), new Dictionary<object, object>(), "u1");
        var output = new TagHelperOutput(tag, attributes ?? new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(childHtml)));
        helper.Init(context);
        await helper.ProcessAsync(context, output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task Grid_IsADescriptionList_AndKeepsThePagesClass()
    {
        var html = await Run(new PemsInfoGridTagHelper(), "pems-info-grid", "<div>x</div>", new TagHelperAttributeList { { "class", "mt-2" } });
        Assert.StartsWith("<dl class=\"pems-info mt-2\">", html);
        Assert.EndsWith("</dl>", html);
    }

    [Fact]
    public async Task Item_PairsTheLabelWithItsValue()
    {
        var html = await Run(new PemsInfoItemTagHelper { Label = "Vendor code" }, "pems-info-item", "<a href=\"/v/1\">V-1</a>");
        Assert.Equal("<div class=\"pems-info__item\"><dt class=\"pems-info__label\">Vendor code</dt><dd class=\"pems-info__value\"><a href=\"/v/1\">V-1</a></dd></div>", html);
    }

    [Fact]
    public async Task Item_ShowsADash_WhenTheValueIsEmpty()
    {
        Assert.Contains("<dd class=\"pems-info__value\">-</dd>", await Run(new PemsInfoItemTagHelper { Label = "Country" }, "pems-info-item", "  "));
        Assert.Contains("<dd class=\"pems-info__value\">N/A</dd>", await Run(new PemsInfoItemTagHelper { Label = "Country", EmptyText = "N/A" }, "pems-info-item"));
    }

    [Fact]
    public async Task Item_Options_Wide_Num_Mono_Ltr_Tone()
    {
        var wide = await Run(new PemsInfoItemTagHelper { Label = "Description", Wide = true }, "pems-info-item", "text");
        Assert.StartsWith("<div class=\"pems-info__item pems-info__item--wide\">", wide);

        // numbers and phone numbers run left to right inside the value, so the value stays at the start edge of a Persian page
        var num = await Run(new PemsInfoItemTagHelper { Label = "Revision", Num = true }, "pems-info-item", "B2");
        Assert.Contains("<dd class=\"pems-info__value\"><span class=\"pems-info__num\" dir=\"ltr\">B2</span></dd>", num);
        var ltr = await Run(new PemsInfoItemTagHelper { Label = "Phone", Ltr = true }, "pems-info-item", "+98 21 1234");
        Assert.Contains("<dd class=\"pems-info__value\"><span dir=\"ltr\">+98 21 1234</span></dd>", ltr);

        Assert.Contains("class=\"pems-info__value pems-info__value--mono\"", await Run(new PemsInfoItemTagHelper { Label = "Code", Mono = true }, "pems-info-item", "C"));
        Assert.Contains("class=\"pems-info__value text-danger\"", await Run(new PemsInfoItemTagHelper { Label = "Overdue", Tone = "danger" }, "pems-info-item", "3"));
        Assert.Contains("<dd class=\"pems-info__value\">0</dd>", await Run(new PemsInfoItemTagHelper { Label = "Overdue", Tone = "" }, "pems-info-item", "0"));
        Assert.Contains("<dd class=\"pems-info__value\" id=\"permInfo\">", await Run(new PemsInfoItemTagHelper { Label = "P", ValueId = "permInfo" }, "pems-info-item", "1"));
    }

    [Fact]
    public async Task Item_EncodesItsLabel()
    {
        var html = await Run(new PemsInfoItemTagHelper { Label = "P&ID <x>" }, "pems-info-item", "1");
        Assert.Contains("<dt class=\"pems-info__label\">P&amp;ID &lt;x&gt;</dt>", html);
    }

    [Fact]
    public void Stylesheet_HasTheGrid_InTheGlobalComponentFile()
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "PEMS.UI", "wwwroot", "pems-ui.css"));
        Assert.Contains(".pems-info {", css);
        Assert.Contains(".pems-info__item--wide { grid-column: 1 / -1; }", css);
        Assert.Contains(".pems-info__num {", css);
    }

    [Theory]
    [InlineData("Areas/Admin/Views/Users/Details.cshtml", 3, 12)]
    [InlineData("Views/EngineeringDocuments/Details.cshtml", 2, 15)]
    [InlineData("Views/Equipment/Details.cshtml", 1, 7)]
    [InlineData("Views/VDR/Details.cshtml", 1, 7)]
    [InlineData("Views/Vendor/Details.cshtml", 1, 8)]
    public void WaveI1_TakesItsLabelValueBlocksFromTheComponent(string view, int grids, int items)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "PEMS.Web", view));
        Assert.Equal(grids, Regex.Matches(text, "<pems-info-grid>").Count);
        Assert.Equal(items, Regex.Matches(text, "<pems-info-item ").Count);
        Assert.DoesNotContain("vs-info", text);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.Web", "PEMS.Web.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
