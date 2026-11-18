using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Definitions;
using PEMS.Persistence.Context;
using PEMS.UI.Abstractions;

namespace PEMS.Web.Definitions;

// SCRUM-437 (the waves): service operations. A page whose actions go through a service with business rules (status sync, segregation of duties, numbering, notifications) must keep those rules, so it
// cannot be an entity write. Instead the existing service method is exposed to definitions, as it is, by an attribute on its interface method ([PemsOperation("WorkRequest.Submit", "WorkRequest.View")]):
// the catalog below finds every such method at start-up, and ONE generic handler (operation.invoke) runs it for an action definition. No adapter and no handler per operation: the service stays the only
// place of its rules, the definition says which operation a button runs and where its arguments come from, and the code checks the operation's permission, not the definition.

/// <summary>An operation found on a service interface: its model and action, the permission the code requires, and the method to call.</summary>
public sealed record OperationRegistration(string Model, string Action, string? Permission, Type ServiceType, MethodInfo Method)
{
    public string Name => Model + "." + Action;
}

/// <summary>What a designer sees of an operation: the pair, the permission and the arguments the method takes (the actor and the cancellation token are supplied by the engine).</summary>
public sealed record OperationDescription(string Model, string Action, string? Permission, IReadOnlyList<string> Arguments);

public sealed class OperationCatalog
{
    private readonly Dictionary<(string Model, string Action), OperationRegistration> _operations = new();

    /// <summary>Scans the interfaces of the given assemblies for methods marked with <see cref="PemsOperationAttribute"/>.</summary>
    public static OperationCatalog Scan(params Assembly[] assemblies)
    {
        var catalog = new OperationCatalog();
        foreach (var type in assemblies.SelectMany(a => a.GetTypes()).Where(t => t.IsInterface))
            foreach (var method in type.GetMethods())
                if (method.GetCustomAttribute<PemsOperationAttribute>() is { } attr)
                {
                    if (catalog._operations.ContainsKey((attr.Model, attr.Action))) throw new InvalidOperationException($"The operation '{attr.Model}.{attr.Action}' is declared twice.");
                    catalog._operations[(attr.Model, attr.Action)] = new OperationRegistration(attr.Model, attr.Action, attr.Permission, type, method);
                }
        return catalog;
    }

    public OperationRegistration? Get(string? model, string? action) => model is not null && action is not null && _operations.TryGetValue((model, action), out var o) ? o : null;

    /// <summary>The models that have operations, and the actions of one model: what a designer chooses from.</summary>
    public IReadOnlyList<string> Models => _operations.Keys.Select(k => k.Model).Distinct(StringComparer.Ordinal).OrderBy(m => m, StringComparer.Ordinal).ToList();
    public IReadOnlyList<string> Actions(string model) => _operations.Keys.Where(k => k.Model == model).Select(k => k.Action).OrderBy(a => a, StringComparer.Ordinal).ToList();

    public IReadOnlyList<OperationDescription> Describe()
        => _operations.Values.OrderBy(o => o.Model, StringComparer.Ordinal).ThenBy(o => o.Action, StringComparer.Ordinal)
            .Select(o => new OperationDescription(o.Model, o.Action, o.Permission, o.Method.GetParameters().Where(p => p.ParameterType != typeof(CancellationToken) && (Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType) is { } t
                && (t == typeof(string) || t.IsPrimitive || t == typeof(decimal) || t == typeof(DateTime) || t.IsEnum)).Select(p => p.Name!).ToList())).ToList();
}

/// <summary>
/// <c>operation.invoke</c>: runs a registered service operation. Parameters: <c>model</c> and <c>action</c> (the pair the service method is marked with) and <c>args</c> (parameter name to source: <c>field:name</c> a value of the form or button, <c>literal:text</c>, or
/// <c>context:employeeId|roles|userName|culture</c>; a parameter without an entry reads the field of the same name). A parameter that is a record (an "actor") is built from the context by the names of its
/// constructor parameters. The method's answer <c>(bool Success, string? Error)</c>, <c>bool</c> or none is turned into the outcome of the action; a refusal shows the service's own message.
/// </summary>
public sealed class OperationInvokeAction(IServiceScopeFactory scopes, OperationCatalog catalog) : IActionHandler
{
    public string Key => "operation.invoke";
    public IReadOnlyCollection<string>? AllowedRoles => null;

    private static bool IsSimple(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        return t == typeof(string) || t == typeof(int) || t == typeof(long) || t == typeof(decimal) || t == typeof(double) || t == typeof(bool) || t == typeof(DateTime) || t.IsEnum;
    }

    private static readonly HashSet<string> ContextNames = new(StringComparer.OrdinalIgnoreCase) { "employeeId", "roles", "userName", "culture" };

