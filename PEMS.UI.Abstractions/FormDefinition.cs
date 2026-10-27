namespace PEMS.UI.Abstractions;

/// <summary>
/// Describes a whole form. <see cref="Action"/> is the post target; values and validation messages are state and travel separately
/// (see <see cref="FormState"/>), so the definition stays immutable and cacheable. Later one database row holds one definition.
/// </summary>
public sealed record FormDefinition
{
    public required string Id { get; init; }
    public required TranslatableText Title { get; init; }
    public required string Action { get; init; }
    public required IReadOnlyList<FieldDescriptor> Fields { get; init; }
    public required TranslatableText SubmitLabel { get; init; }
}

/// <summary>Per-request form state: current values (data) and validation error texts by field name.</summary>
public sealed record FormState
{
    public IReadOnlyDictionary<string, string?> Values { get; init; } = new Dictionary<string, string?>();
    public IReadOnlyDictionary<string, TranslatableText> Errors { get; init; } = new Dictionary<string, TranslatableText>();

    public static FormState Empty { get; } = new();

    public string? ValueOf(string name) => Values.TryGetValue(name, out var v) ? v : null;
    public TranslatableText? ErrorOf(string name) => Errors.TryGetValue(name, out var e) ? e : null;
}
