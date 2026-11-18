using System.Text.Json;
using System.Text.Json.Nodes;
using PEMS.Application.Definitions;
using PEMS.UI.Abstractions;

namespace PEMS.Web.Definitions;

// SCRUM-437 (#437, ADR-0014): data sources and actions as definitions. A data source says where a list of choices comes from (inline items, or a registered provider by key); an action says what happens
// when a form is submitted or a button is pressed (a registered handler by key, who may run it, what the user is told, where to go next). JSON names the provider or the handler; the code behind
// them is registered in the host, so a definition can never run code that is not reviewed, and an unknown name is refused when the definition is loaded.

/// <summary>What a data source provider is asked: the signed-in user and the language, plus the parameters of the definition.</summary>
public sealed record DataSourceRequest(string Culture, IReadOnlyCollection<string> Roles, string? UserName, IReadOnlyDictionary<string, string?> Parameters);

/// <summary>A registered source of choices (departments, equipment statuses ...). Reads through the REST-level services with the same rights as the rest of the product.</summary>
public interface IDataSourceProvider
{
    string Key { get; }
    /// <summary>Roles that may read it; null = any signed-in user (the data source definition may still name a permission).</summary>
    IReadOnlyCollection<string>? AllowedRoles { get; }
    Task<IReadOnlyList<FieldOption>> LoadAsync(DataSourceRequest request, CancellationToken ct);
    /// <summary>Checks the <c>parameters</c> of a data source definition that names this provider; refused at load.</summary>
    IReadOnlyList<DefinitionFieldError> ValidateParameters(JsonObject? parameters) => [];
}

/// <summary>The values an action works on and who runs it.</summary>
public sealed record ActionInvocation(IReadOnlyDictionary<string, string?> Values, string Culture, IReadOnlyCollection<string> Roles, string? UserName, JsonObject? Parameters = null, string? IpAddress = null, string? UserAgent = null);

/// <summary>What an action did. A refused action says why per field or for the form as a whole; a text is always in both languages.</summary>
public sealed record ActionOutcome(bool Ok, TranslatableText? Message = null, IReadOnlyDictionary<string, TranslatableText>? FieldErrors = null, TranslatableText? FormError = null, string? Redirect = null)
{
    public static ActionOutcome Done(TranslatableText? message = null, string? redirect = null) => new(true, message, null, null, redirect);
    public static ActionOutcome Refused(TranslatableText formError) => new(false, null, null, formError);
    public static ActionOutcome Refused(string field, TranslatableText error) => new(false, null, new Dictionary<string, TranslatableText> { [field] = error });
}

/// <summary>The rows a grid or a view asks of a data source (SCRUM-437): the standard grid query (paging, sorting, filters, quick search) already parsed, and who asks.</summary>
public sealed record GridDataRequest(PEMS.Application.Grids.GridRequest Grid, string Culture, IReadOnlyCollection<string> Roles, string? UserName, IReadOnlyDictionary<string, string?> Parameters);

/// <summary>A registered source of grid rows: reads through the product's services with the rights of the user (row-level rules included) and answers with items and the total.</summary>
public interface IGridDataProvider
{
    string Key { get; }
    /// <summary>Roles that may read it; null = any signed-in user (the data source definition may still name a permission).</summary>
    IReadOnlyCollection<string>? AllowedRoles { get; }
    /// <summary>Items are anonymous or typed rows; they are written as JSON with the MVC defaults (camel case).</summary>
    Task<(IReadOnlyList<object> Items, int Total)> QueryAsync(GridDataRequest request, CancellationToken ct);
    /// <summary>Checks the <c>parameters</c> of a data source definition that names this provider; refused at load.</summary>
    IReadOnlyList<DefinitionFieldError> ValidateParameters(JsonObject? parameters) => [];
}

/// <summary>A registered action: the code a stored action definition may name.</summary>
public interface IActionHandler
{
    string Key { get; }
    /// <summary>Roles that may run it; null = any signed-in user (the action definition may still name a permission).</summary>
    IReadOnlyCollection<string>? AllowedRoles { get; }
    Task<ActionOutcome> ExecuteAsync(ActionInvocation invocation, CancellationToken ct);
    /// <summary>Checks the <c>parameters</c> object of an action definition that names this handler (what the handler needs besides the posted values); refused at load.</summary>
    IReadOnlyList<DefinitionFieldError> ValidateParameters(JsonObject? parameters) => [];
}

