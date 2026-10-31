using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PEMS.UI.TagHelpers;

// SCRUM-337: the list page (frame, toolbar, filters dropdown, KPI row, chips, scrolling table, empty state, footer) with the
// look of the correspondence and Engineering Documents lists. These tag helpers own the structure, the classes, the ARIA and the
// data-* hooks of pems-list.js; the page keeps its own columns, filters, rows, actions, permissions and paging.
// Styles: _content/PEMS.UI/pems-list.css. Script: _content/PEMS.UI/pems-list.js. Usage: docs/design/LIST_PAGE_COMPONENT.md.

internal static class PemsList
{
    public const string TextsKey = "PEMS.UI.PemsListTexts";

    public static PemsListTexts Texts(TagHelperContext context)
        => context.Items.TryGetValue(TextsKey, out var t) && t is PemsListTexts texts
            ? texts
            : throw new InvalidOperationException("List-page tags must be inside <pems-list-page texts=\"...\">.");

    public static TagBuilder Icon(string bootstrapIcon)
    {
        var i = new TagBuilder("i");
        i.AddCssClass("bi " + bootstrapIcon);
        i.Attributes["aria-hidden"] = "true";
        return i;
    }

    public static TagBuilder Hidden(string text)
    {
        var s = new TagBuilder("span");
        s.AddCssClass("visually-hidden");
        s.InnerHtml.Append(text);
        return s;
    }

    /// <summary>An icon-only tool button or link, with its label as tool tip and for screen readers.</summary>
    public static TagBuilder IconTool(string tag, string icon, string label)
    {
        var b = new TagBuilder(tag);
        b.AddCssClass("lp-icon-btn");
        if (tag == "button") b.Attributes["type"] = "button";
        b.Attributes["title"] = label;
        b.InnerHtml.AppendHtml(Icon(icon));
        b.InnerHtml.AppendHtml(Hidden(label));
        return b;
    }

    public static void Become(TagHelperOutput output, string tag, string cssClass)
    {
        output.TagName = tag;
        output.TagMode = TagMode.StartTagAndEndTag;
        // a class written on the tag by the page is kept after the component's own classes
        var own = output.Attributes.TryGetAttribute("class", out var existing) ? " " + existing.Value : "";
        output.Attributes.SetAttribute("class", cssClass + own);
    }
}

/// <summary>Frame of the whole page: fills the window, the table scrolls inside. Sets the texts for every child tag.</summary>
[HtmlTargetElement("pems-list-page")]
public sealed class PemsListPageTagHelper : TagHelper
{
    [HtmlAttributeName("texts")] public PemsListTexts Texts { get; set; } = default!;

    public override void Init(TagHelperContext context) => context.Items[PemsList.TextsKey] = Texts;

    public override void Process(TagHelperContext context, TagHelperOutput output) => PemsList.Become(output, "div", "lp-page");
}

/// <summary>The card that holds toolbar, KPI row, chips and grid.</summary>
[HtmlTargetElement("pems-list-panel", ParentTag = "pems-list-page")]
public sealed class PemsListPanelTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output) => PemsList.Become(output, "div", "lp-panel");
}

