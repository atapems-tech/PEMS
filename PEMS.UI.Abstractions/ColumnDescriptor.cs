namespace PEMS.UI.Abstractions;

public enum ColumnAlignment
{
    Start,
    Center,
    End
}

/// <summary>Describes one table column. <see cref="Key"/> selects the value in each row; <see cref="Header"/> is localised text.</summary>
public sealed record ColumnDescriptor(
    string Key,
    string Header,
    ColumnAlignment Alignment = ColumnAlignment.Start,
    bool Sortable = false);
