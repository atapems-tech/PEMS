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
