namespace PEMS.UI.Abstractions;

/// <summary>
/// Describes a whole form. <see cref="Action"/> is the post target; values and validation messages are state and travel separately
/// (see <see cref="FormState"/>), so the definition stays immutable and cacheable. Later one database row holds one definition.
/// </summary>
public sealed record FormDefinition(
    string Id,
    TranslatableText Title,
    string Action,
    IReadOnlyList<FieldDescriptor> Fields,
    TranslatableText SubmitLabel);

/// <summary>Per-request form state: current values (data) and validation error texts by field name.</summary>
public sealed record FormState(
    IReadOnlyDictionary<string, string?> Values,
    IReadOnlyDictionary<string, TranslatableText> Errors)
{
    public static FormState Empty { get; } = new(
        new Dictionary<string, string?>(), new Dictionary<string, TranslatableText>());

    public string? ValueOf(string name) => Values.TryGetValue(name, out var v) ? v : null;
    public TranslatableText? ErrorOf(string name) => Errors.TryGetValue(name, out var e) ? e : null;
}
