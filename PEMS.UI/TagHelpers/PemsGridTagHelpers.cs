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

/// <summary>A check box / radio / switch with its label: <c>&lt;pems-check-row switch css="mb-0"&gt;&lt;pems-input kind="check" .../&gt;&lt;label class="form-check-label"&gt;...&lt;/label&gt;&lt;/pems-check-row&gt;</c>.</summary>
[HtmlTargetElement("pems-check-row")]
[PemsFacade(UiKeys.CheckRow)]
public sealed class PemsCheckRowTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    [HtmlAttributeName("switch")] public bool Switch { get; set; }
    [HtmlAttributeName("inline")] public bool Inline { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var (pageClass, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsCheckRow>(renderer, output, new CheckRowOptions { Switch = Switch, Inline = Inline, Css = Css, PageClass = pageClass, Attributes = attrs }, await ListRender.Inner(output));
    }
}

/// <summary>Page-number links: <c>&lt;pems-pager current="2" total="5" href="/items?page={page}" scale="small" css="mb-0" /&gt;</c> (<c>{page}</c> is replaced by each page number).</summary>
[HtmlTargetElement("pems-pager")]
[PemsFacade(UiKeys.Pager)]
public sealed class PemsPagerTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    [HtmlAttributeName("current")] public int Current { get; set; } = 1;
    [HtmlAttributeName("total")] public int Total { get; set; } = 1;
    [HtmlAttributeName("href")] public string Href { get; set; } = "?page={page}";
    [HtmlAttributeName("scale")] public string? Scale { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var (pageClass, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsPager>(renderer, output, new PagerOptions { Current = Current, Total = Total, Href = Href, Scale = Scale, Css = Css, PageClass = pageClass, Attributes = attrs });
    }
}

/// <summary>The white content panel of a page: <c>&lt;pems-content-card css="mb-3"&gt;...&lt;/pems-content-card&gt;</c> writes <c>&lt;div class="content-card mb-3"&gt;</c>.</summary>
[HtmlTargetElement("pems-content-card")]
[PemsFacade(UiKeys.ContentCard)]
public sealed class PemsContentCardTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    /// <summary>div (default), section, article or details.</summary>
    [HtmlAttributeName("as")] public string As { get; set; } = "div";
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var (pageClass, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsContentCard>(renderer, output, new ContentCardOptions { As = As, Css = Css, PageClass = pageClass, Attributes = attrs }, await ListRender.Inner(output));
    }
}

/// <summary>The outer wrapper of a page: <c>&lt;pems-page-shell kind="admin"&gt;</c> writes <c>&lt;div class="admin-page-shell"&gt;</c>, <c>kind="workspace"</c> (default) <c>&lt;div class="vs-page"&gt;</c>.</summary>
[HtmlTargetElement("pems-page-shell")]
[PemsFacade(UiKeys.PageShell)]
public sealed class PemsPageShellTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    [HtmlAttributeName("kind")] public string Kind { get; set; } = "workspace";
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var (pageClass, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsPageShell>(renderer, output, new PageShellOptions { Kind = Kind, Css = Css, PageClass = pageClass, Attributes = attrs }, await ListRender.Inner(output));
    }
}

/// <summary>
/// A flex row or column: <c>&lt;pems-flex justify="between" align="center" wrap="true" gap="2" css="mb-3"&gt;</c> writes
/// <c>&lt;div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3"&gt;</c>. <c>dir="column"</c> stacks; <c>as</c> is div (default), span, li, header, footer, section, h5 or h6;
/// <c>inline="true"</c> writes <c>d-inline-flex</c>. Every other attribute (id, data-*) is kept.
/// </summary>
[HtmlTargetElement("pems-flex")]
[PemsFacade(UiKeys.Flex)]
public sealed class PemsFlexTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    [HtmlAttributeName("as")] public string As { get; set; } = "div";
    [HtmlAttributeName("inline")] public bool Inline { get; set; }
    [HtmlAttributeName("dir")] public string? Dir { get; set; }
    [HtmlAttributeName("justify")] public string? Justify { get; set; }
    [HtmlAttributeName("align")] public string? Align { get; set; }
    [HtmlAttributeName("wrap")] public bool Wrap { get; set; }
    [HtmlAttributeName("gap")] public string? Gap { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var (pageClass, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsFlex>(renderer, output, new FlexOptions { As = As, Inline = Inline, Dir = Dir, Justify = Justify, Align = Align, Wrap = Wrap, Gap = Gap, Css = Css, PageClass = pageClass, Attributes = attrs }, await ListRender.Inner(output));
    }
}