public enum ActionStatus { Ok, NotFound, Forbidden, Refused }

public sealed record ActionResult(ActionStatus Status, ActionOutcome? Outcome, TranslatableText? Message, string? Redirect);

/// <summary>The data source definition kind: <c>{ "kind": "static", "items": [...] }</c> or <c>{ "kind": "provider", "provider": "departments", "parameters": {...}, "permission": "Module.Action" }</c>.</summary>
public sealed class DataSourceDefinitionKind(IEnumerable<IDataSourceProvider> providers, IEnumerable<IGridDataProvider>? grids = null) : IDefinitionKind
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal) { "kind", "items", "provider", "parameters", "permission" };
    private readonly Dictionary<string, IDataSourceProvider> _providers = providers.ToDictionary(p => p.Key, StringComparer.Ordinal);
    private readonly Dictionary<string, IGridDataProvider> _grids = (grids ?? []).ToDictionary(p => p.Key, StringComparer.Ordinal);

    public string Key => "data-source";

    public IReadOnlyList<DefinitionFieldError> Validate(string payloadJson, DefinitionLimits limits)
    {
        var errors = new List<DefinitionFieldError>();
        if (JsonNode.Parse(payloadJson) is not JsonObject root) return [new("", "wrong-type", "Def_Err_Type", "A data source is an object.")];
        foreach (var p in root) if (!Allowed.Contains(p.Key)) errors.Add(new("/" + p.Key, "unknown-property", "Def_Err_UnknownProperty", $"'{p.Key}' is not a property of a data source."));
        var kind = Str(root["kind"]);
        switch (kind)
        {
            case "static":
                if (root["items"] is not JsonArray items || items.Count == 0) { errors.Add(new("/items", "required", "Def_Err_Required", "A static data source needs items.")); break; }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < items.Count; i++)
                {
                    var value = items[i] is JsonObject o ? Str(o["value"]) : null;
                    if (string.IsNullOrEmpty(value)) errors.Add(new($"/items/{i}/value", "required", "Def_Err_Required", "An item needs a value."));
                    else if (!seen.Add(value)) errors.Add(new($"/items/{i}/value", "duplicate", "Def_Err_Duplicate", $"The value '{value}' is used twice."));
                    if (items[i] is JsonObject lo) CheckLabel(lo["label"], $"/items/{i}/label", errors);
                }
                if (root["provider"] is not null) errors.Add(new("/provider", "overlap", "Def_Err_Overlap", "A static data source has no provider."));
                break;
            case "provider":
                var provider = Str(root["provider"]);
                if (string.IsNullOrEmpty(provider)) errors.Add(new("/provider", "required", "Def_Err_Required", "A provider data source needs a provider key."));
                else if (!_providers.TryGetValue(provider, out var registered)) errors.Add(new("/provider", "unknown-provider", "Def_Err_UnknownProvider", $"'{provider}' is not a registered data source provider."));
                else errors.AddRange(registered.ValidateParameters(root["parameters"] as JsonObject).Select(e => e with { Pointer = "/parameters" + e.Pointer }));
                if (root["items"] is not null) errors.Add(new("/items", "overlap", "Def_Err_Overlap", "A provider data source has no inline items."));
                break;
            case "grid":
                var gridProvider = Str(root["provider"]);
                if (string.IsNullOrEmpty(gridProvider)) errors.Add(new("/provider", "required", "Def_Err_Required", "A grid data source needs a provider key."));
                else if (!_grids.TryGetValue(gridProvider, out var registeredGrid)) errors.Add(new("/provider", "unknown-provider", "Def_Err_UnknownProvider", $"'{gridProvider}' is not a registered grid data provider."));
                else errors.AddRange(registeredGrid.ValidateParameters(root["parameters"] as JsonObject).Select(e => e with { Pointer = "/parameters" + e.Pointer }));
                if (root["items"] is not null) errors.Add(new("/items", "overlap", "Def_Err_Overlap", "A grid data source has no inline items."));
                break;
            default:
                errors.Add(new("/kind", "unknown-kind", "Def_Err_Kind", "kind is static, provider or grid."));
                break;
        }
        return errors;
    }

    internal static void CheckLabel(JsonNode? label, string at, List<DefinitionFieldError> errors)
    {
        try
        {
            var text = label?.Deserialize<TranslatableText>(PemsJson.Options);
            if (text is null || text.MissingLanguages(["fa", "en"]).Count > 0) errors.Add(new(at, "missing-translation", "Def_Err_Translation", "A text needs both fa and en."));
        }
        catch (JsonException) { errors.Add(new(at, "wrong-type", "Def_Err_Type", "A text is an object with fa and en.")); }
    }

    internal static string? Str(JsonNode? n) => n is JsonValue v && v.GetValueKind() == JsonValueKind.String ? (string?)v : null;

    public IReadOnlyList<DefinitionRef> References(string payloadJson) => [];
}

