namespace PEMS.UI.Abstractions;

/// <summary>Describes a read-only list view: a title and its columns. Rows are data and are passed separately.</summary>
public sealed record ViewDescriptor(
    string Id,
    string Title,
    IReadOnlyList<ColumnDescriptor> Columns);
