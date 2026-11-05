using Microsoft.AspNetCore.Components;
using PEMS.Web.Models;

namespace PEMS.UI.Components;

// SCRUM-507: the nav frames (dropdown panel, tab strip, breadcrumb trail, table, list-group row, toast), the button frame, the chart
// host and the date field as registered components. The classes come from FrameState / ButtonState / ControlState (plain C#, unit
// tested); the page's own class is kept after the component's, every other attribute the page wrote follows in its order.

/// <summary>What every frame component shares: the page's own class and the other attributes it wrote on the tag.</summary>
public abstract record FrameOptions
{
    /// <summary>The class the page wrote on the tag, kept after the component's classes.</summary>
    public string? PageClass { get; init; }
    public IReadOnlyDictionary<string, object>? Attributes { get; init; }
}

/// <summary>Inputs of <see cref="PemsDropdownMenu"/>: the dropdown panel (a list, or a div for free markup); the page's items are the child content.</summary>
public sealed record DropdownMenuOptions : FrameOptions
{
    public bool End { get; init; }
    public string? Css { get; init; }
    /// <summary>ul (default) or div.</summary>
    public string As { get; init; } = "ul";
}

/// <summary>Inputs of <see cref="PemsNav"/>: the tab or pill strip.</summary>
public sealed record NavOptions : FrameOptions
{
    /// <summary>tabs (default) or pills.</summary>
    public string Kind { get; init; } = "tabs";
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsCrumbTrail"/>: the breadcrumb list frame.</summary>
public sealed record CrumbTrailOptions : FrameOptions
{
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsTableFrame"/>: the table element; thead and tbody are the child content.</summary>
public sealed record TableFrameOptions : FrameOptions
{
    /// <summary>enterprise adds table-enterprise.</summary>
    public string? Kind { get; init; }
    /// <summary>small adds table-sm.</summary>
    public string? Scale { get; init; }
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsListGroupItem"/>: one list-group row (a link, a div or a button).</summary>
public sealed record ListGroupItemOptions : FrameOptions
{
    public bool Action { get; init; }
    public bool Active { get; init; }
    public string? Css { get; init; }
    /// <summary>a (default), div or button.</summary>
    public string As { get; init; } = "a";
}

/// <summary>Inputs of <see cref="PemsToast"/>: the toast frame; body and close button are the child content.</summary>
public sealed record ToastOptions : FrameOptions
{
    public string Tone { get; init; } = "primary";
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsButtonFrame"/>: the button classes around the page's own content and attributes (the label-driven button is <see cref="PemsButton"/>).</summary>
public sealed record ButtonFrameOptions : FrameOptions
{
    /// <summary>neutral, primary, danger, success, warning, info, light or dark; "none" when the page passes the colour class in <see cref="Css"/>.</summary>
    public string Tone { get; init; } = "primary";
    public string Size { get; init; } = "medium";
    public bool Outline { get; init; }
    public string? Css { get; init; }
    public bool Disabled { get; init; }
    /// <summary>button (default), a, label or span.</summary>
    public string As { get; init; } = "button";
}

/// <summary>Inputs of <see cref="PemsChart"/>: the chart host (drawn by the Bklit island from <c>data-bk-chart</c>) and its accessible copy of the numbers.</summary>
public sealed record ChartOptions
{
    public BkChartSpec? Spec { get; init; }
}

/// <summary>The hidden input of a Jalali date field: the posted name and the ISO value.</summary>
public sealed record DateHidden
{
    public string Id { get; init; } = "";
    public string? Name { get; init; }
    public string Value { get; init; } = "";
    public bool Disabled { get; init; }
    public string? Form { get; init; }
}

/// <summary>Inputs of <see cref="PemsDate"/>: the visible input with every attribute already resolved (native date input, or the text input of the Jalali picker) and, for Jalali, the hidden input.</summary>
public sealed record DateOptions
{
    public IReadOnlyDictionary<string, object>? Attributes { get; init; }
    public DateHidden? Hidden { get; init; }
}

/// <summary>Small helpers the frame components share.</summary>
internal static class FrameMarkup
{
    /// <summary>The frame element: class first (the component's classes, then the page's), then the page's other attributes in their order, then the content.</summary>
    public static RenderFragment Element(string tag, string css, string? pageClass, IReadOnlyDictionary<string, object>? attributes, RenderFragment? content, bool disabled = false) => b =>
    {
        b.OpenElement(0, tag);
        b.AddAttribute(1, "class", string.IsNullOrWhiteSpace(pageClass) ? css : css + " " + pageClass.Trim());
        if (attributes is not null) b.AddMultipleAttributes(2, attributes);
        if (disabled) b.AddAttribute(3, "disabled", "disabled");
        if (content is not null) b.AddContent(4, content);
        b.CloseElement();
    };
}

// SCRUM-507: the bare form controls (input, select, textarea, label, validation summary). The built-in ASP.NET Core helper still computes the
// model-bound values (name, id, value, validation attributes, option list, message list); the component writes the element.

/// <summary>Inputs of <see cref="PemsInput"/>: a text-like input or a check box / radio / switch input.</summary>
public sealed record InputOptions : FrameOptions
{
    /// <summary>text (default) or check.</summary>
    public string Kind { get; init; } = "text";
    /// <summary>small or large; empty for the default size.</summary>
    public string? Scale { get; init; }
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsSelect"/>: the select element; the option list (from the page or from asp-items) is the child content.</summary>
public sealed record SelectOptions : FrameOptions
{
    public string? Scale { get; init; }
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsTextarea"/>: the textarea element; its text is the child content.</summary>
public sealed record TextareaOptions : FrameOptions
{
    public string? Scale { get; init; }
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsFormLabel"/>: the form label; its text is the child content (from the page or from the model metadata).</summary>
public sealed record FormLabelOptions : FrameOptions
{
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsValidationSummary"/>: the validation summary box (alert alert-danger); the message list is the child content.</summary>
public sealed record ValidationSummaryOptions : FrameOptions
{
    public string? Css { get; init; }
}