/// <summary>The muted help line under a control: <c>&lt;pems-help-text&gt;text&lt;/pems-help-text&gt;</c> writes <c>&lt;div class="form-text"&gt;</c> (<c>as="small"</c> or <c>as="span"</c> for inline help).</summary>
[HtmlTargetElement("pems-help-text")]
[PemsFacade(UiKeys.HelpText)]
public sealed class PemsHelpTextTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    [HtmlAttributeName("as")] public string As { get; set; } = "div";
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var (pageClass, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsHelpText>(renderer, output, new HelpTextOptions { As = As, Css = Css, PageClass = pageClass, Attributes = attrs }, await ListRender.Inner(output));
    }
}

/// <summary>The scroll wrapper of a wide table: <c>&lt;pems-table-scroll&gt;&lt;pems-table&gt;...&lt;/pems-table&gt;&lt;/pems-table-scroll&gt;</c> writes <c>&lt;div class="table-responsive"&gt;</c>.</summary>
[HtmlTargetElement("pems-table-scroll")]
[PemsFacade(UiKeys.TableScroll)]
public sealed class PemsTableScrollTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var (pageClass, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsTableScroll>(renderer, output, new TableScrollOptions { Css = Css, PageClass = pageClass, Attributes = attrs }, await ListRender.Inner(output));
    }
}

/// <summary>The container of the panes of a tab strip: <c>&lt;pems-tab-content&gt;&lt;pems-tab-pane id="a" active="true" fade="true"&gt;...&lt;/pems-tab-pane&gt;&lt;/pems-tab-content&gt;</c> writes <c>&lt;div class="tab-content"&gt;</c>.</summary>
[HtmlTargetElement("pems-tab-content")]
[PemsFacade(UiKeys.TabContent)]
public sealed class PemsTabContentTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var (pageClass, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsTabContent>(renderer, output, new TabContentOptions { Css = Css, PageClass = pageClass, Attributes = attrs }, await ListRender.Inner(output));
    }
}

/// <summary>One tab pane: <c>&lt;pems-tab-pane id="info" fade="true" active="true"&gt;</c> writes <c>&lt;div class="tab-pane fade show active" id="info"&gt;</c>; <c>role</c>, <c>aria-labelledby</c> and every other attribute are kept.</summary>
[HtmlTargetElement("pems-tab-pane")]
[PemsFacade(UiKeys.TabPane)]
public sealed class PemsTabPaneTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    [HtmlAttributeName("fade")] public bool Fade { get; set; }
    [HtmlAttributeName("active")] public bool Active { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var (pageClass, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsTabPane>(renderer, output, new TabPaneOptions { Fade = Fade, Active = Active, Css = Css, PageClass = pageClass, Attributes = attrs }, await ListRender.Inner(output));
    }
}

/// <summary>A labelled number tile: <c>&lt;pems-stat label="Modules scanned"&gt;@Model.Count&lt;/pems-stat&gt;</c> writes <c>&lt;div class="admin-kpi"&gt;&lt;div class="admin-kpi-label"&gt;...&lt;/div&gt;&lt;div class="admin-kpi-value"&gt;...&lt;/div&gt;&lt;/div&gt;</c>.</summary>
[HtmlTargetElement("pems-stat")]
[PemsFacade(UiKeys.Stat)]
public sealed class PemsStatTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    [HtmlAttributeName("label")] public string Label { get; set; } = "";
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var (pageClass, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsStat>(renderer, output, new StatOptions { Label = Label, Css = Css, PageClass = pageClass, Attributes = attrs }, await ListRender.Inner(output));
    }
}