    public IReadOnlyList<DefinitionFieldError> ValidateParameters(JsonObject? parameters)
    {
        var model = EntityJson.Str(parameters?["model"]);
        if (catalog.Get(model, EntityJson.Str(parameters?["action"])) is not { } op)
        {
            if (model is null || !catalog.Models.Contains(model)) return [new("/model", "unknown-model", "Def_Err_UnknownOperation", $"parameters.model must be one of: {string.Join(", ", catalog.Models)}.")];
            return [new("/action", "unknown-action", "Def_Err_UnknownOperation", $"parameters.action of {model} must be one of: {string.Join(", ", catalog.Actions(model))}.")];
        }
        var errors = new List<DefinitionFieldError>();
        var args = parameters!["args"] as JsonObject ?? [];
        var names = op.Method.GetParameters().Select(p => p.Name!).ToHashSet(StringComparer.Ordinal);
        foreach (var (name, source) in args)
        {
            var s = EntityJson.Str(source) ?? "";
            if (!names.Contains(name)) errors.Add(new("/args/" + name, "unknown-parameter", "Def_Err_UnknownField", $"'{name}' is not a parameter of {op.Name}."));
            else if (!(s.StartsWith("field:") && s.Length > 6 || s.StartsWith("literal:") || s.StartsWith("context:") && ContextNames.Contains(s[8..]))) errors.Add(new("/args/" + name, "wrong-source", "Def_Err_Type", "A source is field:name, literal:text or context:employeeId|roles|userName|culture."));
        }
        foreach (var p in op.Method.GetParameters())
        {
            if (p.ParameterType == typeof(CancellationToken)) continue;
            if (IsSimple(p.ParameterType)) { if (!args.ContainsKey(p.Name!) && !p.HasDefaultValue && false) errors.Add(new("/args", "missing", "Def_Err_Required", "")); continue; }
            var ctor = p.ParameterType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            if (ctor is null || ctor.GetParameters().Any(cp => !ContextNames.Contains(cp.Name!)))
                errors.Add(new("/operation", "unsupported-parameter", "Def_Err_Type", $"The parameter '{p.Name}' of {op.Name} is neither a simple value nor an object built from the context (employeeId, roles, userName, culture)."));
        }
        return errors;
    }

    public async Task<ActionOutcome> ExecuteAsync(ActionInvocation invocation, CancellationToken ct)
    {
        var op = catalog.Get(EntityJson.Str(invocation.Parameters?["model"]), EntityJson.Str(invocation.Parameters?["action"]));
        if (op is null) return ActionOutcome.Refused(TranslatableText.Of(("en", "The action is not set up."), ("fa", "کنش درست تنظیم نشده است.")));
        using var scope = scopes.CreateScope();
        if (!await scope.ServiceProvider.GetRequiredService<IDefinitionAccess>().CanOpenAsync(op.Permission, invocation.Roles, ct))
            return ActionOutcome.Refused(TranslatableText.Of(("en", "You may not do this."), ("fa", "اجازه‌ی انجام این کار را ندارید.")));

        var db = scope.ServiceProvider.GetRequiredService<PEMSDbContext>();
        var employeeId = invocation.UserName is null ? null : await db.Users.Where(u => u.UserName == invocation.UserName).Select(u => u.EmployeeId).FirstOrDefaultAsync(ct);
        var context = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            { ["employeeId"] = employeeId, ["roles"] = invocation.Roles.ToList(), ["userName"] = invocation.UserName, ["culture"] = invocation.Culture };
        var args = invocation.Parameters?["args"] as JsonObject ?? [];

        var values = new List<object?>();
        foreach (var p in op.Method.GetParameters())
        {
            if (p.ParameterType == typeof(CancellationToken)) { values.Add(ct); continue; }
            if (IsSimple(p.ParameterType))
            {
                var source = EntityJson.Str(args[p.Name!]) ?? "field:" + p.Name;
                var text = source.StartsWith("literal:") ? source[8..] : source.StartsWith("context:") ? EntityJson.Show(context[source[8..]]) : invocation.Values.GetValueOrDefault(source[6..]);
                var converted = EntityJson.Convert(p.ParameterType, text);
                if (converted is EntityJson.NotConvertible) return ActionOutcome.Refused(TranslatableText.Of(("en", $"The value of '{p.Name}' is not valid."), ("fa", $"مقدار «{p.Name}» معتبر نیست.")));
                var plainValue = p.ParameterType.IsValueType && Nullable.GetUnderlyingType(p.ParameterType) is null && p.ParameterType != typeof(bool);   // a missing checkbox is false, a missing number is missing
                if (plainValue && !p.HasDefaultValue && string.IsNullOrWhiteSpace(text))
                    return ActionOutcome.Refused(TranslatableText.Of(("en", $"'{p.Name}' is required."), ("fa", $"«{p.Name}» الزامی است.")));
                values.Add(converted);
            }
            else
            {
                var ctor = p.ParameterType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
                values.Add(ctor.Invoke(ctor.GetParameters().Select(cp => context[cp.Name!] is List<string> roles && cp.ParameterType != typeof(List<string>) ? (object)roles.AsReadOnly() : context[cp.Name!]).ToArray()));
            }
        }

        var service = scope.ServiceProvider.GetRequiredService(op.ServiceType);
        object? answer;
        try
        {
            var task = op.Method.Invoke(service, values.ToArray())!;
            await (Task)task;
            answer = task.GetType().IsGenericType ? task.GetType().GetProperty("Result")!.GetValue(task) : null;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException or UnauthorizedAccessException or DbUpdateConcurrencyException)
        {
            return ActionOutcome.Refused(TranslatableText.Literal(ex.InnerException.Message));
        }

        // (bool Success, string? Error) or bool; anything else is a success
        switch (answer)
        {
            case bool ok: return ok ? ActionOutcome.Done() : ActionOutcome.Refused(TranslatableText.Of(("en", "The operation was refused."), ("fa", "عملیات پذیرفته نشد.")));
            case ITuple t when t.Length == 2 && t[0] is bool success: return success ? ActionOutcome.Done() : ActionOutcome.Refused(TranslatableText.Literal(t[1] as string ?? "The operation was refused."));
            default: return ActionOutcome.Done();
        }
    }
}
