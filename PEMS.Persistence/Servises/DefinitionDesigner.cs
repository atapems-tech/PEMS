using System.Text.Json;
using System.Text.Json.Nodes;
using PEMS.Application.Definitions;
using PEMS.Application.Interfaces;

namespace PEMS.Persistence.Services;

/// <summary>The designer operations (SCRUM-439, 604, 606, 607, 608); see <see cref="IDefinitionDesigner"/>. Edits are made on the open draft; nothing is published here.</summary>
public sealed class DefinitionDesigner(IDefinitionService definitions, IDefinitionWorkbench workbench) : IDefinitionDesigner
{
    public Task<(bool Success, string? Error)> SetValueAsync(string reference, string pointer, string valueJson, string actor)
        => EditAsync(reference, actor, "Designer: set " + pointer, root =>
        {
            var value = ParseJson(valueJson, out var error);
            if (error != null) return error;
            var (parent, last) = Locate(root, pointer, createMissing: true, out var locateError);
            if (locateError != null) return locateError;
            switch (parent)
            {
                case JsonObject o: o[last!] = value; return null;
                case JsonArray a when int.TryParse(last, out var i) && i >= 0 && i < a.Count: a[i] = value; return null;
                default: return $"The pointer '{pointer}' does not point at a place that can be set.";
            }
        });

    public Task<(bool Success, string? Error)> AddItemAsync(string reference, string arrayPointer, string itemJson, string actor)
        => EditAsync(reference, actor, "Designer: add to " + arrayPointer, root =>
        {
            var item = ParseJson(itemJson, out var error);
            if (error != null) return error;
            var array = EnsureArray(root, arrayPointer, out var arrayError);
            if (arrayError != null) return arrayError;
            array!.Add(item);
            return null;
        });

    public Task<(bool Success, string? Error)> RemoveItemAsync(string reference, string pointer, string actor)
        => EditAsync(reference, actor, "Designer: remove " + pointer, root =>
        {
            var (parent, last) = Locate(root, pointer, createMissing: false, out var error);
            if (error != null) return error;
            switch (parent)
            {
                case JsonObject o when o.ContainsKey(last!): o.Remove(last!); return null;
                case JsonArray a when int.TryParse(last, out var i) && i >= 0 && i < a.Count: a.RemoveAt(i); return null;
                default: return $"Nothing at '{pointer}' to remove.";
            }
        });

    public Task<(bool Success, string? Error)> MoveItemAsync(string reference, string arrayPointer, int from, int to, string actor)
        => EditAsync(reference, actor, $"Designer: move {from} to {to} in {arrayPointer}", root =>
        {
            var array = EnsureArray(root, arrayPointer, out var error, createMissing: false);
            if (error != null) return error;
            if (from < 0 || from >= array!.Count || to < 0 || to >= array.Count) return "The positions are outside the list.";
            var node = array[from];
            array.RemoveAt(from);
            array.Insert(to, node);
            return null;
        });

    public Task<(bool Success, string? Error)> RemoveRowAsync(string rowId, string actor)
    {
        var (reference, pointer, index, error) = SplitRow(rowId);
        return error != null ? Task.FromResult((false, (string?)error)) : RemoveItemAsync(reference, pointer + "/" + index, actor);
    }