/// <summary>The one toolbar: title and count, then the page's search form and tools (child content).</summary>
[HtmlTargetElement("pems-list-toolbar")]
public sealed class PemsListToolbarTagHelper : TagHelper
{
    [HtmlAttributeName("heading")] public string Heading { get; set; } = "";
    [HtmlAttributeName("heading-title")] public string? HeadingTitle { get; set; }
    [HtmlAttributeName("total")] public int? Total { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var texts = PemsList.Texts(context);
        PemsList.Become(output, "div", "lp-toolbar");
        output.Attributes.SetAttribute("data-lp-toolbar", "");

        var head = new TagBuilder("div");
        head.AddCssClass("lp-tabs lp-head");
        var h2 = new TagBuilder("h2");
        if (!string.IsNullOrEmpty(HeadingTitle)) h2.Attributes["title"] = HeadingTitle;
        h2.InnerHtml.Append(Heading);
        head.InnerHtml.AppendHtml(h2);
        if (Total.HasValue)
        {
            var total = new TagBuilder("span");
            total.AddCssClass("lp-total");
            total.Attributes["aria-label"] = $"{texts.Total}: {Total}";
            total.InnerHtml.Append(Total.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            head.InnerHtml.AppendHtml(total);
        }

        output.PreContent.AppendHtml(head);
        output.Content.AppendHtml(await output.GetChildContentAsync());
    }
}

/// <summary>
/// The search form that opens over the title. Child content = hidden inputs that keep the active filters.
/// With <c>search-type</c> the input is the shared autocomplete; picking a result opens it, Enter submits the form.
/// </summary>
[HtmlTargetElement("pems-list-search")]
public sealed class PemsListSearchTagHelper : TagHelper
{
    [HtmlAttributeName("id")] public string Id { get; set; } = "";
    [HtmlAttributeName("action")] public string Action { get; set; } = "";
    [HtmlAttributeName("input-id")] public string InputId { get; set; } = "";
    [HtmlAttributeName("name")] public string Name { get; set; } = "search";
    [HtmlAttributeName("value")] public string? Value { get; set; }
    [HtmlAttributeName("placeholder")] public string? Placeholder { get; set; }
    [HtmlAttributeName("search-type")] public string? SearchType { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var texts = PemsList.Texts(context);
        PemsList.Become(output, "form", "lp-search");
        output.Attributes.SetAttribute("id", Id);
        output.Attributes.SetAttribute("method", "get");
        output.Attributes.SetAttribute("action", Action);
        output.Attributes.SetAttribute("role", "search");
        output.Attributes.SetAttribute("data-lp-search", "");
        // pems-list.js opens it on load when a search is active, so the user sees what filters the list
        output.Attributes.SetAttribute("hidden", "hidden");

        var input = new TagBuilder("input");
        input.TagRenderMode = TagRenderMode.SelfClosing;
        input.Attributes["type"] = "search";
        input.Attributes["id"] = InputId;
        input.Attributes["name"] = Name;
        input.Attributes["value"] = Value ?? "";
        input.Attributes["placeholder"] = Placeholder ?? "";
        input.Attributes["aria-label"] = texts.Search;
        input.Attributes["autocomplete"] = "off";
        if (!string.IsNullOrEmpty(SearchType))
        {
            input.Attributes["data-pems-search"] = "";
            input.Attributes["data-search-type"] = SearchType;
        }

        var submit = new TagBuilder("button");
        submit.Attributes["type"] = "submit";
        submit.AddCssClass("btn btn-sm btn-pems-solid");
        submit.InnerHtml.Append(texts.Search);

        var close = PemsList.IconTool("button", "bi-x-lg", texts.SearchClose);
        close.Attributes["data-lp-search-close"] = "";

        output.Content.AppendHtml(PemsList.Icon("bi-search"));
        output.Content.AppendHtml(input);
        output.Content.AppendHtml(await output.GetChildContentAsync());
        output.Content.AppendHtml(submit);
        output.Content.AppendHtml(close);
    }
}

/// <summary>Row of icon tools at the end of the toolbar. <c>search-for</c> adds the button that opens the search form.</summary>
[HtmlTargetElement("pems-list-tools")]
public sealed class PemsListToolsTagHelper : TagHelper
{
    [HtmlAttributeName("search-for")] public string? SearchFor { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var texts = PemsList.Texts(context);
        PemsList.Become(output, "div", "lp-tools");
        if (!string.IsNullOrEmpty(SearchFor))
        {
            var open = PemsList.IconTool("button", "bi-search", texts.SearchOpen);
            open.Attributes["data-lp-search-open"] = "";
            open.Attributes["aria-controls"] = SearchFor;
            open.Attributes["aria-expanded"] = "false";
            output.PreContent.AppendHtml(open);
        }
        output.Content.AppendHtml(await output.GetChildContentAsync());
    }
}

/// <summary>
/// Filters in a dropdown: funnel button with the active-filter count, then a GET form whose fields are the child content,
/// with Reset and Apply. The page decides which filters exist and what "active" means (<c>count</c>).
/// </summary>
[HtmlTargetElement("pems-list-filters")]
public sealed class PemsListFiltersTagHelper : TagHelper
{
    [HtmlAttributeName("form-id")] public string FormId { get; set; } = "";
    [HtmlAttributeName("action")] public string Action { get; set; } = "";
    [HtmlAttributeName("count")] public int Count { get; set; }
    [HtmlAttributeName("reset-href")] public string? ResetHref { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var texts = PemsList.Texts(context);
        PemsList.Become(output, "div", "dropdown");

        var button = PemsList.IconTool("button", "bi-funnel", texts.Filters);
        button.Attributes["id"] = FormId + "Btn";
        button.Attributes["data-bs-toggle"] = "dropdown";
        button.Attributes["data-bs-auto-close"] = "outside";
        button.Attributes["aria-expanded"] = "false";
        if (Count > 0)
        {
            var badge = new TagBuilder("span");
            badge.AddCssClass("lp-badge");
            badge.InnerHtml.Append(Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            badge.InnerHtml.AppendHtml(PemsList.Hidden(" " + texts.ActiveFilters));
            button.InnerHtml.AppendHtml(badge);
        }

        var body = new TagBuilder("div");
        body.AddCssClass("lp-fp-body");
        body.InnerHtml.AppendHtml(await output.GetChildContentAsync());

        var foot = new TagBuilder("div");
        foot.AddCssClass("lp-fp-foot");
        var reset = new TagBuilder("a");
        reset.Attributes["href"] = ResetHref ?? Action;            // without a reset target: the list without these filters
        reset.AddCssClass("btn btn-sm btn-link");
        reset.InnerHtml.Append(texts.ResetFilters);
        foot.InnerHtml.AppendHtml(reset);
        var apply = new TagBuilder("button");
        apply.Attributes["type"] = "submit";
        apply.AddCssClass("btn btn-sm btn-pems-solid");
        apply.InnerHtml.AppendHtml(PemsList.Icon("bi-funnel"));
        apply.InnerHtml.Append(" " + texts.ApplyFilters);
        foot.InnerHtml.AppendHtml(apply);

        var form = new TagBuilder("form");
        form.Attributes["method"] = "get";
        form.Attributes["action"] = Action;
        form.Attributes["id"] = FormId;
        form.InnerHtml.AppendHtml(body);
        form.InnerHtml.AppendHtml(foot);

        var menu = new TagBuilder("div");
        menu.AddCssClass("dropdown-menu dropdown-menu-end lp-filter-pop p-0");
        menu.InnerHtml.AppendHtml(form);

        output.Content.AppendHtml(button);
        output.Content.AppendHtml(menu);
    }
}

/// <summary>Button that shows/hides the KPI row (<c>target</c> = its id); the choice is kept per browser under <c>storage-key</c>.</summary>
[HtmlTargetElement("pems-list-kpi-toggle", TagStructure = TagStructure.WithoutEndTag)]
public sealed class PemsListKpiToggleTagHelper : TagHelper
{
    [HtmlAttributeName("target")] public string Target { get; set; } = "";
    [HtmlAttributeName("storage-key")] public string StorageKey { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var b = PemsList.IconTool("button", "bi-bar-chart-line", PemsList.Texts(context).Kpis);
        b.Attributes["data-lp-kpi-toggle"] = "";
        b.Attributes["aria-controls"] = Target;
        b.Attributes["aria-expanded"] = "false";
        b.Attributes["data-storage-key"] = StorageKey;
        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetHtmlContent(b);
    }
}

/// <summary>Excel export of the filtered list (the page builds the link with its filters). <c>label</c> (W2) replaces the
/// default text when the export is not the filtered list (for example an export of every row).</summary>
[HtmlTargetElement("pems-list-export", TagStructure = TagStructure.WithoutEndTag)]
public sealed class PemsListExportTagHelper : TagHelper
{
    [HtmlAttributeName("href")] public string Href { get; set; } = "";
    [HtmlAttributeName("label")] public string? Label { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var text = PemsList.Texts(context).Export;
        var a = PemsList.IconTool("a", "bi-file-earmark-excel", string.IsNullOrEmpty(Label) ? text : Label);
        a.Attributes["href"] = Href;
        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetHtmlContent(a);
    }
}

/// <summary>Resets the stored column widths of the grid (hidden on phones, where columns are not resized).</summary>
[HtmlTargetElement("pems-list-reset-columns", TagStructure = TagStructure.WithoutEndTag)]
public sealed class PemsListResetColumnsTagHelper : TagHelper
{
    [HtmlAttributeName("grid-key")] public string GridKey { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var b = PemsList.IconTool("button", "bi-arrow-counterclockwise", PemsList.Texts(context).ResetColumns);
        b.AddCssClass("lp-hide-xs");
        b.Attributes["data-lp-reset-columns"] = GridKey;
        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetHtmlContent(b);
    }
}

/// <summary>The primary "New ..." action; the label collapses to the icon on phones. <c>href</c> opens a page;
/// <c>modal</c> (an element id, W2) opens the page's own Bootstrap modal instead.</summary>
[HtmlTargetElement("pems-list-new", TagStructure = TagStructure.WithoutEndTag)]
public sealed class PemsListNewTagHelper : TagHelper
{
    [HtmlAttributeName("href")] public string Href { get; set; } = "";
    [HtmlAttributeName("modal")] public string? Modal { get; set; }
    [HtmlAttributeName("text")] public string Text { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        TagBuilder a;
        if (!string.IsNullOrEmpty(Modal))
        {
            a = new TagBuilder("button");
            a.Attributes["type"] = "button";
            a.Attributes["data-bs-toggle"] = "modal";
            a.Attributes["data-bs-target"] = "#" + Modal;
        }
        else
        {
            a = new TagBuilder("a");
            a.Attributes["href"] = Href;
        }
        a.AddCssClass("btn btn-sm btn-pems-solid lp-compose");
        a.InnerHtml.AppendHtml(PemsList.Icon("bi-plus-lg"));
        a.InnerHtml.Append(" ");
        var label = new TagBuilder("span");
        label.AddCssClass("lp-compose-label");
        label.InnerHtml.Append(Text);
        a.InnerHtml.AppendHtml(label);
        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetHtmlContent(a);
    }
}

/// <summary>The KPI row; hidden until the KPI toggle shows it. Child content = the page's KPI chips.</summary>
[HtmlTargetElement("pems-list-kpis")]
public sealed class PemsListKpisTagHelper : TagHelper
{
    [HtmlAttributeName("id")] public string Id { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        PemsList.Become(output, "div", "lp-kpis");
        output.Attributes.SetAttribute("id", Id);
        output.Attributes.SetAttribute("hidden", "hidden");
    }
}

/// <summary>Active-filter chips; renders nothing when there is no chip. <c>reset-href</c> adds "reset all" from two chips on.</summary>
[HtmlTargetElement("pems-list-chips")]
public sealed class PemsListChipsTagHelper : TagHelper
{
    [HtmlAttributeName("reset-href")] public string? ResetHref { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var texts = PemsList.Texts(context);
        var chips = await output.GetChildContentAsync();
        var html = chips.GetContent();
        if (string.IsNullOrWhiteSpace(html)) { output.SuppressOutput(); return; }

        PemsList.Become(output, "div", "lp-chips");
        var ul = new TagBuilder("ul");
        ul.AddCssClass("pems-filter-chips list-unstyled mb-0");
        ul.Attributes["aria-label"] = texts.ActiveFilters;
        ul.InnerHtml.AppendHtml(chips);
        output.Content.SetHtmlContent(ul);

        var count = System.Text.RegularExpressions.Regex.Matches(html, "class=\"pems-filter-chip\"").Count;
        if (!string.IsNullOrEmpty(ResetHref) && count > 1)
        {
            var reset = new TagBuilder("a");
            reset.Attributes["href"] = ResetHref;
            reset.AddCssClass("small");
            reset.InnerHtml.Append(texts.ResetAll);
            output.Content.AppendHtml(reset);
        }
    }
}

/// <summary>One active filter: "label: value" with a link that removes it.</summary>
[HtmlTargetElement("pems-list-chip", TagStructure = TagStructure.WithoutEndTag)]
public sealed class PemsListChipTagHelper : TagHelper
{
    [HtmlAttributeName("label")] public string Label { get; set; } = "";
    [HtmlAttributeName("value")] public string? Value { get; set; }
    [HtmlAttributeName("remove-href")] public string RemoveHref { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var texts = PemsList.Texts(context);
        var li = new TagBuilder("li");
        li.AddCssClass("pems-filter-chip");
        var span = new TagBuilder("span");
        span.InnerHtml.Append(string.IsNullOrEmpty(Value) ? Label : $"{Label}: {Value}");
        var a = new TagBuilder("a");
        a.Attributes["href"] = RemoveHref;
        a.Attributes["title"] = texts.RemoveFilter;
        a.Attributes["aria-label"] = $"{texts.RemoveFilter}: {Label}";
        a.InnerHtml.AppendHtml(PemsList.Icon("bi-x"));
        li.InnerHtml.AppendHtml(span);
        li.InnerHtml.AppendHtml(a);
        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetHtmlContent(li);
    }
}

/// <summary>Holds the scrolling table and the footer.</summary>
[HtmlTargetElement("pems-list-grid")]
public sealed class PemsListGridTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output) => PemsList.Become(output, "div", "lp-tablewrap");
}

