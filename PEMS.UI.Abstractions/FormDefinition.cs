namespace PEMS.UI.Abstractions;

/// <summary>
/// Describes a whole form. <see cref="Action"/> is the post target; values and validation messages are state and travel separately
/// (see <see cref="FormState"/>), so the definition stays immutable and cacheable. Later one database row holds one definition.
/// </summary>
public sealed record FormDefinition
{
    public required string Id { get; init; }
    public required TranslatableText Title { get; init; }
    /// <summary>The post target of a code-defined form; a stored form leaves it out (the form host posts to itself and runs <see cref="ActionKey"/>).</summary>
    public string Action { get; init; } = "";
    /// <summary>The key of the action definition the valid values are handed to (SCRUM-437).</summary>
    public string? ActionKey { get; init; }
    /// <summary>"Module.Action" the user needs to open the form (checked by the pipeline).</summary>
    public string? Permission { get; init; }
    /// <summary>Columns of the form grid (1 or 2); a field spans <see cref="FieldDescriptor.Span"/> of them.</summary>
    public int Columns { get; init; } = 1;
    /// <summary>Rules across fields (the end date after the start date); each names the field that shows the message.</summary>
    public IReadOnlyList<FieldRule>? Rules { get; init; }
    public required IReadOnlyList<FieldDescriptor> Fields { get; init; }
    public required TranslatableText SubmitLabel { get; init; }
    /// <summary>Ask the browser to confirm leaving the page after a value changed (pems-form.js).</summary>
    public bool WarnOnLeave { get; init; }
    /// <summary>An edit form that loads its saved values from a read operation (SCRUM-818): <c>/f/{key}/{id}</c> runs it and fills the fields of the same name from the first row.</summary>
    public FormLoad? Load { get; init; }
}

/// <summary>
/// The read operation that gives a form its saved values: a model and an action of the operation catalog, and the sources of its arguments (<c>param:id</c> is the id of the address, any other
/// <c>param:name</c> a value of the query string; <c>literal:</c> and <c>context:</c> as in a data source). Without <see cref="Args"/> the operation gets the id as its <c>id</c> argument.
/// The row is matched by name: a property whose name (first letter lower-case) is a field of the form fills it.
/// </summary>
public sealed record FormLoad
{
    public required string Model { get; init; }
    public required string Action { get; init; }
    public IReadOnlyDictionary<string, string>? Args { get; init; }
}

/// <summary>Per-request form state: current values (data) and validation error texts by field name.</summary>
public sealed record FormState
{
    public IReadOnlyDictionary<string, string?> Values { get; init; } = new Dictionary<string, string?>();
    public IReadOnlyDictionary<string, TranslatableText> Errors { get; init; } = new Dictionary<string, TranslatableText>();

    /// <summary>Names of the fields whose condition is false for the current values: they are rendered hidden (the browser shows them again when the values change).</summary>
    public IReadOnlyCollection<string> Hidden { get; init; } = [];
    /// <summary>Errors of the form as a whole (a rule across fields, a refused action).</summary>
    public IReadOnlyList<TranslatableText> FormErrors { get; init; } = [];

    public static FormState Empty { get; } = new();

    public string? ValueOf(string name) => Values.TryGetValue(name, out var v) ? v : null;
    public TranslatableText? ErrorOf(string name) => Errors.TryGetValue(name, out var e) ? e : null;
}
