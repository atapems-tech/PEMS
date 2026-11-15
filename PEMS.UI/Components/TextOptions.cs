namespace PEMS.UI.Components;

// SCRUM-522: the text elements of a page (heading, paragraph, link, bullet list, divider) as registered components. They write the element with the page's classes
// and attributes unchanged; the point is that no raw h1-h6, p, a, ul/ol/li or hr is left in a view (a ratchet counter per element watches it).

/// <summary>Inputs of <see cref="PemsHeading"/>: <c>h1</c> to <c>h6</c>.</summary>
public sealed record HeadingOptions : FrameOptions
{
    /// <summary>1 to 6.</summary>
    public int Level { get; init; } = 2;
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsText"/>: a paragraph (<c>p</c>).</summary>
public sealed record TextBlockOptions : FrameOptions
{
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsLink"/>: an anchor; the href comes from the page (<c>href</c>, <c>asp-action</c> ...).</summary>
public sealed record LinkOptions : FrameOptions
{
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsBulletList"/>: <c>ul</c>, or <c>ol</c> when <see cref="Ordered"/>.</summary>
public sealed record BulletListOptions : FrameOptions
{
    public bool Ordered { get; init; }
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsBullet"/>: one <c>li</c>.</summary>
public sealed record BulletOptions : FrameOptions
{
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsDivider"/>: <c>hr</c>.</summary>
public sealed record DividerOptions : FrameOptions
{
    public string? Css { get; init; }
}

// SCRUM-486: the stage diagram (the graphic cycle of a record: circles joined by connectors) as one registered component. The page decides every state (done, current,
// pending, rejected); the component only draws. "wf" is the workflow diagram (icon, label and role), "wo" the smaller work-order diagram (no role line, no rejected state).

/// <summary>The state of one stage.</summary>
public enum StageState { Pending, Current, Done, Rejected }

/// <summary>One stage of a <see cref="PemsStageDiagram"/>: the text is already localized by the page.</summary>
public sealed record StageStep(string Label, string Icon, StageState State, string? Role = null);

/// <summary>Inputs of <see cref="PemsStageDiagram"/>.</summary>
public sealed record StageDiagramOptions
{
    /// <summary>wf (default) or wo.</summary>
    public string Variant { get; init; } = "wf";
    public IReadOnlyList<StageStep>? Steps { get; init; }
}