/// <summary>The action definition kind: <c>{ "kind": "handler", "handler": "department.create", "permission": "Module.Action", "success": {fa,en}, "redirect": "/Department" }</c>.</summary>
public sealed class ActionDefinitionKind(IEnumerable<IActionHandler> handlers) : IDefinitionKind
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal) { "kind", "handler", "permission", "success", "redirect", "confirm", "parameters" };
    private readonly Dictionary<string, IActionHandler> _handlers = handlers.ToDictionary(h => h.Key, StringComparer.Ordinal);

    public string Key => "action";

    public IReadOnlyList<DefinitionFieldError> Validate(string payloadJson, DefinitionLimits limits)
    {
        var errors = new List<DefinitionFieldError>();
        if (JsonNode.Parse(payloadJson) is not JsonObject root) return [new("", "wrong-type", "Def_Err_Type", "An action is an object.")];
        foreach (var p in root) if (!Allowed.Contains(p.Key)) errors.Add(new("/" + p.Key, "unknown-property", "Def_Err_UnknownProperty", $"'{p.Key}' is not a property of an action."));
        if (DataSourceDefinitionKind.Str(root["kind"]) != "handler") errors.Add(new("/kind", "unknown-kind", "Def_Err_Kind", "kind is handler."));
        var handler = DataSourceDefinitionKind.Str(root["handler"]);
        if (string.IsNullOrEmpty(handler)) errors.Add(new("/handler", "required", "Def_Err_Required", "An action needs a handler key."));
        else if (!_handlers.TryGetValue(handler, out var registered)) errors.Add(new("/handler", "unknown-handler", "Def_Err_UnknownHandler", $"'{handler}' is not a registered action handler."));
        else
        {
            if (root["parameters"] is not null and not JsonObject) errors.Add(new("/parameters", "wrong-type", "Def_Err_Type", "parameters is an object."));
            else errors.AddRange(registered.ValidateParameters(root["parameters"] as JsonObject).Select(e => e with { Pointer = "/parameters" + e.Pointer }));
        }
        if (root["success"] is { } success) DataSourceDefinitionKind.CheckLabel(success, "/success", errors);
        if (root["confirm"] is { } confirm) DataSourceDefinitionKind.CheckLabel(confirm, "/confirm", errors);
        var redirect = DataSourceDefinitionKind.Str(root["redirect"]);
        if (redirect is not null && (!redirect.StartsWith('/') || redirect.StartsWith("//") || redirect.Contains('\\'))) errors.Add(new("/redirect", "invalid-redirect", "Def_Err_Redirect", "redirect is a path of this site (starts with one /)."));
        return errors;
    }

    public IReadOnlyList<DefinitionRef> References(string payloadJson) => [];
}

/// <summary>Runs a stored action: resolve the definition, authorize (the definition's permission and the handler's roles), run the handler, return what to tell the user and where to go.</summary>
public sealed class ActionExecutor(IDefinitionStore store, IDefinitionAccess access, IEnumerable<IActionHandler> handlers)
{
    private readonly Dictionary<string, IActionHandler> _handlers = handlers.ToDictionary(h => h.Key, StringComparer.Ordinal);

    /// <summary>The <c>parameters</c> of a published action (null when there is none): an edit form uses them to know which row to load and how its fields map to the row.</summary>
    public async Task<JsonObject?> ParametersAsync(string key, string appKey = "core", CancellationToken ct = default)
    {
        var published = await store.GetPublishedAsync("action", key, appKey, ct);
        return published is null ? null : (JsonNode.Parse(published.PayloadJson) as JsonObject)?["parameters"] as JsonObject;
    }

