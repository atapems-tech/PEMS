using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.Web.Models;

namespace PEMS.UI.TagHelpers;

// SCRUM-337 Chart: one chart host for every report and dashboard. The page describes the chart (BkChartSpec: kind, labels,
// series, colours from the --pems-chart-* tokens); the Bklit island in dist/dashboards/dashboards.js draws it from
// data-bk-chart. The visually hidden table (or line, for a gauge) is the accessible and no-script copy of the same numbers.
// The page loads the bundle once with the _BkChartStyles / _BkChartScripts partials. Usage: docs/design/LIST_PAGE_COMPONENT.md (Chart).

/// <summary><c>&lt;pems-chart spec="@Model.VolumeChart" /&gt;</c>: the chart host and its accessible table.</summary>
[HtmlTargetElement("pems-chart", TagStructure = TagStructure.WithoutEndTag)]
public sealed class PemsChartTagHelper : TagHelper
{
    [HtmlAttributeName("spec")] public BkChartSpec? Spec { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        output.Content.SetHtmlContent(Spec is null ? "" : Markup(Spec));
    }

    /// <summary>The host element and the accessible copy, the markup the retired _BkChart partial rendered.</summary>
    public static string Markup(BkChartSpec spec)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var culture = CultureInfo.CurrentCulture;
        var sb = new StringBuilder();
        sb.Append("<div class=\"bk-scope bk-chart min-w-0\" data-bk-chart=\"").Append(E(spec.ToJson()))
          .Append("\" role=\"img\" aria-label=\"").Append(E(spec.AriaLabel)).Append("\"></div>");
        if (spec.Kind == "gauge")
        {
            if (spec.Value.HasValue)
                sb.Append("<span class=\"visually-hidden\">").Append(E(spec.AriaLabel)).Append(": ")
                  .Append(E(spec.Value.Value.ToString("0.#", culture))).Append(E(spec.Unit)).Append("</span>");
        }
        else if (spec.Labels.Count > 0)
        {
            sb.Append("<table class=\"visually-hidden\"><caption>").Append(E(spec.AriaLabel)).Append("</caption><thead><tr><th scope=\"col\"></th>");
            foreach (var s in spec.Series) sb.Append("<th scope=\"col\">").Append(E(s.Name)).Append("</th>");
            sb.Append("</tr></thead><tbody>");
            for (var i = 0; i < spec.Labels.Count; i++)
            {
                sb.Append("<tr><th scope=\"row\">").Append(E(spec.Labels[i])).Append("</th>");
                foreach (var s in spec.Series)
                    sb.Append("<td>").Append(i < s.Values.Count ? E(s.Values[i].ToString("0.##", culture)) : "").Append("</td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table>");
        }
        return sb.ToString();
    }
}