/// <summary>The grid of stat tiles: <c>&lt;pems-stat-grid&gt;&lt;pems-stat .../&gt;...&lt;/pems-stat-grid&gt;</c> writes <c>&lt;div class="admin-kpi-grid"&gt;</c>.</summary>
[HtmlTargetElement("pems-stat-grid")]
[PemsFacade(UiKeys.StatGrid)]
public sealed class PemsStatGridTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var (pageClass, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsStatGrid>(renderer, output, new StatGridOptions { Css = Css, PageClass = pageClass, Attributes = attrs }, await ListRender.Inner(output));
    }
}

/// <summary>A big number: <c>&lt;pems-metric css="text-success"&gt;42&lt;/pems-metric&gt;</c> writes <c>&lt;div class="fs-4 fw-bold text-success"&gt;42&lt;/div&gt;</c>.</summary>
[HtmlTargetElement("pems-metric")]
[PemsFacade(UiKeys.Metric)]
public sealed class PemsMetricTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var (pageClass, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsMetric>(renderer, output, new MetricOptions { Css = Css, PageClass = pageClass, Attributes = attrs }, await ListRender.Inner(output));
    }
}

/// <summary>The intro banner under a page title: <c>&lt;pems-page-hero&gt;text and actions&lt;/pems-page-hero&gt;</c> writes <c>&lt;div class="admin-hero"&gt;</c>.</summary>
[HtmlTargetElement("pems-page-hero")]
[PemsFacade(UiKeys.PageHero)]
public sealed class PemsPageHeroTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var (pageClass, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsPageHero>(renderer, output, new PageHeroOptions { Css = Css, PageClass = pageClass, Attributes = attrs }, await ListRender.Inner(output));
    }
}

/// <summary>
/// A table of typed columns: <c>&lt;pems-data-table items="@Model.History" columns="@cols" empty="@Localizer["No history"]" dense="true" /&gt;</c> where <c>cols</c> is built with
/// <c>DataColumn.Of&lt;Row&gt;(header, row =&gt; value, kind)</c>. The page supplies every text; the table writes <c>table table-enterprise</c>.
/// </summary>
[HtmlTargetElement("pems-data-table")]
[PemsFacade(UiKeys.DataTable)]
public sealed class PemsDataTableTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    [HtmlAttributeName("items")] public System.Collections.IEnumerable? Items { get; set; }
    [HtmlAttributeName("columns")] public IReadOnlyList<DataColumn>? Columns { get; set; }
    /// <summary>The rows and the columns together (<c>DataRows.Of(items, c =&gt; c.Add(...))</c>); used instead of <c>items</c> and <c>columns</c>.</summary>
    [HtmlAttributeName("data")] public DataRows? Data { get; set; }
    [HtmlAttributeName("empty")] public string? Empty { get; set; }
    [HtmlAttributeName("dense")] public bool Dense { get; set; }
    /// <summary>The enterprise look (default true); <c>enterprise="false"</c> writes the plain table.</summary>
    [HtmlAttributeName("enterprise")] public bool Enterprise { get; set; } = true;
    [HtmlAttributeName("row-css")] public string? RowCss { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var (pageClass, attrs) = ListRender.Page(output);
        await ListRender.Replace<PemsDataTable>(renderer, output, new DataTableOptions
        {
            Items = Data?.Items ?? Items?.Cast<object>(), Columns = Data?.Columns ?? Columns, Empty = Empty, Dense = Dense, Enterprise = Enterprise, RowCss = RowCss,
            Css = string.IsNullOrWhiteSpace(pageClass) ? Css : string.Join(" ", new[] { Css, pageClass }.Where(x => !string.IsNullOrWhiteSpace(x))),
            Attributes = attrs
        });
    }
}
