namespace PEMS.UI.Abstractions;

/// <summary>
/// Describes a whole form. <see cref="Action"/> is the post target; values and validation messages are state and travel separately
/// (see <see cref="FormState"/>), so the definition stays immutable and cacheable.
/// </summary>
public sealed record FormDefinition(
    string Id,
    string Title,
    string Action,
    IReadOnlyList<FieldDescriptor> Fields,
    string SubmitLabel = "Save");

/// <summary>Per-request form state: current values and validation errors by field name.</summary>
public sealed record FormState(
    IReadOnlyDictionary<string, string?> Values,
    IReadOnlyDictionary<string, string> Errors)
{
    public static FormState Empty { get; } = new(
        new Dictionary<string, string?>(), new Dictionary<string, string>());

    public string? ValueOf(string name) => Values.TryGetValue(name, out var v) ? v : null;
    public string? ErrorOf(string name) => Errors.TryGetValue(name, out var e) ? e : null;
}
