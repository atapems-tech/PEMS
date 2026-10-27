namespace PEMS.UI.Components;

/// <summary>Everything the button markup prints, computed from the inputs. Plain C#, unit-testable without rendering.</summary>
public sealed record ButtonState(string Css, bool AsLink)
{
    public static ButtonState From(ButtonOptions o) => new(
        $"pems-btn pems-btn--{o.Tone.ToString().ToLowerInvariant()} pems-btn--{o.Size.ToString().ToLowerInvariant()}",
        o.Href is not null && !o.Disabled);
}
