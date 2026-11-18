using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PEMS.Application.Definitions;
using PEMS.UI.Abstractions;
using PEMS.UI.Manifest;
using PEMS.UI.Rendering;

namespace PEMS.Web.Definitions;

/// <summary>The kinds that can be stored today (ADR-0011). More kinds are added by registering another <see cref="IDefinitionKind"/>.</summary>
public static class DefinitionKindExtensions
{
    public static IServiceCollection AddPemsDefinitionKinds(this IServiceCollection services)
    {
        services.AddSingleton<IDefinitionKind, FormDefinitionKind>();
        services.AddSingleton<IDefinitionKind, PageDefinitionKind>();
        services.AddSingleton<IDefinitionKind, LayoutDefinitionKind>();
        // SCRUM-437: data sources and actions as definitions; the providers and handlers are registered code, named by the definitions
        services.AddSingleton<IDefinitionKind, DataSourceDefinitionKind>();
        services.AddSingleton<IDefinitionKind, ActionDefinitionKind>();
        // posted values of a stored form are cleaned before use: rich-text HTML sanitized, undeclared names dropped (SCRUM-59)
        services.AddScoped<FormValueSanitizer>();
        return services;
    }

    internal static string Pointer(string jsonPath)
    {
        // "$.fields[0].name" -> "/fields/0/name"
        var p = jsonPath.TrimStart('$').Replace("[", "/").Replace("]", "").Replace('.', '/');
        return p.Length == 0 ? "" : (p.StartsWith('/') ? p : "/" + p);
    }
}

/// <summary>A form is the <see cref="FormDefinition"/> record as JSON; unknown properties are rejected.</summary>
public sealed class FormDefinitionKind : IDefinitionKind
{
    private static readonly JsonSerializerOptions Strict = new(PemsJson.Options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public string Key => "form";

    public IReadOnlyList<DefinitionFieldError> Validate(string payloadJson, DefinitionLimits limits)
    {
        FormDefinition? form;
        try { form = JsonSerializer.Deserialize<FormDefinition>(payloadJson, Strict); }
        catch (JsonException ex)
        {
            return new[] { new DefinitionFieldError(DefinitionKindExtensions.Pointer(ex.Path ?? "$"), "invalid-form", "Def_Err_Form", ex.Message) };
        }
        if (form is null) return new[] { new DefinitionFieldError("", "invalid-form", "Def_Err_Form", "The form is empty.") };

        var errors = new List<DefinitionFieldError>();
        if (form.Fields.Count == 0) errors.Add(new("/fields", "required", "Def_Err_FormFields", "A form needs at least one field."));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < form.Fields.Count; i++)
            if (!seen.Add(form.Fields[i].Name))
                errors.Add(new($"/fields/{i}/name", "duplicate", "Def_Err_FormFieldDuplicate", $"The field name '{form.Fields[i].Name}' is used twice."));
        errors.AddRange(FormEngine.CheckDefinition(form));   // SCRUM-578: conditions, rules, messages and options are checked when the definition is loaded
        return errors;
    }

    public IReadOnlyList<DefinitionRef> References(string payloadJson)
    {
        try
        {
            var form = JsonSerializer.Deserialize<FormDefinition>(payloadJson, Strict);
            if (form is null) return Array.Empty<DefinitionRef>();
            var refs = form.Fields.Where(f => !string.IsNullOrEmpty(f.OptionsFrom)).Select(f => new DefinitionRef("data-source", f.OptionsFrom!, "options")).ToList();
            if (!string.IsNullOrEmpty(form.ActionKey)) refs.Add(new DefinitionRef("action", form.ActionKey, "submits"));
            return refs;
        }
        catch (JsonException) { return Array.Empty<DefinitionRef>(); }
    }
}

