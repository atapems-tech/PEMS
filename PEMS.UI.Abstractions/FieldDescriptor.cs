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

/// <summary>One choice of a <see cref="FieldKind.Select"/> field.</summary>
public sealed record FieldOption(string Value, string Label);

/// <summary>
/// Describes one form field as data. Code-defined pages and the metadata engine build the same type, so there is one rendering path.
/// <see cref="Label"/> and <see cref="HelpText"/> are already localised text; the caller resolves the resource key.
/// </summary>
public sealed record FieldDescriptor(
    string Name,
    string Label,
    FieldKind Kind = FieldKind.Text,
    bool Required = false,
    bool ReadOnly = false,
    string? HelpText = null,
    int? MaxLength = null,
    IReadOnlyList<FieldOption>? Options = null);
