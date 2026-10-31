using PEMS.UI.Abstractions;

namespace PEMS.UI.Components;

public enum Tone { Neutral, Primary, Danger, Success, Warning }

public enum Size { Small, Medium, Large }

/// <summary>Inputs of <see cref="PemsButton"/>. Immutable named properties (object initializer); callers use <c>with</c> to vary it. The label is an input, never a default in the component.</summary>
public sealed record ButtonOptions
{
    public required TranslatableText Label { get; init; }
    public Tone Tone { get; init; } = Tone.Primary;
    public Size Size { get; init; } = Size.Medium;
    public bool Disabled { get; init; }
    public string Type { get; init; } = "button";
    public string? Href { get; init; }
    /// <summary>Id of a modal, drawer or confirm dialog this button opens (pems-overlay.js). The row key is copied into the dialog.</summary>
    public string? Opens { get; init; }
    public string? RowKey { get; init; }
}
