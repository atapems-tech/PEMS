using Microsoft.AspNetCore.Components;
using PEMS.UI.Components;
using Xunit;

namespace PEMS.UI.Tests;

/// <summary>SCRUM-510: the form frame writes the page's attributes and content as given.</summary>
public sealed class FormFrameTests
{
    private static Task<string> Render(IReadOnlyDictionary<string, object>? attrs, string? pageClass = null) =>
        UiTestRenderer.Instance.RenderAsync<PemsFormFrame>(new Dictionary<string, object?>
        {
            [nameof(PemsFormFrame.Options)] = new FormFrameOptions { PageClass = pageClass, Attributes = attrs },
            [nameof(PemsFormFrame.ChildContent)] = (RenderFragment)(b => b.AddMarkupContent(0, "<p>x</p>"))
        });

    [Fact]
    public async Task A_post_form_without_action_gets_no_action_from_the_renderer()
    {
        var html = await Render(new Dictionary<string, object> { ["method"] = "post" });
        Assert.StartsWith("<form method=\"post\">", html);
        Assert.DoesNotContain("action=", html);
    }

    [Fact]
    public async Task An_empty_action_is_written_once()
    {
        var html = await Render(new Dictionary<string, object> { ["method"] = "post", ["action"] = "" });
        Assert.Equal("<form method=\"post\" action=\"\"><p>x</p></form>", html);
    }

    [Fact]
    public async Task A_page_action_class_and_content_are_kept_in_order()
    {
        var html = await Render(new Dictionary<string, object> { ["method"] = "post", ["action"] = "/Letter/Refer", ["id"] = "f1" }, "mt-2");
        Assert.Equal("<form class=\"mt-2\" method=\"post\" action=\"/Letter/Refer\" id=\"f1\"><p>x</p></form>", html);
    }
}
