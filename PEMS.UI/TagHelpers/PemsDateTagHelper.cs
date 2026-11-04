using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.Components;

namespace PEMS.UI.TagHelpers;

/// <summary>
/// A date field that follows the page calendar (SCRUM-225, #206). The value posted is always the Gregorian date-only ISO text <c>yyyy-MM-dd</c>, so model binding and storage
/// do not change; only what the person sees and picks differs.
/// <list type="bullet">
/// <item><c>calendar="gregorian"</c>, or <c>auto</c> on a page that is not Persian: the native date input, exactly what the pages wrote by hand (<c>form-control</c> classes, name, value, min, max).</item>
/// <item><c>calendar="jalali"</c>, or <c>auto</c> on a Persian page: a text input showing the Jalali date (<c>1403/12/30</c>) that opens the vendored Jalali picker
/// (<c>pems-date.js</c>), plus a hidden input with the posted name holding the ISO value. If the script or the library does not load, the script turns the pair back into the native
/// date input, so a date can always be entered.</item>
/// </list>
/// Every other attribute (id, data-*, aria-*, placeholder, required, disabled, readonly, title) goes to the visible input. Dates in <c>value</c>, <c>min</c> and <c>max</c> are ISO text.
/// </summary>
[HtmlTargetElement("pems-date")]
public sealed class PemsDateTagHelper : TagHelper
{
    [HtmlAttributeName("name")] public string? Name { get; set; }
    /// <summary>ISO date <c>yyyy-MM-dd</c> (a time part after it is ignored); empty for no date.</summary>
    [HtmlAttributeName("value")] public string? Value { get; set; }
    [HtmlAttributeName("min")] public string? Min { get; set; }
    [HtmlAttributeName("max")] public string? Max { get; set; }
    /// <summary>small or large; empty for the default size.</summary>
    [HtmlAttributeName("scale")] public string? Scale { get; set; }
    [HtmlAttributeName("css")] public string? Css { get; set; }
    /// <summary>auto (default: Jalali on a Persian page), jalali or gregorian.</summary>
    [HtmlAttributeName("calendar")] public string Calendar { get; set; } = "auto";

    private static readonly PersianCalendar Persian = new();

    /// <summary>The Jalali text <c>yyyy/MM/dd</c> of an ISO date, or empty when the text is not a date.</summary>
    public static string ToJalaliText(string? iso)
    {
        var date = ParseIso(iso);
        return date is null ? "" : $"{Persian.GetYear(date.Value):D4}/{Persian.GetMonth(date.Value):D2}/{Persian.GetDayOfMonth(date.Value):D2}";
    }

    /// <summary>The ISO text of a date given as ISO text (normalised), or empty.</summary>
    public static string ToIsoText(string? iso) => ParseIso(iso)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

    private static DateTime? ParseIso(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var head = text.Trim();
        if (head.Length > 10) head = head[..10];
        return DateTime.TryParseExact(head, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    private bool UseJalali() => Calendar.Trim().ToLowerInvariant() switch
    {
        "jalali" => true,
        "gregorian" => false,
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("fa", StringComparison.OrdinalIgnoreCase),
    };

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var css = ControlState.Css(ControlTagHelper.Options(ControlKind.Text, Scale, Css));
        var iso = ToIsoText(Value);
        var jalali = UseJalali();

        output.TagName = "input";
        output.TagMode = TagMode.SelfClosing;
        var id = output.Attributes.TryGetAttribute("id", out var idAttribute) ? idAttribute.Value?.ToString() : null;

        if (!jalali)
        {
            ControlTagHelper.PutClassFirst(output, css);
            ControlTagHelper.Restore(output, ("type", "date"), ("name", Name), ("value", iso.Length > 0 ? iso : null), ("min", ToIsoText(Min) is { Length: > 0 } min ? min : null), ("max", ToIsoText(Max) is { Length: > 0 } max ? max : null));
            return;
        }

        // Jalali: the visible text input keeps the id and every other attribute; the hidden input carries the name and the ISO value.
        var hiddenId = (string.IsNullOrEmpty(id) ? "pdt" + Guid.NewGuid().ToString("N")[..8] : id) + "-iso";
        ControlTagHelper.PutClassFirst(output, css);
        ControlTagHelper.Restore(output, ("type", "text"), ("dir", "ltr"), ("inputmode", "numeric"), ("autocomplete", "off"));
        if (!output.Attributes.ContainsName("placeholder")) output.Attributes.Add("placeholder", "1403/01/01");
        output.Attributes.Add("data-jdp", "");
        output.Attributes.Add("data-jdp-target-value-input", "#" + hiddenId);
        output.Attributes.Add("data-jdp-target-value-type", "gregorian");
        var minText = ToJalaliText(Min);
        var maxText = ToJalaliText(Max);
        // the ISO limits stay on the element too: the script restores the native date input from them if the picker cannot start
        if (minText.Length > 0) { output.Attributes.Add("data-jdp-min-date", minText); output.Attributes.Add("data-pems-date-min", ToIsoText(Min)); }
        if (maxText.Length > 0) { output.Attributes.Add("data-jdp-max-date", maxText); output.Attributes.Add("data-pems-date-max", ToIsoText(Max)); }
        var text = ToJalaliText(iso);
        if (text.Length > 0) output.Attributes.Add("value", text);

        var hidden = new TagBuilder("input") { TagRenderMode = TagRenderMode.SelfClosing };
        hidden.Attributes["type"] = "hidden";
        hidden.Attributes["id"] = hiddenId;
        if (!string.IsNullOrEmpty(Name)) hidden.Attributes["name"] = Name;
        hidden.Attributes["value"] = iso;
        hidden.Attributes["data-pems-date-iso"] = "";
        if (output.Attributes.ContainsName("disabled")) hidden.Attributes["disabled"] = "disabled";
        if (output.Attributes.TryGetAttribute("form", out var form) && form.Value is not null) hidden.Attributes["form"] = form.Value.ToString()!;
        output.PostElement.AppendHtml(hidden);
    }
}
