namespace PEMS.UI.Components;

public enum Tone { Neutral, Primary, Danger, Success, Warning }

public enum Size { Small, Medium, Large }

/// <summary>Inputs of <see cref="PemsButton"/>. Immutable; callers use <c>with</c> to vary it.</summary>
public sealed record ButtonOptions(
    string Label,
    Tone Tone = Tone.Primary,
    Size Size = Size.Medium,
    bool Disabled = false,
    string Type = "button",
    string? Href = null);
