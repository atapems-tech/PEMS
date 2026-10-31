using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.Components;
using PEMS.UI.Rendering;

namespace PEMS.UI.TagHelpers;

/// <summary>
/// MVC facade for <see cref="PemsBadge"/>: <c>&lt;pems-badge tone="warning" css="ms-1"&gt;page text&lt;/pems-badge&gt;</c>.
/// The text between the tags stays in the page (it is a status name or a count the page computed); the component supplies the element and its colour classes.
/// </summary>
[HtmlTargetElement("pems-badge")]
public sealed class PemsBadgeTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    /// <summary>neutral, primary, danger, success, warning, info, light or dark (written in lower case in the page).</summary>
    [HtmlAttributeName("tone")] public string Tone { get; set; } = "neutral";
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var inner = (await output.GetChildContentAsync()).GetContent();
        var html = await renderer.RenderAsync<PemsBadge>(new Dictionary<string, object?>
        {
            [nameof(PemsBadge.Options)] = new BadgeOptions { Label = "", Tone = Enum.Parse<Tone>(Tone, ignoreCase: true), CssClass = Css },
            [nameof(PemsBadge.ChildContent)] = (RenderFragment)(b => b.AddMarkupContent(0, inner))
        });
        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;
        // The component file ends in a newline; an inline badge must not add whitespace between it and the text around it.
        output.Content.SetHtmlContent(html.Trim());
    }
}
