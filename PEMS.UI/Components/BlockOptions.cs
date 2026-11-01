using PEMS.UI.Abstractions;

namespace PEMS.UI.Components;

/// <summary>Inputs of <see cref="PemsCard"/>: the Bootstrap card box; the page keeps the card body markup.</summary>
public sealed record CardOptions
{
    /// <summary>Extra classes the page needs on the card (shadow-sm, border-0, h-100, spacing helpers).</summary>
    public string? CssClass { get; init; }
}

/// <summary>Inputs of <see cref="PemsProgress"/>: a bar filled to Value of Max. Text between the tag helper tags is shown inside the bar.</summary>
public sealed record ProgressOptions
{
    public double Value { get; init; }
    public double Max { get; init; } = 100;
    /// <summary>The bar colour (success, danger, warning, info, primary...); null keeps the default Bootstrap bar.</summary>
    public Tone? Tone { get; init; }
    /// <summary>CSS height of the track, e.g. "8px".</summary>
    public string? Height { get; init; }
    public string? CssClass { get; init; }
    /// <summary>Extra inline style of the track (width, min-width) for layouts the classes do not cover.</summary>
    public string? Style { get; init; }
    public TranslatableText? Label { get; init; }
    public string? Title { get; init; }
}

/// <summary>Inputs of <see cref="PemsAccordion"/>: the container whose items collapse one at a time.</summary>
public sealed record AccordionOptions
{
    public required string Id { get; init; }
    public string? CssClass { get; init; }
}

/// <summary>Inputs of <see cref="PemsAccordionItem"/>: one collapsible item; Target is the id of the collapse panel, ParentId the accordion id.</summary>
public sealed record AccordionItemOptions
{
    public required string Target { get; init; }
    public string? ParentId { get; init; }
    public string? ButtonCssClass { get; init; }
    public string? BodyCssClass { get; init; }
}

/// <summary>Inputs of <see cref="PemsListGroup"/>: the list-group box; the items stay page markup (links with their routing).</summary>
public sealed record ListGroupOptions
{
    public bool Flush { get; init; }
    public string? CssClass { get; init; }
}

/// <summary>Inputs of <see cref="PemsSpinner"/>: an inline busy indicator.</summary>
public sealed record SpinnerOptions
{
    public bool Grow { get; init; }
    public Size Size { get; init; } = Size.Medium;
    public Tone? Tone { get; init; }
}
