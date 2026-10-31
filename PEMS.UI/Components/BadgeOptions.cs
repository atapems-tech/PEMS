using PEMS.UI.Abstractions;

namespace PEMS.UI.Components;

public sealed record BadgeOptions
{
    public required TranslatableText Label { get; init; }
    public Tone Tone { get; init; } = Tone.Neutral;
    /// <summary>Extra classes the page needs on the element (spacing helpers such as <c>ms-1</c>).</summary>
    public string? CssClass { get; init; }
    /// <summary>Bootstrap (default) writes today's markup; Pems writes the library's own classes.</summary>
    public ComponentSkin Skin { get; init; } = ComponentSkin.Bootstrap;
}
