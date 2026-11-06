using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.Components;
using PEMS.UI.TagHelpers;

namespace PEMS.UI.Tests;

/// <summary>SCRUM-508: the check / switch row, the pager, the label element and the plain table write the classes the pages wrote by hand.</summary>
public sealed class CheckRowPagerTests
{
    private static string Run(TagHelper helper, string tag, TagHelperAttributeList attributes, string inner = "")
    {
        var context = new TagHelperContext(tag, new TagHelperAttributeList(), new Dictionary<object, object>(), "c");
        var output = new TagHelperOutput(tag, attributes, (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(inner)));
        output.Content.SetHtmlContent(inner);
        helper.ProcessAsync(context, output).GetAwaiter().GetResult();
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public void A_switch_row_is_form_check_form_switch_with_the_page_css()
        => Assert.Equal("<div class=\"form-check form-switch mb-0\">x</div>", Run(new PemsCheckRowTagHelper(UiTestRenderer.Instance) { Switch = true, Css = "mb-0" }, "pems-check-row", [], "x"));

    [Fact]
    public void A_plain_check_row_is_form_check_and_inline_adds_form_check_inline()
    {
        Assert.Equal("<div class=\"form-check\">x</div>", Run(new PemsCheckRowTagHelper(UiTestRenderer.Instance), "pems-check-row", [], "x"));
        Assert.Equal("<div class=\"form-check form-check-inline\">x</div>", Run(new PemsCheckRowTagHelper(UiTestRenderer.Instance) { Inline = true }, "pems-check-row", [], "x"));
    }

    [Fact]
    public void A_pager_writes_a_link_per_page_and_marks_the_current_one()
    {
        var html = Run(new PemsPagerTagHelper(UiTestRenderer.Instance) { Current = 2, Total = 3, Href = "/r?page={page}", Scale = "small", Css = "mb-0" }, "pems-pager", [new("class", "mt-2")]);
        Assert.Contains("<nav class=\"mt-2\">", html);
        Assert.Contains("<ul class=\"pagination pagination-sm mb-0\">", html);
        Assert.Contains("<li class=\"page-item\"><a class=\"page-link\" href=\"/r?page=1\">1</a></li>", html);
        Assert.Contains("<li class=\"page-item active\"><a class=\"page-link\" href=\"/r?page=2\">2</a></li>", html);
        Assert.Contains("href=\"/r?page=3\"", html);
    }

    [Theory]
    [InlineData("span", "<span class=\"form-label d-block\">t</span>")]
    [InlineData("legend", "<legend class=\"form-label d-block\">t</legend>")]
    [InlineData("label", "<label class=\"form-label d-block\">t</label>")]
    [InlineData("script", "<label class=\"form-label d-block\">t</label>")]
    public void A_form_label_can_be_a_span_or_legend_and_never_another_element(string element, string expected)
        => Assert.Equal(expected, Run(new PemsLabelTagHelper(null!, UiTestRenderer.Instance) { As = element, Css = "d-block" }, "pems-label", [], "t"));

    [Fact]
    public void A_plain_table_writes_only_the_page_class()
    {
        Assert.Equal("<table class=\"ct-table\">r</table>", Run(new PemsTableFrameTagHelper(UiTestRenderer.Instance) { Kind = "plain", Css = "ct-table" }, "pems-table", [], "r"));
        Assert.Equal("<table class=\"table table-sm\">r</table>", Run(new PemsTableFrameTagHelper(UiTestRenderer.Instance) { Scale = "small" }, "pems-table", [], "r"));
    }
}
