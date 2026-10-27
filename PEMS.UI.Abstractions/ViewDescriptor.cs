namespace PEMS.UI.Abstractions;

/// <summary>Describes a read-only list view: a title, its columns and the text shown when there are no rows. Rows are data and are passed separately.</summary>
public sealed record ViewDescriptor(
    string Id,
    TranslatableText Title,
    IReadOnlyList<ColumnDescriptor> Columns,
    TranslatableText? EmptyText = null);