/// <summary>
/// The table, scrolling inside the frame with a sticky header. Child content = thead and tbody. <c>grid-key</c> turns on the stored
/// column widths (pems-list.js). With <c>empty="true"</c> the empty state follows the table; when the list is <c>filtered</c> it also
/// says so and offers "clear all" (<c>clear-href</c>, the unfiltered list).
/// </summary>
[HtmlTargetElement("pems-list-table")]
public sealed class PemsListTableTagHelper : TagHelper
{
    [HtmlAttributeName("grid-key")] public string? GridKey { get; set; }
    [HtmlAttributeName("empty")] public bool Empty { get; set; }
    [HtmlAttributeName("empty-text")] public string? EmptyText { get; set; }
    [HtmlAttributeName("filtered")] public bool Filtered { get; set; }
    [HtmlAttributeName("clear-href")] public string? ClearHref { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var texts = PemsList.Texts(context);
        PemsList.Become(output, "div", "table-responsive pems-grid-scroll table-scroll-area lp-scroll");

        var table = new TagBuilder("table");
        table.AddCssClass("table table-enterprise pems-grid-table pems-compact-rows mb-0 lp-table");
        if (!string.IsNullOrEmpty(GridKey)) table.Attributes["data-grid-key"] = GridKey;
        table.InnerHtml.AppendHtml(await output.GetChildContentAsync());
        output.Content.SetHtmlContent(table);

        if (!Empty) return;
        var empty = new TagBuilder("div");
        empty.AddCssClass("lp-empty");
        empty.InnerHtml.AppendHtml(PemsList.Icon("bi-inbox"));
        var strong = new TagBuilder("strong");
        strong.InnerHtml.Append(EmptyText ?? "");
        empty.InnerHtml.AppendHtml(strong);
        if (Filtered)
        {
            var hint = new TagBuilder("span");
            hint.InnerHtml.Append(texts.NoResultsHint);
            var clear = new TagBuilder("a");
            clear.Attributes["href"] = ClearHref ?? "";
            clear.AddCssClass("btn btn-sm btn-outline-secondary mt-2");
            clear.InnerHtml.Append(texts.ClearAll);
            empty.InnerHtml.AppendHtml(hint);
            empty.InnerHtml.AppendHtml(clear);
        }
        output.Content.AppendHtml(empty);
    }
}

