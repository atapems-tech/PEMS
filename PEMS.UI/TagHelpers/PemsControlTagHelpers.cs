using PEMS.UI.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.Components;

namespace PEMS.UI.TagHelpers;

/// <summary>Shared by the three control facades: the classes in front of the tag, and the bound attributes put back when the built-in helper did not run.</summary>
internal static class ControlTagHelper
{
    public static ControlOptions Options(ControlKind kind, string? scale, string? css) => new()
    {
        Kind = kind,
        Size = scale?.ToLowerInvariant() switch { "small" => Size.Small, "large" => Size.Large, _ => Size.Medium },
        CssClass = css
    };

    /// <summary>
    /// The control classes go first, where the pages wrote them. A class the built-in helper added (input-validation-error) stays after them.
    /// </summary>
    public static void PutClassFirst(TagHelperOutput output, string css)
    {
        var existing = output.Attributes.TryGetAttribute("class", out var attribute) ? attribute.Value?.ToString() : null;
        output.Attributes.RemoveAll("class");
        output.Attributes.Insert(0, new TagHelperAttribute("class", string.IsNullOrWhiteSpace(existing) ? css : css + " " + existing.Trim()));
    }

    public static void Restore(TagHelperOutput output, params (string Name, string? Value)[] bound)
    {
        foreach (var (name, value) in bound)
            if (value is not null) output.Attributes.Add(new TagHelperAttribute(name, value));
    }
}

/// <summary>
/// MVC facade for the bare input classes: <c>&lt;pems-input name="qty" type="number" scale="small" css="w-auto" /&gt;</c> becomes an <c>input</c> with
/// <c>form-control form-control-sm w-auto</c> (<c>kind="check"</c>: <c>form-check-input</c>). It keeps every other attribute the page wrote.
/// With <c>asp-for</c> it derives from the built-in input helper, so model binding, ids, values and validation attributes are the same as before.
/// </summary>
[HtmlTargetElement("pems-input")]
[PemsFacade(FacadeKind.Direct)]
public sealed class PemsInputTagHelper(IHtmlGenerator generator) : InputTagHelper(generator)
{
    /// <summary>text (default) or check.</summary>
    [HtmlAttributeName("kind")] public string Kind { get; set; } = "text";
    /// <summary>small or large; empty for the default size.</summary>
    [HtmlAttributeName("scale")] public string? Scale { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (For is not null) base.Process(context, output);
        else ControlTagHelper.Restore(output, ("name", Name), ("type", InputTypeName), ("value", Value), ("form", FormName));
        output.TagName = "input";
        output.TagMode = TagMode.SelfClosing;
        var kind = Kind.Equals("check", StringComparison.OrdinalIgnoreCase) ? ControlKind.Check : ControlKind.Text;
        ControlTagHelper.PutClassFirst(output, ControlState.Css(ControlTagHelper.Options(kind, Scale, Css)));
    }
}

/// <summary>MVC facade for the bare select classes: <c>&lt;pems-select name="status" scale="small"&gt;options&lt;/pems-select&gt;</c>; <c>asp-for</c> and <c>asp-items</c> work as on a select.</summary>
[HtmlTargetElement("pems-select")]
[PemsFacade(FacadeKind.Direct)]
public sealed class PemsSelectTagHelper(IHtmlGenerator generator) : SelectTagHelper(generator)
{
    [HtmlAttributeName("scale")] public string? Scale { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (For is not null) base.Process(context, output);
        else ControlTagHelper.Restore(output, ("name", Name));
        output.TagName = "select";
        output.TagMode = TagMode.StartTagAndEndTag;
        ControlTagHelper.PutClassFirst(output, ControlState.Css(ControlTagHelper.Options(ControlKind.Select, Scale, Css)));
    }
}

/// <summary>MVC facade for the bare textarea classes: <c>&lt;pems-textarea name="note" rows="3"&gt;text&lt;/pems-textarea&gt;</c>.</summary>
[HtmlTargetElement("pems-textarea")]
[PemsFacade(FacadeKind.Direct)]
public sealed class PemsTextAreaTagHelper(IHtmlGenerator generator) : TextAreaTagHelper(generator)
{
    [HtmlAttributeName("scale")] public string? Scale { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (For is not null) base.Process(context, output);
        else ControlTagHelper.Restore(output, ("name", Name));
        output.TagName = "textarea";
        output.TagMode = TagMode.StartTagAndEndTag;
        ControlTagHelper.PutClassFirst(output, ControlState.Css(ControlTagHelper.Options(ControlKind.Text, Scale, Css)));
    }
}

/// <summary>
/// MVC facade for the form label: <c>&lt;pems-label css="small" for="qty"&gt;Quantity&lt;/pems-label&gt;</c> becomes <c>&lt;label class="form-label small" for="qty"&gt;</c>.
/// With <c>asp-for</c> it derives from the built-in label helper, so the text comes from the model metadata when the tag has no content.
/// </summary>
[HtmlTargetElement("pems-label")]
[PemsFacade(FacadeKind.Direct)]
public sealed class PemsLabelTagHelper(IHtmlGenerator generator) : LabelTagHelper(generator)
{
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (For is not null) await base.ProcessAsync(context, output);
        output.TagName = "label";
        output.TagMode = TagMode.StartTagAndEndTag;
        ControlTagHelper.PutClassFirst(output, ControlState.LabelCss(Css));
    }
}

/// <summary>
/// MVC facade for the validation summary box: <c>&lt;pems-validation-summary asp-validation-summary="All" css="py-2 px-3 small"&gt;</c> writes the
/// <c>alert alert-danger</c> classes in front of the ones the built-in helper adds (validation-summary-errors / -valid) and keeps every other attribute.
/// </summary>
[HtmlTargetElement("pems-validation-summary")]
[PemsFacade(FacadeKind.Direct)]
public sealed class PemsValidationSummaryTagHelper(IHtmlGenerator generator) : ValidationSummaryTagHelper(generator)
{
    [HtmlAttributeName("css")] public string? Css { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        base.Process(context, output);
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        ControlTagHelper.PutClassFirst(output, string.IsNullOrWhiteSpace(Css) ? "alert alert-danger" : "alert alert-danger " + Css.Trim());
    }
}