/// <summary>
/// A page is a layout of regions holding component instances (ADR-0011 point 1 and 2): every instance names a registered component and its options
/// are checked against that component's manifest (#433).
/// </summary>
public sealed class PageDefinitionKind : IDefinitionKind
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal) { "layout", "regions", "route", "permission" };
    /// <summary>The variables an expression of a page can read: the signed-in user, the request (culture, route values), the hydrated values of the page and the current item.</summary>
    internal static readonly string[] PageExpressionRoots = ["user", "request", "values", "item"];

    private static readonly HashSet<string> AllowedInstance = new(StringComparer.Ordinal) { "id", "component", "options", "visible", "bindings" };

    private readonly IPemsComponentRegistry _registry;
    private readonly ComponentManifestCatalog _catalog;

    public PageDefinitionKind(IPemsComponentRegistry registry, ComponentManifestCatalog catalog)
    {
        _registry = registry;
        _catalog = catalog;
    }

    public string Key => "page";

    public IReadOnlyList<DefinitionFieldError> Validate(string payloadJson, DefinitionLimits limits)
    {
        var errors = new List<DefinitionFieldError>();
        var root = (JsonObject)JsonNode.Parse(payloadJson)!;
        foreach (var p in root) if (!Allowed.Contains(p.Key)) errors.Add(new("/" + p.Key, "unknown-property", "Def_Err_UnknownProperty", $"'{p.Key}' is not a property of a page."));
        if (root["layout"] is not JsonValue lv || lv.GetValueKind() != JsonValueKind.String || string.IsNullOrWhiteSpace((string?)lv))
            errors.Add(new("/layout", "required", "Def_Err_Required", "A page needs a layout key."));
        if (root["route"] is { } route && !(route is JsonValue rv && rv.GetValueKind() == JsonValueKind.String)) errors.Add(new("/route", "wrong-type", "Def_Err_Type", "The route must be text."));
        if (root["permission"] is { } perm && !(perm is JsonValue pv && pv.GetValueKind() == JsonValueKind.String)) errors.Add(new("/permission", "wrong-type", "Def_Err_Type", "The permission must be text."));

        if (root["regions"] is not JsonObject regions) { errors.Add(new("/regions", "required", "Def_Err_Required", "A page needs regions (an object of instance lists).")); return errors; }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;
        foreach (var (region, list) in regions)
        {
            if (list is not JsonArray items) { errors.Add(new($"/regions/{region}", "wrong-type", "Def_Err_Type", "A region is a list of component instances.")); continue; }
            for (var i = 0; i < items.Count; i++)
            {
                var at = $"/regions/{region}/{i}";
                if (++count > limits.MaxInstancesPerPage) { errors.Add(new("/regions", "too-many-instances", "Def_Err_TooMany", $"A page may hold at most {limits.MaxInstancesPerPage} component instances.")); return errors; }
                if (items[i] is not JsonObject inst) { errors.Add(new(at, "wrong-type", "Def_Err_Type", "A component instance is an object.")); continue; }
                foreach (var p in inst) if (!AllowedInstance.Contains(p.Key)) errors.Add(new(at + "/" + p.Key, "unknown-property", "Def_Err_UnknownProperty", $"'{p.Key}' is not a property of a component instance."));

                var id = inst["id"] is JsonValue idv && idv.GetValueKind() == JsonValueKind.String ? (string?)idv : null;
                if (string.IsNullOrWhiteSpace(id)) errors.Add(new(at + "/id", "required", "Def_Err_Required", "An instance needs an id."));
                else if (!ids.Add(id)) errors.Add(new(at + "/id", "duplicate", "Def_Err_Duplicate", $"The instance id '{id}' is used twice on this page."));

                var component = inst["component"] is JsonValue cv && cv.GetValueKind() == JsonValueKind.String ? (string?)cv : null;
                if (string.IsNullOrWhiteSpace(component)) { errors.Add(new(at + "/component", "required", "Def_Err_Required", "An instance needs a component key.")); continue; }
                if (!_registry.Keys.Contains(component, StringComparer.OrdinalIgnoreCase)) { errors.Add(new(at + "/component", "unknown-component", "Def_Err_UnknownComponent", $"'{component}' is not a registered component.")); continue; }

                // visible is an expression (ADR-0015): it must parse and read only the variables a page offers; checked at load, not at run time
                if (inst["visible"] is { } visible)
                {
                    if (visible is JsonValue vv && vv.GetValueKind() == JsonValueKind.String)
                    {
                        if (!PEMS.Application.Expressions.ExpressionCompiler.TryCompile((string)vv!, out _, out var ex, PageExpressionRoots))
                            errors.Add(new(at + "/visible", ex!.Code, "Def_Err_Expression", ex.Message));
                    }
                    else errors.Add(new(at + "/visible", "wrong-type", "Def_Err_Type", "visible is an expression (text)."));
                }

                var options = inst["options"] ?? new JsonObject();
                if (options is not JsonObject) { errors.Add(new(at + "/options", "wrong-type", "Def_Err_Type", "The options are an object.")); continue; }
                foreach (var e in _catalog.Validate(component, options.ToJsonString()).Errors)
                    errors.Add(new(at + "/options" + e.Pointer, e.Code, e.MessageKey, e.Message));
            }
        }
        return errors;
    }

    private static IEnumerable<string> Strings(JsonNode? node) => node switch
    {
        JsonValue v when v.GetValueKind() == JsonValueKind.String => [(string)v!],
        JsonObject o => o.SelectMany(p => Strings(p.Value)),
        JsonArray a => a.SelectMany(Strings),
        _ => []
    };

    public IReadOnlyList<DefinitionRef> References(string payloadJson)
    {
        var refs = new List<DefinitionRef>();
        var root = (JsonObject)JsonNode.Parse(payloadJson)!;
        if (root["regions"] is JsonObject regions)
            foreach (var (_, list) in regions)
                if (list is JsonArray items)
                    foreach (var inst in items.OfType<JsonObject>())
                    {
                        if (inst["component"] is JsonValue cv && cv.GetValueKind() == JsonValueKind.String)
                            refs.Add(new DefinitionRef("component", (string)cv!, "renders"));
                        // SCRUM-437: the bindings of a component to stored definitions: an url "/d/{key}" reads a data source, an action-button runs an action
                        if (inst["options"] is JsonObject options)
                        {
                            foreach (var text in Strings(options))
                                if (System.Text.RegularExpressions.Regex.Match(text, "^/d/([a-z0-9][a-z0-9._-]*)(?:[?].*)?$") is { Success: true } m) refs.Add(new DefinitionRef("data-source", m.Groups[1].Value, "reads"));
                            if ((string?)inst["component"] == "action-button" && options["action"] is JsonValue av && av.GetValueKind() == JsonValueKind.String) refs.Add(new DefinitionRef("action", (string)av!, "runs"));
                        }
                    }
        return refs;
    }
}
