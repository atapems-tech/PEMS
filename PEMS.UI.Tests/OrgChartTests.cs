using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PEMS.UI.Abstractions;
using PEMS.UI.Components;
using PEMS.UI.Manifest;

namespace PEMS.UI.Tests;

/// <summary>SCRUM-841: the organization chart component writes the shell the old Team/OrganizationChart view wrote (summary, vacancy filter, legend, discipline search, chart states, toolbar); the addresses and the texts are options.</summary>
public sealed class OrgChartTests
{
    private static string Render(string culture = "en", string? json = null)
    {
        var options = JsonSerializer.Deserialize<OrgChartOptions>(json ?? ComponentSamples.Get("org-chart")!.ToJsonString(), PemsJson.Options);
        return UiTestRenderer.Instance.RenderAsync("org-chart", new Dictionary<string, object?> { ["Options"] = options }, new CultureInfo(culture)).GetAwaiter().GetResult();
    }

    [Fact]
    public void The_root_carries_the_addresses_and_the_texts_the_script_needs()
    {
        var html = System.Net.WebUtility.HtmlDecode(Render());
        Assert.Contains("id=\"pemsOrgChartRoot\"", html);
        Assert.Contains("data-tree-url=\"/x/team-organization-tree\"", html);
        Assert.Contains("data-disciplines-url=\"/x/team-chart-disciplines\"", html);
        Assert.Contains("data-details-url-template=\"/p/team-details\"", html);
        foreach (var name in new[] { "empty", "error", "vacant", "positions", "members", "view-details", "filter-empty", "no-discipline", "all-disciplines" })
            Assert.Contains($"data-{name}-text=\"", html);
    }

    [Fact]
    public void The_summary_the_vacancy_filter_and_the_legend_are_there()
    {
        var html = Render();
        Assert.Matches(new Regex(@"<div[^>]*id=""orgChartSummary""[^>]*hidden"), html);   // hidden until a tree is loaded
        foreach (var id in new[] { "orgChartStatTotal", "orgChartStatFilled", "orgChartStatVacant", "orgChartStatOccupancy", "orgChartFilterFilled", "orgChartFilterVacant", "orgChartProgressFilled", "orgChartProgressVacant" })
            Assert.Contains($"id=\"{id}\"", html);
        Assert.Equal(3, Regex.Matches(html, "data-filter=\"(all|filled|vacant)\"").Count);
        Assert.Contains("data-filter=\"all\" aria-pressed=\"true\"", html);
        Assert.Equal(5, Regex.Matches(html, "class=\"pems-org-swatch\"").Count);
    }

    [Fact]
    public void The_discipline_search_the_chart_states_and_the_toolbar_are_there()
    {
        var html = Render();
        foreach (var id in new[] { "orgChartDisciplineSearch", "orgChartDisciplineFilter", "orgChartLoading", "orgChartError", "orgChartEmpty", "orgChartViewport", "orgChartCanvas", "orgChartExpandAll", "orgChartCollapseAll", "orgChartZoomIn", "orgChartZoomOut", "orgChartFit", "orgChartRetry" })
            Assert.Contains($"id=\"{id}\"", html);
        Assert.Equal(1, Regex.Matches(html, @"<option").Count);   // the choices come from the address, only "all" is written
    }

    [Fact]
    public void The_texts_follow_the_language()
    {
        Assert.Contains("بازگشت به تیم‌ها", System.Net.WebUtility.HtmlDecode(Render("fa")));
        Assert.Contains("Back to the teams", Render("en"));
    }
}