    public async Task<ActionResult> RunAsync(string key, ActionInvocation invocation, string appKey = "core", CancellationToken ct = default)
    {
        var published = await store.GetPublishedAsync("action", key, appKey, ct);
        if (published is null) return new ActionResult(ActionStatus.NotFound, null, null, null);
        var root = (JsonObject)JsonNode.Parse(published.PayloadJson)!;
        var handlerKey = DataSourceDefinitionKind.Str(root["handler"]);
        if (handlerKey is null || !_handlers.TryGetValue(handlerKey, out var handler)) return new ActionResult(ActionStatus.NotFound, null, null, null);

        var permission = DataSourceDefinitionKind.Str(root["permission"]);
        var allowed = await access.CanOpenAsync(permission, invocation.Roles, ct)
            && (handler.AllowedRoles is null || handler.AllowedRoles.Any(r => invocation.Roles.Contains(r, StringComparer.OrdinalIgnoreCase)));
        if (!allowed) return new ActionResult(ActionStatus.Forbidden, null, null, null);

        var outcome = await handler.ExecuteAsync(invocation with { Parameters = root["parameters"] as JsonObject }, ct);
        if (!outcome.Ok) return new ActionResult(ActionStatus.Refused, outcome, null, null);
        var success = root["success"]?.Deserialize<TranslatableText>(PemsJson.Options);
        var redirect = outcome.Redirect ?? DataSourceDefinitionKind.Str(root["redirect"]);
        // {name} in a redirect is a value of the request (the id of the record the action ran on), so "back to the record" is data, not code
        if (redirect is not null) redirect = System.Text.RegularExpressions.Regex.Replace(redirect, @"\{(\w+)\}", m => invocation.Values.TryGetValue(m.Groups[1].Value, out var v) ? Uri.EscapeDataString(v ?? "") : "");
        return new ActionResult(ActionStatus.Ok, outcome, outcome.Message ?? success, redirect);
    }
}

/// <summary>
/// The hydrate stage for forms: a field with <c>optionsFrom</c> gets its options from the data source definition of that key, read with the rights of the user. A source the user may not read, or that
/// does not exist, gives no options and a problem the host logs; the form is never filled with data the user may not see.
/// </summary>
public sealed class FormOptionsHydrator(IDefinitionStore store, IDefinitionAccess access, IEnumerable<IDataSourceProvider> providers) : IDefinitionHydrator
{
    public const string OptionsKey = "options";
    public const string ProblemsKey = "problems";
    private readonly Dictionary<string, IDataSourceProvider> _providers = providers.ToDictionary(p => p.Key, StringComparer.Ordinal);

    public int Order => 100;

    public async Task HydrateAsync(ResolvedDefinition definition, PipelineRequest request, CancellationToken ct = default)
    {
        if (definition.Source.Kind != "form" || definition.Payload["fields"] is not JsonArray fields) return;
        var options = new Dictionary<string, IReadOnlyList<FieldOption>>(StringComparer.Ordinal);
        var problems = new List<string>();
        foreach (var field in fields.OfType<JsonObject>())
        {
            var name = DataSourceDefinitionKind.Str(field["name"]);
            var from = DataSourceDefinitionKind.Str(field["optionsFrom"]);
            if (name is null || from is null) continue;
            var list = await LoadAsync(from, request, problems, ct);
            options[name] = list;
        }
        definition.Hydrated[OptionsKey] = options;
        definition.Hydrated[ProblemsKey] = problems;
    }

