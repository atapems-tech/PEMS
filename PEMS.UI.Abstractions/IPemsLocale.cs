using System.Globalization;

namespace PEMS.UI.Abstractions;

/// <summary>
/// The language and formatting context a component renders in. Components never look texts up and never format by themselves:
/// they ask this service. The host may replace the default implementation (for example to read the user's language from a profile).
/// </summary>
public interface IPemsLocale
{
    CultureInfo Culture { get; }

    /// <summary>The language used when a text has none for <see cref="Culture"/> (the product default).</summary>
    string DefaultLanguage { get; }

    bool IsRightToLeft { get; }

    /// <summary>Resolves a translatable text for <see cref="Culture"/> (null gives an empty string).</summary>
    string Text(TranslatableText? text);

    /// <summary>Formats a data value for display: dates (Jalali for Persian), numbers, other values as text. Never translates.</summary>
    string Format(object? value, ValueKind kind = ValueKind.Auto);
}
