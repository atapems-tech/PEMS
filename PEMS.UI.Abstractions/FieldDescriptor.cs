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
public sealed record FieldOption(string Value, TranslatableText Label);

/// <summary>
/// Describes one form field as data. Code-defined pages and the metadata engine build the same type, so there is one rendering path.
/// Every user-visible text is a <see cref="TranslatableText"/> input; the component holds none (ADR-0008).
/// </summary>
public sealed record FieldDescriptor(
    string Name,
    TranslatableText Label,
    FieldKind Kind = FieldKind.Text,
    bool Required = false,
    bool ReadOnly = false,
    TranslatableText? HelpText = null,
    int? MaxLength = null,
    IReadOnlyList<FieldOption>? Options = null,
    TranslatableText? Placeholder = null);
