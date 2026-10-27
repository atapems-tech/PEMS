namespace PEMS.UI.Abstractions;

/// <summary>The input kinds a form field can take. A kind maps to exactly one component.</summary>
public enum FieldKind
{
    Text,
    TextArea,
    Number,
    Date,
    Select,
    Checkbox,
    File
}

/// <summary>One choice of a <see cref="FieldKind.Select"/> field. <see cref="Value"/> is data and is never translated.</summary>
public sealed record FieldOption
{
    public required string Value { get; init; }
    public required TranslatableText Label { get; init; }
}

/// <summary>
/// Describes one form field as data. Code-defined pages and the metadata engine build the same type, so there is one rendering path.
/// Every user-visible text is a <see cref="TranslatableText"/> input; the component holds none (ADR-0008).
/// </summary>
public sealed record FieldDescriptor
{
    public required string Name { get; init; }
    public required TranslatableText Label { get; init; }
    public FieldKind Kind { get; init; } = FieldKind.Text;
    public bool Required { get; init; }
    public bool ReadOnly { get; init; }
    public TranslatableText? HelpText { get; init; }
    public int? MaxLength { get; init; }
    public IReadOnlyList<FieldOption>? Options { get; init; }
    public TranslatableText? Placeholder { get; init; }
}
