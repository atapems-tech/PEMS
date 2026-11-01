using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.Components;

namespace PEMS.UI.TagHelpers;

/// <summary>
/// MVC facade for the button classes: <c>&lt;pems-button tone="neutral" size="small" outline="true" css="w-100"&gt;text&lt;/pems-button&gt;</c> becomes a
/// <c>button</c> (or an <c>a</c> with <c>as="a"</c>). The element keeps every other attribute the page wrote (type, id, href, data-*, onclick, disabled) and its content;
/// the classes come from <see cref="ButtonState"/>, the same code <see cref="PemsButton"/> uses, so no view writes <c>class="btn btn-*"</c> by hand.
/// A link can carry <c>asp-controller</c>, <c>asp-action</c>, <c>asp-route-*</c> and the other routing attributes of the built-in anchor helper (this class derives from it).
/// </summary>
[HtmlTargetElement("pems-button")]
public sealed class PemsButtonTagHelper(IHtmlGenerator generator) : AnchorTagHelper(generator)
{
    /// <summary>neutral, primary, danger, success, warning, info, light or dark (lower case in the page); "none" when the page passes the colour class in <c>css</c> (btn-pems, btn-link).</summary>
    [HtmlAttributeName("tone")] public string Tone { get; set; } = "primary";
    /// <summary>small, medium or large.</summary>
    [HtmlAttributeName("size")] public string Size { get; set; } = "medium";
    [HtmlAttributeName("outline")] public bool Outline { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }
    /// <summary>Writes the disabled attribute when true (a bool attribute value would print disabled="False").</summary>
    [HtmlAttributeName("disabled")] public bool Disabled { get; set; }
    /// <summary>The element to write: <c>button</c> (default), <c>a</c>, <c>label</c> or <c>span</c>.</summary>
    [HtmlAttributeName("as")] public string As { get; set; } = "button";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var options = new ButtonOptions
        {
            Label = "",
            Tone = Tone.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : Enum.Parse<Tone>(Tone, ignoreCase: true),
            Size = Enum.Parse<Size>(Size, ignoreCase: true),
            Outline = Outline,
            CssClass = Css
        };
        output.TagName = As;
        output.TagMode = TagMode.StartTagAndEndTag;
        // First, where the pages wrote it: page tests and scripts that look at the start tag keep working.
        output.Attributes.Insert(0, new TagHelperAttribute("class", ButtonState.From(options).Css));
        if (Disabled) output.Attributes.Add(new TagHelperAttribute("disabled", "disabled"));
    }
}
