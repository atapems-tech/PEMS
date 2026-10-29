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
        return errors;
    }

    public IReadOnlyList<DefinitionRef> References(string payloadJson) => Array.Empty<DefinitionRef>();
}

/// <summary>
/// A page is a layout of regions holding component instances (ADR-0011 point 1 and 2): every instance names a registered component and its options
/// are checked against that component's manifest (#433).
/// </summary>
public sealed class PageDefinitionKind : IDefinitionKind
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal) { "layout", "regions", "route", "permission" };
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

                var options = inst["options"] ?? new JsonObject();
                if (options is not JsonObject) { errors.Add(new(at + "/options", "wrong-type", "Def_Err_Type", "The options are an object.")); continue; }
                foreach (var e in _catalog.Validate(component, options.ToJsonString()).Errors)
                    errors.Add(new(at + "/options" + e.Pointer, e.Code, e.MessageKey, e.Message));
            }
        }
        return errors;
    }

    public IReadOnlyList<DefinitionRef> References(string payloadJson)
    {
        var refs = new List<DefinitionRef>();
        var root = (JsonObject)JsonNode.Parse(payloadJson)!;
        if (root["regions"] is JsonObject regions)
            foreach (var (_, list) in regions)
                if (list is JsonArray items)
                    foreach (var inst in items.OfType<JsonObject>())
                        if (inst["component"] is JsonValue cv && cv.GetValueKind() == JsonValueKind.String)
                            refs.Add(new DefinitionRef("component", (string)cv!, "renders"));
        return refs;
    }
}