    public Task<(bool Success, string? Error)> MoveRowAsync(string rowId, string direction, string actor)
    {
        var (reference, pointer, index, error) = SplitRow(rowId);
        if (error != null) return Task.FromResult((false, (string?)error));
        if (!int.TryParse(index, out var from)) return Task.FromResult((false, (string?)"Only rows of a list can be moved."));
        var to = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase) ? from - 1 : from + 1;
        return MoveItemAsync(reference, pointer, from, to, actor);
    }

    private static (string Reference, string Pointer, string Index, string? Error) SplitRow(string rowId)
    {
        var parts = (rowId ?? "").Split('|', 3);
        return parts.Length == 3 && parts[1].StartsWith('/') ? (parts[0], parts[1], parts[2], null) : ("", "", "", "The row is not one of a designer list.");
    }

    public Task<(bool Success, string? Error)> AddFormFieldAsync(string reference, string name, string kind, string titleEn, string titleFa, string required, string actor)
    {
        var field = new JsonObject
        {
            ["name"] = name.Trim(), ["kind"] = string.IsNullOrWhiteSpace(kind) ? "text" : kind.Trim(),
            ["label"] = new JsonObject { ["en"] = titleEn.Trim(), ["fa"] = titleFa.Trim() }
        };
        if (bool.TryParse(required, out var r) && r) field["required"] = true;
        return AddItemAsync(reference, "/fields", field.ToJsonString(), actor);
    }

    public Task<(bool Success, string? Error)> AddPageInstanceAsync(string reference, string region, string id, string component, string? optionsJson, string actor)
    {
        var options = string.IsNullOrWhiteSpace(optionsJson) ? "{}" : optionsJson;
        var instance = new JsonObject { ["id"] = id.Trim(), ["component"] = component.Trim() };
        var parsed = ParseJson(options, out var error);
        if (error != null) return Task.FromResult((false, (string?)error));
        instance["options"] = parsed;
        return AddItemAsync(reference, "/regions/" + Escape(region.Trim()), instance.ToJsonString(), actor);
    }

    public Task<(bool Success, string? Error)> AddMenuItemAsync(string reference, string itemKey, string? parent, string titleEn, string titleFa, string page, int order, string actor)
    {
        var item = new JsonObject
        {
            ["key"] = itemKey.Trim(), ["kind"] = "link", ["order"] = order, ["page"] = page.Trim(),
            ["title"] = new JsonObject { ["en"] = titleEn.Trim(), ["fa"] = titleFa.Trim() }
        };
        if (!string.IsNullOrWhiteSpace(parent)) item["parent"] = parent.Trim();
        return AddItemAsync(reference, "/items", item.ToJsonString(), actor);
    }

    public async Task<(bool Success, string? Error)> CreateActionAsync(string key, string? appKey, string model, string action, string? argsJson, string successEn, string successFa, string? redirect, string actor)
    {
        var args = ParseJson(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson, out var error);
        if (error != null || args is not JsonObject) return (false, error ?? "The arguments are a JSON object of field:name, literal:text or context:name sources.");
        var payload = new JsonObject
        {
            ["kind"] = "handler", ["handler"] = "operation.invoke",
            ["parameters"] = new JsonObject { ["model"] = model.Trim(), ["action"] = action.Trim(), ["args"] = args },
            ["success"] = new JsonObject { ["en"] = successEn.Trim(), ["fa"] = successFa.Trim() }
        };
        if (!string.IsNullOrWhiteSpace(redirect)) payload["redirect"] = redirect.Trim();
        return await workbench.CreateAsync("action", key.Trim(), appKey, $"{model} {action}", $"{model} {action}", payload.ToJsonString(), "Designer: action", actor);
    }

    // ------------------------------------------------------------------ the draft edit

    private async Task<(bool Success, string? Error)> EditAsync(string reference, string actor, string summary, Func<JsonNode, string?> change)
    {
        var parts = (reference ?? "").Split(':', 3);
        if (parts.Length != 3) return (false, "The reference is appKey:kind:key.");
        var page = await definitions.ListAsync(new DefinitionListQuery(parts[1], parts[0], parts[2], null, [], 1, 20));
        var id = page.Items.FirstOrDefault(d => d.Kind == parts[1] && d.Key == parts[2] && d.AppKey == parts[0])?.Id;
        if (id is null) return (false, $"The definition {parts[1]}/{parts[2]} does not exist.");

        var draft = await definitions.GetDraftAsync(id.Value);
        if (!draft.Success)
        {
            var detail = await definitions.GetAsync(id.Value);
            if (!detail.Success) return Answer(detail);
            var opened = await definitions.CreateDraftAsync(id.Value, null, detail.Value!.Summary.Revision, actor);
            if (!opened.Success) return Answer(opened);
            draft = opened;
        }

        JsonNode? root;
        try { root = JsonNode.Parse(draft.Value!.PayloadJson); }
        catch (JsonException) { return (false, "The draft is not valid JSON."); }
        if (root is null) return (false, "The draft is empty.");
        var problem = change(root);
        if (problem != null) return (false, problem);
        return Answer(await definitions.SaveDraftAsync(id.Value, root.ToJsonString(), summary, draft.Value.Info.Revision, actor));
    }

    // ------------------------------------------------------------------ JSON Pointer helpers

    private static string Escape(string segment) => segment.Replace("~", "~0").Replace("/", "~1");
    private static string Unescape(string segment) => segment.Replace("~1", "/").Replace("~0", "~");

    private static JsonNode? ParseJson(string json, out string? error)
    {
        error = null;
        try { return JsonNode.Parse(json); }
        catch (JsonException) { error = "The value is not valid JSON."; return null; }
    }

    /// <summary>The container that holds the last segment of a pointer, and that segment. With <paramref name="createMissing"/> missing objects on the way are created.</summary>
    private static (JsonNode? Parent, string? Last) Locate(JsonNode root, string pointer, bool createMissing, out string? error)
    {
        error = null;
        if (string.IsNullOrEmpty(pointer) || pointer[0] != '/') { error = "A pointer starts with '/' (for example /fields/0/label)."; return (null, null); }
        var segments = pointer[1..].Split('/').Select(Unescape).ToArray();
        var node = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var next = node switch
            {
                JsonObject o => o[segments[i]],
                JsonArray a when int.TryParse(segments[i], out var idx) && idx >= 0 && idx < a.Count => a[idx],
                _ => null
            };
            if (next is null)
            {
                if (!createMissing || node is not JsonObject obj) { error = $"Nothing at '/{string.Join('/', segments.Take(i + 1).Select(Escape))}'."; return (null, null); }
                next = new JsonObject();
                obj[segments[i]] = next;
            }
            node = next;
        }
        return (node, segments[^1]);
    }

    private static JsonArray? EnsureArray(JsonNode root, string pointer, out string? error, bool createMissing = true)
    {
        var (parent, last) = Locate(root, pointer, createMissing, out error);
        if (error != null) return null;
        JsonNode? target = parent switch
        {
            JsonObject o => o[last!],
            JsonArray a when int.TryParse(last, out var i) && i >= 0 && i < a.Count => a[i],
            _ => null
        };
        if (target is JsonArray existing) return existing;
        if (target is null && createMissing && parent is JsonObject po)
        {
            var created = new JsonArray();
            po[last!] = created;
            return created;
        }
        error = $"'{pointer}' is not a list.";
        return null;
    }

    private static (bool Success, string? Error) Answer<T>(DefinitionOutcome<T> outcome)
    {
        if (outcome.Success) return (true, null);
        var first = outcome.Errors?.FirstOrDefault();
        return (false, first is null ? outcome.Code ?? "The definition could not be saved." : $"{first.Pointer} {first.Message}".Trim());
    }
}