/// <summary>Footer under the table (the pager).</summary>
[HtmlTargetElement("pems-list-footer")]
public sealed class PemsListFooterTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output) => PemsList.Become(output, "div", "lp-footer");
}

/// <summary>
/// Bulk-action bar (SCRUM-337 W1): hidden until at least one row is ticked. Rows tick with <c>input[data-lp-select]</c> (value = record
/// id), the header with <c>input[data-lp-select-all]</c>. Child content = the page's bulk actions; every <c>form</c> inside receives the
/// ticked ids as hidden inputs named <c>name</c> when it is submitted. <c>count-text</c> is the localised count, "{0}" = number ticked.
/// </summary>
[HtmlTargetElement("pems-list-bulk")]
public sealed class PemsListBulkTagHelper : TagHelper
{
    [HtmlAttributeName("count-text")] public string CountText { get; set; } = "{0}";
    [HtmlAttributeName("name")] public string Name { get; set; } = "ids";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        PemsList.Become(output, "div", "lp-bulk");
        output.Attributes.SetAttribute("data-lp-bulk", Name);
        output.Attributes.SetAttribute("data-count-text", CountText);
        output.Attributes.SetAttribute("hidden", "hidden");
        var count = new TagBuilder("span");
        count.AddCssClass("lp-bulk-count");
        count.Attributes["data-lp-bulk-count"] = "";
        count.Attributes["aria-live"] = "polite";
        output.PreContent.AppendHtml(count);
    }
}

