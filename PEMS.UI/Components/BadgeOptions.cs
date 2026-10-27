using PEMS.UI.Abstractions;

namespace PEMS.UI.Components;

public sealed record BadgeOptions
{
    public required TranslatableText Label { get; init; }
    public Tone Tone { get; init; } = Tone.Neutral;
}
