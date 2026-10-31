namespace PEMS.UI.Components;

/// <summary>Everything the button markup prints, computed from the inputs. Plain C#, unit-testable without rendering.</summary>
public sealed record ButtonState(string Css, bool AsLink)
{
    public static ButtonState From(ButtonOptions o) => new(
        o.Skin == ComponentSkin.Bootstrap ? BootstrapCss(o) : With($"pems-btn pems-btn--{o.Tone.ToString().ToLowerInvariant()} pems-btn--{o.Size.ToString().ToLowerInvariant()}", o.CssClass),
        o.Href is not null && !o.Disabled);

    private static string BootstrapCss(ButtonOptions o)
    {
        var name = o.Tone switch { Tone.Neutral => "secondary", Tone.Primary when !o.Outline => "pems-solid", Tone.Primary => "primary", _ => o.Tone.ToString().ToLowerInvariant() };
        var size = o.Size switch { Size.Small => " btn-sm", Size.Large => " btn-lg", _ => "" };
        return With($"btn{size} btn-{(o.Outline ? "outline-" : "")}{name}", o.CssClass);
    }

    private static string With(string css, string? extra) => string.IsNullOrWhiteSpace(extra) ? css : css + " " + extra.Trim();
}
