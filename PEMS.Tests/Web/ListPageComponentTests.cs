using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.TagHelpers;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-337: the list-page tag helpers of PEMS.UI on their own (no view, no request): texts come only from the page,
/// a tool appears only when the page gives it, chips and the empty state react to the page's state, and every icon tool
/// has a tool tip and a screen-reader label.
/// </summary>
public class ListPageComponentTests
{
    private static readonly PemsListTexts Texts = new(
        "Search", "Open search", "Close search", "Filters", "active filters", "Apply", "Reset", "Counts", "Excel",
        "Reset columns", "Total", "Remove filter", "Reset all", "Change the filters.", "Clear all");

    private static async Task<string> Run(TagHelper helper, string tag, string childHtml = "", bool insidePage = true)
    {
        var items = new Dictionary<object, object>();
        if (insidePage)
        {
            var page = new PemsListPageTagHelper { Texts = Texts };
            var pageContext = new TagHelperContext("pems-list-page", new TagHelperAttributeList(), items, "p");
            page.Init(pageContext);
        }
        var context = new TagHelperContext(tag, new TagHelperAttributeList(), items, "x");
        var output = new TagHelperOutput(tag, new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(childHtml)));
        helper.Init(context);
        await helper.ProcessAsync(context, output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task Texts_come_only_from_the_page_so_a_tag_outside_a_list_page_is_refused()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(new PemsListToolbarTagHelper { Heading = "Docs" }, "pems-list-toolbar", insidePage: false));
    }

    [Fact]
    public async Task Toolbar_shows_the_title_and_the_count_with_an_accessible_label()
    {
        var html = await Run(new PemsListToolbarTagHelper { Heading = "Documents", HeadingTitle = "All documents", Total = 42 }, "pems-list-toolbar", "<div class=\"lp-tools\"></div>");

        Assert.StartsWith("<div class=\"lp-toolbar\" data-lp-toolbar", html);
        Assert.Contains("<h2 title=\"All documents\">Documents</h2>", html);
        Assert.Matches("<span aria-label=\"Total: 42\" class=\"lp-total\">42</span>", html);
        Assert.Contains("<div class=\"lp-tools\"></div>", html);
    }

    [Fact]
    public async Task Search_form_starts_hidden_keeps_the_page_filters_and_uses_the_autocomplete_only_when_asked()
    {
        var html = await Run(new PemsListSearchTagHelper { Id = "f", Action = "/Docs", InputId = "q", Name = "Search", Value = "pump", SearchType = "Doc" },
            "pems-list-search", "<input type=\"hidden\" name=\"Status\" value=\"Approved\" />");

        Assert.Contains("hidden=\"hidden\"", html);
        Assert.Contains("data-lp-search", html);
        Assert.Contains("value=\"pump\"", html);
        Assert.Contains("data-pems-search=\"\"", html);
        Assert.Contains("data-search-type=\"Doc\"", html);
        Assert.Contains("name=\"Status\" value=\"Approved\"", html);
        Assert.Contains("data-lp-search-close", html);

        var plain = await Run(new PemsListSearchTagHelper { Id = "f", Action = "/Docs", InputId = "q" }, "pems-list-search");
        Assert.DoesNotContain("data-pems-search", plain);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, true)]
    public async Task Filters_dropdown_shows_the_count_badge_only_when_something_is_filtered(int count, bool badge)
    {
        var html = await Run(new PemsListFiltersTagHelper { FormId = "flt", Action = "/Docs", Count = count, ResetHref = "/Docs?Search=a" },
            "pems-list-filters", "<select name=\"Status\"></select>");

        Assert.Equal(badge, html.Contains("lp-badge"));
        Assert.Contains("id=\"fltBtn\"", html);
        Assert.Contains("lp-filter-pop", html);
        Assert.Matches("<form action=\"/Docs\" id=\"flt\" method=\"get\">", html);
        Assert.Contains("<div class=\"lp-fp-body\"><select name=\"Status\"></select></div>", html);
        Assert.Contains("href=\"/Docs?Search=a\"", html);
    }

    [Fact]
    public async Task Chips_render_nothing_without_a_chip_and_offer_reset_all_from_two_chips_on()
    {
        Assert.Equal("", await Run(new PemsListChipsTagHelper { ResetHref = "/x" }, "pems-list-chips", "   "));

        const string chip = "<li class=\"pems-filter-chip\"><span>A</span></li>";
        var one = await Run(new PemsListChipsTagHelper { ResetHref = "/x" }, "pems-list-chips", chip);
        Assert.Contains("class=\"lp-chips\"", one);
        Assert.DoesNotContain("Reset all", one);

        var two = await Run(new PemsListChipsTagHelper { ResetHref = "/x" }, "pems-list-chips", chip + chip);
        Assert.Contains(">Reset all</a>", two);
    }

    [Fact]
    public async Task A_chip_names_the_filter_and_links_to_the_list_without_it()
    {
        var html = await Run(new PemsListChipTagHelper { Label = "Status", Value = "Approved", RemoveHref = "/Docs?Area=U1" }, "pems-list-chip");

        Assert.Contains("<span>Status: Approved</span>", html);
        Assert.Contains("href=\"/Docs?Area=U1\"", html);
        Assert.Contains("aria-label=\"Remove filter: Status\"", html);
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, true, true)]
    public async Task Table_adds_the_empty_state_only_when_empty_and_clear_all_only_when_filtered(bool empty, bool filtered, bool hasEmpty, bool hasClear)
    {
        var html = await Run(new PemsListTableTagHelper { GridKey = "Docs", Empty = empty, Filtered = filtered, EmptyText = "No documents", ClearHref = "/Docs" },
            "pems-list-table", "<thead></thead><tbody></tbody>");

        Assert.Contains("lp-scroll", html);
        Assert.Matches("<table class=\"[^\"]*lp-table\" data-grid-key=\"Docs\"><thead></thead><tbody></tbody></table>", html);
        Assert.Equal(hasEmpty, html.Contains("class=\"lp-empty\""));
        Assert.Equal(hasClear, html.Contains(">Clear all</a>"));
    }

    [Fact]
    public async Task Every_icon_tool_has_a_tool_tip_and_a_screen_reader_label()
    {
        var tools = new (TagHelper Helper, string Tag)[]
        {
            (new PemsListToolsTagHelper { SearchFor = "f" }, "pems-list-tools"),
            (new PemsListKpiToggleTagHelper { Target = "k", StorageKey = "s" }, "pems-list-kpi-toggle"),
            (new PemsListExportTagHelper { Href = "/x" }, "pems-list-export"),
            (new PemsListResetColumnsTagHelper { GridKey = "Docs" }, "pems-list-reset-columns"),
        };
        foreach (var (helper, tag) in tools)
        {
            var html = await Run(helper, tag);
            var button = Regex.Match(html, "<(a|button) [^>]*class=\"lp-icon-btn[^\"]*\"[^>]*>(.*?)</\\1>", RegexOptions.Singleline);
            Assert.True(button.Success, tag);
            Assert.Matches("title=\"[^\"]+\"", button.Value);
            Assert.Contains("class=\"visually-hidden\"", button.Value);
        }
    }

    [Fact]
    public async Task Kpi_row_starts_hidden_and_the_toggle_remembers_the_choice_under_the_page_key()
    {
        // a Process-only tag keeps the page's child content (Razor renders it); here only the wrapper is checked
        var row = await Run(new PemsListKpisTagHelper { Id = "edKpis" }, "pems-list-kpis");
        Assert.StartsWith("<div class=\"lp-kpis\" id=\"edKpis\" hidden=\"hidden\">", row);

        var toggle = await Run(new PemsListKpiToggleTagHelper { Target = "edKpis", StorageKey = "pems.docs.kpis" }, "pems-list-kpi-toggle");
        Assert.Contains("aria-controls=\"edKpis\"", toggle);
        Assert.Contains("data-storage-key=\"pems.docs.kpis\"", toggle);
    }
}
