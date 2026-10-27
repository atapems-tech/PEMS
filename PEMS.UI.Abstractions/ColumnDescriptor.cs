namespace PEMS.UI.Abstractions;

public enum ColumnAlignment
{
    Start,
    Center,
    End
}

/// <summary>
/// Describes one table column. <see cref="Key"/> selects the value in each row (data, never translated); <see cref="Header"/> is text.
/// <see cref="Kind"/> says how the value is formatted. A boolean value shows <see cref="TrueText"/> or <see cref="FalseText"/>, which are inputs too.
/// </summary>
public sealed record ColumnDescriptor(
    string Key,
    TranslatableText Header,
    ColumnAlignment Alignment = ColumnAlignment.Start,
    bool Sortable = false,
    ValueKind Kind = ValueKind.Auto,
    TranslatableText? TrueText = null,
    TranslatableText? FalseText = null);
