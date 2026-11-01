using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PEMS.UI.TagHelpers;

// SCRUM-337 InfoGrid: the label / value block at the top of a detail page (code, dates, people, contact). A description list
// laid out as a responsive grid, so screen readers read each label with its value. The page keeps its own values (links,
// badges, conditions); an empty value shows a dash. Styles: _content/PEMS.UI/pems-ui.css (pems-info*), loaded by the layout.
// Usage: docs/design/LIST_PAGE_COMPONENT.md (InfoGrid).

/// <summary>The grid: <c>&lt;dl class="pems-info"&gt;</c> around its <c>pems-info-item</c>s. A class or id on the tag is kept.</summary>
[HtmlTargetElement("pems-info-grid")]
public sealed class PemsInfoGridTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output) => PemsList.Become(output, "dl", "pems-info");
}

/// <summary>
/// One label and its value (child content). <c>wide</c> takes the whole row; <c>num</c> sets numbers, codes and dates in a
/// fixed-width, left-to-right font; <c>mono</c> sets a code in a small muted fixed-width font; <c>ltr</c> keeps a phone number
/// or address left to right in Persian pages; <c>tone="danger"</c> colours the value. An empty value shows <c>empty-text</c>
/// (a dash by default).
/// </summary>
[HtmlTargetElement("pems-info-item", ParentTag = "pems-info-grid")]
public sealed class PemsInfoItemTagHelper : TagHelper
{
    [HtmlAttributeName("label")] public string Label { get; set; } = "";
    [HtmlAttributeName("wide")] public bool Wide { get; set; }
    [HtmlAttributeName("num")] public bool Num { get; set; }
    [HtmlAttributeName("mono")] public bool Mono { get; set; }
    [HtmlAttributeName("ltr")] public bool Ltr { get; set; }
    [HtmlAttributeName("tone")] public string? Tone { get; set; }
    [HtmlAttributeName("empty-text")] public string EmptyText { get; set; } = "-";
    [HtmlAttributeName("value-id")] public string? ValueId { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        PemsList.Become(output, "div", Wide ? "pems-info__item pems-info__item--wide" : "pems-info__item");

        var dt = new TagBuilder("dt");
        dt.AddCssClass("pems-info__label");
        dt.InnerHtml.Append(Label);

        var dd = new TagBuilder("dd");
        dd.AddCssClass("pems-info__value");
        if (Mono) dd.AddCssClass("pems-info__value--mono");
        if (Tone == "danger") dd.AddCssClass("text-danger");   // the app's own danger text colour, as before
        else if (Tone is "warning" or "success") dd.AddCssClass("pems-info__value--" + Tone);
        if (!string.IsNullOrEmpty(ValueId)) dd.Attributes["id"] = ValueId;

        var content = await output.GetChildContentAsync();
        if (content.IsEmptyOrWhiteSpace) dd.InnerHtml.Append(EmptyText);
        else if (Num || Ltr)
        {
            // an inline left-to-right run: the value stays at the start edge of a Persian page, its digits keep their order
            var run = new TagBuilder("span");
            run.Attributes["dir"] = "ltr";
            if (Num) run.AddCssClass("pems-info__num");
            run.InnerHtml.AppendHtml(content);
            dd.InnerHtml.AppendHtml(run);
        }
        else dd.InnerHtml.AppendHtml(content);

        output.Content.SetHtmlContent(dt);
        output.Content.AppendHtml(dd);
    }
}
