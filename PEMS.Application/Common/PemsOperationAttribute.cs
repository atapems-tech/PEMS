namespace PEMS.Application.Common;

/// <summary>
/// Exposes a service interface method to the definition engines as an operation (SCRUM-437): an action definition runs it through the generic handler <c>operation.invoke</c>, the code checks
/// <see cref="Permission"/> ("Module.Action", null = any signed-in user) and the service keeps every business rule. Parameters are simple values or an "actor" record built from the user's context.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PemsOperationAttribute(string key, string? permission = null) : Attribute
{
    public string Key { get; } = key;
    public string? Permission { get; } = permission;
}
