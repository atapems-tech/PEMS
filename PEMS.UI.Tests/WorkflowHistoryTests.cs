using System.Text.Json.Nodes;
using PEMS.UI.Components;
using PEMS.UI.Manifest;

namespace PEMS.UI.Tests;

/// <summary>SCRUM-58: the workflow history timeline draws newest first, in the language of the request, and its options are checked by the manifest.</summary>
public sealed class WorkflowHistoryTests
{
    private static WorkflowHistoryOptions Sample() => System.Text.Json.JsonSerializer.Deserialize<WorkflowHistoryOptions>(ComponentSamples.Get("workflow-history")!.ToJsonString(), PEMS.UI.Abstractions.PemsJson.Options)!;

    private static string Render(WorkflowHistoryOptions o, string culture)
        => UiTestRenderer.Instance.RenderAsync("workflow-history", new Dictionary<string, object?> { ["Options"] = o }, new System.Globalization.CultureInfo(culture)).GetAwaiter().GetResult();

    [Fact]
    public void Entries_are_listed_newest_first_with_tone_route_actor_and_comment()
    {
        var html = System.Net.WebUtility.HtmlDecode(Render(Sample(), "en"));
        Assert.True(html.IndexOf("Approved", StringComparison.Ordinal) < html.IndexOf("Returned", StringComparison.Ordinal));
        Assert.Contains("pems-wfh__item--success", html);
        Assert.Contains("pems-wfh__item--warning", html);
        Assert.Contains("Review → Approval", html);
        Assert.Contains("A. Rezaei", html);
        Assert.Contains("<blockquote class=\"pems-wfh__comment\">Looks good</blockquote>", html);
    }

    [Fact]
    public void An_empty_history_writes_the_empty_text_in_the_language_of_the_request()
    {
        var o = Sample() with { Entries = [] };
        Assert.Contains("No history yet", Render(o, "en"));
        Assert.Contains("هنوز تاریخچه‌ای نیست", System.Net.WebUtility.HtmlDecode(Render(o, "fa")));
    }

    [Fact]
    public void The_options_are_checked_by_the_manifest()
    {
        var catalog = new ComponentManifestCatalog(new PEMS.UI.Rendering.PemsComponentRegistry());
        var ok = ComponentSamples.Get("workflow-history")!.ToJsonString();
        Assert.True(catalog.Validate("workflow-history", ok).IsValid);
        var bad = JsonNode.Parse(ok)!.AsObject();
        bad["nonsense"] = 1;
        Assert.False(catalog.Validate("workflow-history", bad.ToJsonString()).IsValid);
    }
}
