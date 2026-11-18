namespace PEMS.Application.Common;

/// <summary>
/// Exposes a service interface method to the definition engines as an operation (SCRUM-437). An operation is the pair <see cref="Model"/> (the business model: "WorkRequest") and
/// <see cref="Action"/> (what is done to it: "Submit"); an action definition names the pair in two separate properties, so a designer picks a model and then one of its actions.
/// <see cref="Permission"/> ("Module.Action", null = any signed-in user) is checked by the code; the service keeps every business rule.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PemsOperationAttribute(string model, string action, string? permission = null) : Attribute
{
    public string Model { get; } = model;
    public string Action { get; } = action;
    public string? Permission { get; } = permission;
}
