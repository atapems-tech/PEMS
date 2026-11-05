using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.Components;
using PEMS.UI.Rendering;

namespace PEMS.UI.TagHelpers;

/// <summary>
/// A grid row: <c>&lt;pems-row gap="2" css="mb-3"&gt;&lt;pems-col md="6"&gt;...&lt;/pems-col&gt;&lt;/pems-row&gt;</c> writes <c>&lt;div class="row g-2 mb-3"&gt;</c>.
/// <c>gap</c>, <c>gap-x</c>, <c>gap-y</c> are the gutters (0 to 5). Every other attribute (id, data-*) is kept.
/// </summary>
[HtmlTargetElement("pems-row")]
[PemsFacade(UiKeys.Row)]
public sealed class PemsRowTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    [HtmlAttributeName("gap")] public string? Gap { get; set; }
    [HtmlAttributeName("gap-x")] public string? GapX { get; set; }
    [HtmlAttributeName("gap-y")] public string? GapY { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var (pageClass, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsRow>(renderer, output, new RowOptions { Gap = Gap, GapX = GapX, GapY = GapY, Css = Css, PageClass = pageClass, Attributes = attrs }, await ListRender.Inner(output));
    }
}

/// <summary>
/// A grid column: <c>&lt;pems-col span="12" md="6" css="mb-3"&gt;</c> writes <c>&lt;div class="col-12 col-md-6 mb-3"&gt;</c>. Each breakpoint (<c>span</c> = all sizes,
/// <c>sm</c>, <c>md</c>, <c>lg</c>, <c>xl</c>, <c>xxl</c>) takes a width 1 to 12, <c>auto</c>, or an empty value for an equal-width column.
/// </summary>
[HtmlTargetElement("pems-col")]
[PemsFacade(UiKeys.Col)]
public sealed class PemsColTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    [HtmlAttributeName("span")] public string? Span { get; set; }
    [HtmlAttributeName("sm")] public string? Sm { get; set; }
    [HtmlAttributeName("md")] public string? Md { get; set; }
    [HtmlAttributeName("lg")] public string? Lg { get; set; }
    [HtmlAttributeName("xl")] public string? Xl { get; set; }
    [HtmlAttributeName("xxl")] public string? Xxl { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var (pageClass, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsCol>(renderer, output,
            new ColOptions { Span = Span, Sm = Sm, Md = Md, Lg = Lg, Xl = Xl, Xxl = Xxl, Css = Css, PageClass = pageClass, Attributes = attrs }, await ListRender.Inner(output));
    }
}
