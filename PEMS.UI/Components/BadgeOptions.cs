using PEMS.UI.Abstractions;

namespace PEMS.UI.Components;

public sealed record BadgeOptions(TranslatableText Text, Tone Tone = Tone.Neutral);
