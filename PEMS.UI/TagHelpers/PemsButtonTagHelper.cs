using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.Components;

namespace PEMS.UI.TagHelpers;

/// <summary>
/// MVC facade for the button classes: <c>&lt;pems-button tone="neutral" size="small" outline="true" css="w-100"&gt;text&lt;/pems-button&gt;</c> becomes a
/// <c>button</c> (or an <c>a</c> with <c>as="a"</c>). The element keeps every other attribute the page wrote (type, id, href, data-*, onclick, disabled) and its content;
/// the classes come from <see cref="ButtonState"/>, the same code <see cref="PemsButton"/> uses, so no view writes <c>class="btn btn-*"</c> by hand.
/// </summary>
[HtmlTargetElement("pems-button")]
public sealed class PemsButtonTagHelper : TagHelper
{
    /// <summary>neutral, primary, danger, success, warning, info, light or dark (lower case in the page).</summary>
    [HtmlAttributeName("tone")] public string Tone { get; set; } = "primary";
    /// <summary>small, medium or large.</summary>
    [HtmlAttributeName("size")] public string Size { get; set; } = "medium";
    [HtmlAttributeName("outline")] public bool Outline { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }
    /// <summary>The element to write: <c>button</c> (default) or <c>a</c>.</summary>
    [HtmlAttributeName("as")] public string As { get; set; } = "button";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var options = new ButtonOptions
        {
            Label = "",
            Tone = Enum.Parse<Tone>(Tone, ignoreCase: true),
            Size = Enum.Parse<Size>(Size, ignoreCase: true),
            Outline = Outline,
            CssClass = Css
        };
        output.TagName = As;
        output.TagMode = TagMode.StartTagAndEndTag;
        // First, where the pages wrote it: page tests and scripts that look at the start tag keep working.
        output.Attributes.Insert(0, new TagHelperAttribute("class", ButtonState.From(options).Css));
    }
}
