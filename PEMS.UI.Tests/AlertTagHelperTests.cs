using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using PEMS.UI.Components;
using PEMS.UI.Rendering;
using PEMS.UI.TagHelpers;

namespace PEMS.UI.Tests;

/// <summary>SCRUM-481: <c>&lt;pems-alert&gt;</c> replaces the hand-written <c>alert alert-*</c> blocks; the markup it writes is what the pages wrote.</summary>
public sealed class AlertTagHelperTests
{
    private static async Task<string> Run(string tone, string? css, string inner, string? traceId = null)
    {
        await using var provider = new ServiceCollection().AddPemsUi().BuildServiceProvider();
        var helper = new PemsAlertTagHelper(provider.CreateScope().ServiceProvider.GetRequiredService<IPemsComponentRenderer>()) { Tone = tone, Css = css, TraceId = traceId };
        var context = new TagHelperContext("pems-alert", new TagHelperAttributeList(), new Dictionary<object, object>(), "a");
        var output = new TagHelperOutput("pems-alert", new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(inner)));
        await helper.ProcessAsync(context, output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Theory]
    [InlineData("danger", "py-2 px-3 small", "alert alert-danger py-2 px-3 small", "alert")]
    [InlineData("success", "py-2 px-3 small", "alert alert-success py-2 px-3 small", "status")]
    [InlineData("warning", null, "alert alert-warning", "alert")]
    [InlineData("info", "small mb-0", "alert alert-info small mb-0", "status")]
    public async Task Writes_the_alert_element_with_the_tone_and_the_extra_classes_the_page_gave(string tone, string? css, string expectedClass, string role)
    {
        var html = await Run(tone, css, "Saved <a href=\"/x\" class=\"alert-link\">view</a>");
        Assert.Equal($"<div class=\"{expectedClass}\" role=\"{role}\">Saved <a href=\"/x\" class=\"alert-link\">view</a></div>", Squash(html));
    }

    [Fact]
    public async Task A_trace_id_shows_as_code_for_support()
    {
        var html = await Run("danger", null, "Failed", "00-abc");
        Assert.Contains("<code dir=\"ltr\">00-abc</code>", html, StringComparison.Ordinal);
    }

    private static string Squash(string html) => System.Text.RegularExpressions.Regex.Replace(html, @">\s+<", "><").Trim();
}
