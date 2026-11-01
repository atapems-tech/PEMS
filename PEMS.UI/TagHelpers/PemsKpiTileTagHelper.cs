using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PEMS.UI.TagHelpers;

// SCRUM-337 KpiTile: one number with its label (report headers, dashboards, detail pages). Renders the kpi-card markup the
// `_KpiCard` partial renders (styles: admin.css .kpi-card / .kpi-icon / .kpi-value / .kpi-label / .bg-icon-*), so a page looks
// the same; the value is the page's own text (counts, decimals, %, units). Usage: docs/design/LIST_PAGE_COMPONENT.md (KpiTile).

/// <summary>
/// A KPI tile: <c>label</c>, the value as child content (a dash when empty), an <c>icon</c> (Bootstrap icon name) in a
/// coloured square (<c>color</c>: blue, green, amber, red, purple, slate, orange ... or a full <c>bg-icon-*</c> class),
/// an optional <c>sub</c> line under the label, <c>href</c> to make the whole tile a link, and <c>hint</c> as its tool tip.
/// </summary>
[HtmlTargetElement("pems-kpi-tile")]
public sealed class PemsKpiTileTagHelper : TagHelper
{
    [HtmlAttributeName("label")] public string Label { get; set; } = "";
    [HtmlAttributeName("icon")] public string? Icon { get; set; }
    [HtmlAttributeName("color")] public string? Color { get; set; }
    [HtmlAttributeName("sub")] public string? Sub { get; set; }
    [HtmlAttributeName("href")] public string? Href { get; set; }
    [HtmlAttributeName("hint")] public string? Hint { get; set; }

    /// <summary>The colour class of the icon square: <c>green</c> becomes <c>bg-icon-green</c>; a full class is kept.</summary>
    public static string ColorClass(string? color) =>
        string.IsNullOrWhiteSpace(color) ? "bg-icon-blue" : color.StartsWith("bg-icon-", StringComparison.Ordinal) ? color : "bg-icon-" + color;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var link = !string.IsNullOrEmpty(Href);
        PemsList.Become(output, link ? "a" : "div", link ? "kpi-card kpi-link text-decoration-none" : "kpi-card");
        if (link) output.Attributes.SetAttribute("href", Href);
        if (!string.IsNullOrEmpty(Hint)) output.Attributes.SetAttribute("title", Hint);

        var body = new TagBuilder("div");
        var value = new TagBuilder("div");
        value.AddCssClass("kpi-value");
        var content = await output.GetChildContentAsync();
        if (content.IsEmptyOrWhiteSpace) value.InnerHtml.Append("-");
        else value.InnerHtml.AppendHtml(content);
        body.InnerHtml.AppendHtml(value);

        var label = new TagBuilder("div");
        label.AddCssClass("kpi-label");
        label.InnerHtml.Append(Label);
        body.InnerHtml.AppendHtml(label);
        if (!string.IsNullOrEmpty(Sub))
        {
            var sub = new TagBuilder("div");
            sub.AddCssClass("kpi-sub");
            sub.InnerHtml.Append(Sub);
            body.InnerHtml.AppendHtml(sub);
        }

        output.Content.Clear();
        if (!string.IsNullOrEmpty(Icon))
        {
            var square = new TagBuilder("div");
            square.AddCssClass("kpi-icon " + ColorClass(Color));
            square.InnerHtml.AppendHtml(PemsList.Icon(Icon));
            output.Content.AppendHtml(square);
        }
        output.Content.AppendHtml(body);
    }
}
