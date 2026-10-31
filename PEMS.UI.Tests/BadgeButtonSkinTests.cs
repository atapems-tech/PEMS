using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using PEMS.UI.Components;
using PEMS.UI.Rendering;
using PEMS.UI.TagHelpers;

namespace PEMS.UI.Tests;

/// <summary>SCRUM-53 / #31: Badge and Button write the markup the pages wrote by hand (Bootstrap skin, the default); the Pems skin keeps the library classes.</summary>
public sealed class BadgeButtonSkinTests
{
    private static async Task<string> Badge(string tone, string? css, string inner)
    {
        await using var provider = new ServiceCollection().AddPemsUi().BuildServiceProvider();
        var helper = new PemsBadgeTagHelper(provider.CreateScope().ServiceProvider.GetRequiredService<IPemsComponentRenderer>()) { Tone = tone, Css = css };
        var context = new TagHelperContext("pems-badge", new TagHelperAttributeList(), new Dictionary<object, object>(), "b");
        var output = new TagHelperOutput("pems-badge", new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(inner)));
        await helper.ProcessAsync(context, output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    // The class lists below are the ones the views carry today (counted from PEMS.Web/Views).
    [Theory]
    [InlineData("neutral", null, "badge bg-secondary")]
    [InlineData("danger", null, "badge bg-danger")]
    [InlineData("success", null, "badge bg-success")]
    [InlineData("warning", null, "badge bg-warning text-dark")]
    [InlineData("info", null, "badge bg-info text-dark")]
    [InlineData("light", null, "badge bg-light text-dark border")]
    [InlineData("dark", null, "badge bg-dark")]
    [InlineData("danger", "ms-1", "badge bg-danger ms-1")]
    public async Task The_tag_helper_writes_the_badge_classes_the_pages_wrote(string tone, string? css, string expected)
        => Assert.Equal($"<span class=\"{expected}\">Active <b>2</b></span>", await Badge(tone, css, "Active <b>2</b>"));

    [Fact]
    public async Task The_pems_skin_keeps_the_library_classes()
    {
        await using var provider = new ServiceCollection().AddPemsUi().BuildServiceProvider();
        var renderer = provider.CreateScope().ServiceProvider.GetRequiredService<IPemsComponentRenderer>();
        var html = await renderer.RenderAsync<PemsBadge>(new Dictionary<string, object?> { ["Options"] = new BadgeOptions { Label = "Active", Tone = Tone.Success, Skin = ComponentSkin.Pems } });
        Assert.Contains("class=\"pems-badge pems-badge--success\"", html);
    }

    [Theory]
    [InlineData(Tone.Neutral, Size.Small, true, null, "btn btn-sm btn-outline-secondary")]
    [InlineData(Tone.Primary, Size.Small, false, null, "btn btn-sm btn-pems-solid")]
    [InlineData(Tone.Primary, Size.Medium, false, "w-100", "btn btn-pems-solid w-100")]
    [InlineData(Tone.Primary, Size.Small, true, null, "btn btn-sm btn-outline-primary")]
    [InlineData(Tone.Danger, Size.Small, true, null, "btn btn-sm btn-outline-danger")]
    [InlineData(Tone.Success, Size.Small, false, null, "btn btn-sm btn-success")]
    [InlineData(Tone.Warning, Size.Medium, false, null, "btn btn-warning")]
    public void The_button_classes_are_the_ones_the_pages_wrote(Tone tone, Size size, bool outline, string? css, string expected)
        => Assert.Equal(expected, ButtonState.From(new ButtonOptions { Label = "Go", Tone = tone, Size = size, Outline = outline, CssClass = css }).Css);

    [Fact]
    public void The_pems_button_skin_keeps_the_library_classes()
        => Assert.Equal("pems-btn pems-btn--danger pems-btn--small", ButtonState.From(new ButtonOptions { Label = "Go", Tone = Tone.Danger, Size = Size.Small, Skin = ComponentSkin.Pems }).Css);
}