    private async Task<IReadOnlyList<FieldOption>> LoadAsync(string key, PipelineRequest request, List<string> problems, CancellationToken ct)
    {
        var published = await store.GetPublishedAsync("data-source", key, request.AppKey, ct);
        if (published is null) { problems.Add($"The data source \"{key}\" does not exist."); return []; }
        var root = (JsonObject)JsonNode.Parse(published.PayloadJson)!;
        if (!await access.CanOpenAsync(DataSourceDefinitionKind.Str(root["permission"]), request.Roles, ct)) { problems.Add($"The user may not read the data source \"{key}\"."); return []; }

        if (DataSourceDefinitionKind.Str(root["kind"]) == "grid") { problems.Add($"The data source \"{key}\" gives grid rows, not options."); return []; }
        if (DataSourceDefinitionKind.Str(root["kind"]) == "static")
            return (root["items"] as JsonArray ?? []).OfType<JsonObject>()
                .Select(o => new FieldOption { Value = DataSourceDefinitionKind.Str(o["value"])!, Label = o["label"]!.Deserialize<TranslatableText>(PemsJson.Options)! }).ToList();

        var providerKey = DataSourceDefinitionKind.Str(root["provider"]);
        if (providerKey is null || !_providers.TryGetValue(providerKey, out var provider)) { problems.Add($"The provider of \"{key}\" is not registered."); return []; }
        if (provider.AllowedRoles is not null && !provider.AllowedRoles.Any(r => request.Roles.Contains(r, StringComparer.OrdinalIgnoreCase))) { problems.Add($"The user may not read the provider \"{providerKey}\"."); return []; }
        var parameters = (root["parameters"] as JsonObject)?.ToDictionary(p => p.Key, p => (string?)DataSourceDefinitionKind.Str(p.Value) ?? p.Value?.ToJsonString(), StringComparer.Ordinal) ?? new Dictionary<string, string?>();
        return await provider.LoadAsync(new DataSourceRequest(request.Culture, request.Roles, request.UserName, parameters), ct);
    }
}

public enum DataStatus { Ok, NotFound, Forbidden, Invalid }

public sealed record DataResult(DataStatus Status, IReadOnlyList<object>? Items = null, int Total = 0, string? Error = null);

/// <summary>Reads a grid data source definition: resolve, authorize (the definition's permission and the provider's roles), query with the standard grid request. Nothing is read for a user who may not.</summary>
public sealed class DataSourceExecutor(IDefinitionStore store, IDefinitionAccess access, IEnumerable<IGridDataProvider> providers)
{
    private readonly Dictionary<string, IGridDataProvider> _providers = providers.ToDictionary(p => p.Key, StringComparer.Ordinal);

    public async Task<DataResult> QueryAsync(string key, IEnumerable<KeyValuePair<string, string?>> query, string culture, IReadOnlyCollection<string> roles, string? userName, string appKey = "core", CancellationToken ct = default)
    {
        var published = await store.GetPublishedAsync("data-source", key, appKey, ct);
        if (published is null) return new DataResult(DataStatus.NotFound);
        var root = (JsonObject)JsonNode.Parse(published.PayloadJson)!;
        if (DataSourceDefinitionKind.Str(root["kind"]) != "grid") return new DataResult(DataStatus.NotFound);   // a list of options is not served as rows
        var providerKey = DataSourceDefinitionKind.Str(root["provider"]);
        if (providerKey is null || !_providers.TryGetValue(providerKey, out var provider)) return new DataResult(DataStatus.NotFound);
        if (!await access.CanOpenAsync(DataSourceDefinitionKind.Str(root["permission"]), roles, ct)
            || (provider.AllowedRoles is not null && !provider.AllowedRoles.Any(r => roles.Contains(r, StringComparer.OrdinalIgnoreCase))))
            return new DataResult(DataStatus.Forbidden);

        PEMS.Application.Grids.GridRequest grid;
        try { grid = PEMS.Application.Grids.GridRequest.FromQuery(query); }
        catch (PEMS.Application.Grids.GridQueryException ex) { return new DataResult(DataStatus.Invalid, Error: ex.Message); }
        var parameters = (root["parameters"] as JsonObject)?.ToDictionary(p => p.Key, p => (string?)DataSourceDefinitionKind.Str(p.Value) ?? p.Value?.ToJsonString(), StringComparer.Ordinal) ?? new Dictionary<string, string?>();
        try
        {
            var (items, total) = await provider.QueryAsync(new GridDataRequest(grid, culture, roles, userName, parameters), ct);
            return new DataResult(DataStatus.Ok, items, total);
        }
        catch (PEMS.Application.Grids.GridQueryException ex) { return new DataResult(DataStatus.Invalid, Error: ex.Message); }
        catch (UnauthorizedAccessException) { return new DataResult(DataStatus.Forbidden); }
    }
}