/// <summary>
/// A toolbar tool that opens a dropdown of the page's own items (SCRUM-337 W1), e.g. quick filters and saved views. Child content =
/// the <c>li</c> items (<c>dropdown-item</c>, <c>dropdown-header</c>, <c>dropdown-divider</c>). <c>icon</c> is a Bootstrap icon,
/// <c>label</c> the localised tool tip and accessible name.
/// </summary>
[HtmlTargetElement("pems-list-menu")]
public sealed class PemsListMenuTagHelper : TagHelper
{
    [HtmlAttributeName("id")] public string Id { get; set; } = "";
    [HtmlAttributeName("icon")] public string Icon { get; set; } = "bi-three-dots";
    [HtmlAttributeName("label")] public string Label { get; set; } = "";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        PemsList.Become(output, "div", "dropdown");
        output.Attributes.RemoveAll("id");
        var button = PemsList.IconTool("button", Icon, Label);
        if (!string.IsNullOrEmpty(Id)) button.Attributes["id"] = Id;
        button.Attributes["data-bs-toggle"] = "dropdown";
        button.Attributes["aria-expanded"] = "false";

        var menu = new TagBuilder("ul");
        menu.AddCssClass("dropdown-menu dropdown-menu-end lp-menu");
        if (!string.IsNullOrEmpty(Id)) menu.Attributes["aria-labelledby"] = Id;
        menu.InnerHtml.AppendHtml(await output.GetChildContentAsync());

        output.Content.AppendHtml(button);
        output.Content.AppendHtml(menu);
    }
}
