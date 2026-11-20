using PEMS.UI.Abstractions;

namespace PEMS.UI.Components;

// SCRUM-666..757 (batch 5): the pages that only show text (the k6 performance notes, the quick-setup guide, the knowledge-base introduction) are a definition, not a view: one generic component renders a list of
// text blocks the definition writes in both languages. Nothing here reads data; the text is escaped, never markup.

/// <summary>One block: <c>heading</c> (Level 1 to 6), <c>text</c>, <c>alert</c> (Tone info, success, warning or danger, optional Title), <c>list</c> (Items, Ordered), <c>code</c>, <c>link</c> (Href is a path of this site) or <c>divider</c>.</summary>
public sealed record ContentBlock
{
    public required string Type { get; init; }
    public TranslatableText? Text { get; init; }
    public TranslatableText? Title { get; init; }
    public int Level { get; init; } = 3;
    public string Tone { get; init; } = "info";
    public bool Ordered { get; init; }
    public IReadOnlyList<TranslatableText> Items { get; init; } = [];
    public string? Href { get; init; }
    public string? Css { get; init; }
}

/// <summary>Inputs of <see cref="PemsContentBlocks"/>.</summary>
public sealed record ContentBlocksOptions : FrameOptions
{
    public string? Id { get; init; }
    public string? Css { get; init; }
    public IReadOnlyList<ContentBlock> Blocks { get; init; } = [];
    public TranslatableText Empty { get; init; } = TranslatableText.Of(("en", "Nothing to show"), ("fa", "موردی برای نمایش نیست"));
}
