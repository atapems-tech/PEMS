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
    /// <summary>The element: label (default), or span / legend for a caption that labels no single control.</summary>
    public string As { get; init; } = "label";
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsValidationSummary"/>: the validation summary box (alert alert-danger); the message list is the child content.</summary>
public sealed record ValidationSummaryOptions : FrameOptions
{
    public string? Css { get; init; }
}

// SCRUM-508: the Bootstrap icon as a registered component.

/// <summary>Inputs of <see cref="PemsIcon"/>: a Bootstrap icon (<c>&lt;i class="bi bi-gear"&gt;</c>); the page's own attributes (aria-hidden, title, data-*) are kept.</summary>
public sealed record IconOptions : FrameOptions
{
    /// <summary>The icon class, e.g. <c>bi-gear</c>.</summary>
    public string? Name { get; init; }
    /// <summary>Other classes the page needs (spacing, colour).</summary>
    public string? Css { get; init; }
}

// SCRUM-508: the page grid (Bootstrap row and column) as registered components. They write today's classes; the page says "6 columns from md up",
// not "col-md-6". Extra classes the page needs stay in Css.

/// <summary>Inputs of <see cref="PemsRow"/>: a grid row; the columns are the child content.</summary>
public sealed record RowOptions : FrameOptions
{
    /// <summary>Gutter on both axes (g-N), 0 to 5.</summary>
    public string? Gap { get; init; }
    /// <summary>Horizontal gutter (gx-N).</summary>
    public string? GapX { get; init; }
    /// <summary>Vertical gutter (gy-N).</summary>
    public string? GapY { get; init; }
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsCol"/>: a grid column. Each breakpoint takes a width 1 to 12, "auto", or "" (equal width).</summary>
public sealed record ColOptions : FrameOptions
{
    /// <summary>The width on the smallest screens (col, col-6, col-auto).</summary>
    public string? Span { get; init; }
    public string? Sm { get; init; }
    public string? Md { get; init; }
    public string? Lg { get; init; }
    public string? Xl { get; init; }
    public string? Xxl { get; init; }
    public string? Css { get; init; }
}

// SCRUM-508: the check / switch row and the page-number pager.

/// <summary>Inputs of <see cref="PemsCheckRow"/>: the wrapper of a check box, radio or switch and its label (<c>form-check</c>, <c>form-switch</c>); the input and label are the child content.</summary>
public sealed record CheckRowOptions : FrameOptions
{
    /// <summary>A switch (<c>form-check form-switch</c>) instead of a plain check box.</summary>
    public bool Switch { get; init; }
    /// <summary>Checks side by side (<c>form-check-inline</c>).</summary>
    public bool Inline { get; init; }
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsPager"/>: page-number links (<c>pagination</c>); <c>Href</c> holds <c>{page}</c> where the page number goes.</summary>
public sealed record PagerOptions : FrameOptions
{
    public int Current { get; init; } = 1;
    public int Total { get; init; } = 1;
    public string Href { get; init; } = "?page={page}";
    /// <summary>small or large; empty for the default size.</summary>
    public string? Scale { get; init; }
    public string? Css { get; init; }
}

// SCRUM-339 (P1): the page-level wrappers.

/// <summary>Inputs of <see cref="PemsContentCard"/>: the white content panel of a page (<c>content-card</c>); the page's blocks are the child content.</summary>
public sealed record ContentCardOptions : FrameOptions
{
    /// <summary>The element: div (default), section, article or details.</summary>
    public string As { get; init; } = "div";
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsPageShell"/>: the outer wrapper of a page: <c>admin</c> (<c>admin-page-shell</c>, the Administration look) or <c>workspace</c> (<c>vs-page</c>, the workspace look).</summary>
public sealed record PageShellOptions : FrameOptions
{
    public string Kind { get; init; } = "workspace";
    public string? Css { get; init; }
}
