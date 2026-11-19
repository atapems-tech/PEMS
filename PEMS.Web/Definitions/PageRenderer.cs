using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PEMS.Application.Definitions;
using PEMS.Application.Expressions;
using PEMS.UI.Manifest;
using PEMS.UI.Rendering;

namespace PEMS.Web.Definitions;

/// <summary>What the page renderer produced: the page markup, the title for the request's language, and what was wrong with the definition (a problem hides the part, it never breaks the page).</summary>
public sealed record RenderedPage(string Html, string? Title, IReadOnlyList<string> Problems);

/// <summary>
/// The render stage of the pipeline for a page definition (ADR-0011 point 8, SCRUM-438): the page names a layout (a definition of its own); the layout's containers are written
/// around the regions; every region holds component instances that are evaluated (<c>visible</c>, ADR-0015) and rendered by the component registry with the options of the
/// definition. Nothing about the page lives in code: a changed definition is a changed page.
/// </summary>
public sealed class PageRenderer(IPemsComponentRenderer renderer, ComponentManifestCatalog catalog, IDefinitionStore store, DataSourceExecutor? data = null)
{
    public async Task<RenderedPage> RenderAsync(ResolvedDefinition page, PipelineRequest request, CultureInfo culture, CancellationToken ct = default)
    {
        var problems = new List<string>();
        var root = (JsonObject)page.Payload;
        var layoutKey = (string?)root["layout"];
        var layout = layoutKey is null ? null : await store.GetPublishedAsync("layout", layoutKey, page.Source.AppKey, ct);
        if (layout is null)
            return new RenderedPage("", TitleOf(page.Source.TitleJson, culture), [$"The layout \"{layoutKey}\" does not exist."]);

        var layoutRoot = (JsonObject)JsonNode.Parse(layout.PayloadJson)!["root"]!;
        var known = new HashSet<string>(StringComparer.Ordinal);
        CollectRegions(layoutRoot, known);
        var regions = await ResolveRegionsAsync(root, page.Source.AppKey, problems, ct);
        foreach (var name in regions.Select(r => r.Key).Where(n => !known.Contains(n)))
            problems.Add($"The page fills the region \"{name}\", which the layout \"{layoutKey}\" does not have.");

        var expressions = BuildContext(page, request);
        var html = new StringBuilder();
        await WriteNodeAsync(layoutRoot, regions, expressions, culture, html, problems, page.Source.AppKey, request, ct);
        return new RenderedPage(html.ToString(), TitleOf(page.Source.TitleJson, culture), problems);
    }

    private static void CollectRegions(JsonObject node, HashSet<string> into)
    {
        if (node["region"] is JsonValue r) { into.Add((string)r!); return; }
        if (node["children"] is JsonArray c) foreach (var child in c.OfType<JsonObject>()) CollectRegions(child, into);
    }

    private async Task WriteNodeAsync(JsonObject node, JsonObject regions, ExpressionContext ctx, CultureInfo culture, StringBuilder html, List<string> problems, string appKey, PipelineRequest request, CancellationToken ct)
    {
        if (node["region"] is JsonValue regionName)
        {
            if (regions[(string)regionName!] is JsonArray instances)
                foreach (var instance in instances.OfType<JsonObject>()) await WriteInstanceAsync(instance, ctx, culture, html, problems, appKey, request, 0, ct);
            else if (node["required"] is JsonValue req && req.GetValueKind() == JsonValueKind.True)
                problems.Add($"The required region \"{(string)regionName!}\" is empty.");
            return;
        }

        var tag = (string?)node["tag"] ?? "div";
        html.Append('<').Append(tag);
        if (node["css"] is JsonValue css && !string.IsNullOrWhiteSpace((string)css!)) html.Append(" class=\"").Append(WebUtility.HtmlEncode((string)css!)).Append('"');
        if (node["attrs"] is JsonObject attrs)
            foreach (var a in attrs) html.Append(' ').Append(a.Key).Append("=\"").Append(WebUtility.HtmlEncode((string?)a.Value ?? "")).Append('"');
        html.Append('>');
        if (node["children"] is JsonArray children)
            foreach (var child in children.OfType<JsonObject>()) await WriteNodeAsync(child, regions, ctx, culture, html, problems, appKey, request, ct);
        html.Append("</").Append(tag).Append('>');
    }

    private const int MaxDepth = 4;

    /// <summary>The regions of the page: those of its template (a component definition, SCRUM-589) with the template's arguments, then the page's own regions added after them.</summary>
    private async Task<JsonObject> ResolveRegionsAsync(JsonObject root, string appKey, List<string> problems, CancellationToken ct)
    {
        var regions = new JsonObject();
        if (root["template"] is JsonValue tv && tv.GetValueKind() == JsonValueKind.String)
        {
            var key = (string)tv!;
            var definition = await store.GetPublishedAsync("component", key, appKey, ct);
            if (definition is null) problems.Add($"The template \"{key}\" does not exist.");
            else
                try
                {
                    foreach (var (name, list) in ComponentTemplate.ExpandRegions((JsonObject)JsonNode.Parse(definition.PayloadJson)!, root["args"] as JsonObject))
                        regions[name] = list?.DeepClone();
                }
                catch (ComponentTemplateException ex) { problems.Add($"The template \"{key}\": {ex.Message}"); }
        }
        if (root["regions"] is JsonObject own)
            foreach (var (name, list) in own)
            {
                if (regions[name] is JsonArray existing && list is JsonArray more) foreach (var item in more) existing.Add(item?.DeepClone());
                else regions[name] = list?.DeepClone();
            }
        return regions;
    }

    private async Task WriteInstanceAsync(JsonObject instance, ExpressionContext ctx, CultureInfo culture, StringBuilder html, List<string> problems, string appKey, PipelineRequest request, int depth, CancellationToken ct)
    {
        var id = (string?)instance["id"] ?? "?";
        try
        {
            if (instance["visible"] is JsonValue v && v.GetValueKind() == JsonValueKind.String
                && !ExpressionCompiler.Compile((string)v!, PageDefinitionKind.PageExpressionRoots).EvaluateCondition(ctx))
                return;
            var component = (string)instance["component"]!;
            if (ComponentTemplate.IsDefinitionRef(component))
            {
                if (depth >= MaxDepth) { problems.Add($"Instance \"{id}\": component definitions are nested too deep."); return; }
                var key = ComponentTemplate.KeyOf(component);
                var definition = await store.GetPublishedAsync("component", key, appKey, ct);
                if (definition is null) { problems.Add($"Instance \"{id}\": the component definition \"{key}\" does not exist."); return; }
                var children = ComponentTemplate.ExpandInstances((JsonObject)JsonNode.Parse(definition.PayloadJson)!, instance["options"] as JsonObject, id);
                foreach (var child in children.OfType<JsonObject>()) await WriteInstanceAsync(child, ctx, culture, html, problems, appKey, request, depth + 1, ct);
                return;
            }
            var options = RuntimeValues.Substitute((instance["options"] ?? new JsonObject()).DeepClone(), request) as JsonObject ?? new JsonObject();
            if (instance["bindings"] is JsonObject bindings && bindings["record"] is JsonObject record)
            {
                var values = await ReadRecordAsync(record, request, appKey, id, problems, ct);
                if (values is null && problems.Any(p => p.StartsWith($"Instance \"{id}\": the record", StringComparison.Ordinal))) return;   // not allowed or invalid: the part is hidden
                if (values is not null)
                {
                    // `map` feeds named options from row properties (a badge label, a number, a link); without it the whole row goes to `values` (the record view)
                    if (record["map"] is JsonObject map)
                    {
                        foreach (var (option, property) in map)
                            if (property is JsonValue pv && pv.GetValueKind() == JsonValueKind.String && values[(string)pv!] is { } cell) options[option] = cell.DeepClone();
                    }
                    else options["values"] = values;
                }
            }
            if (instance["bindings"] is JsonObject chartBindings && chartBindings["chart"] is JsonObject chart)
            {
                var spec = await BuildChartAsync(chart, request, culture, appKey, id, problems, ct);
                if (spec is null) return;   // not allowed or invalid: the part is hidden
                options["spec"] = spec;
            }
            var parameters = catalog.ToParameters(component, options.ToJsonString());
            html.Append(await renderer.RenderAsync(component, parameters, culture));
        }
        catch (ExpressionException ex) { problems.Add($"Instance \"{id}\": {ex.Code}: {ex.Message}"); }
        catch (ComponentTemplateException ex) { problems.Add($"Instance \"{id}\": {ex.Message}"); }
        catch (ArgumentException ex) { problems.Add($"Instance \"{id}\": {ex.Message}"); }
    }

    /// <summary>
    /// A chart from the rows of a grid data source (SCRUM-619): <c>{ "source": "/d/key", "labelField": "month", "series": [{ "field": "count", "name": {en,fa}, "color": "..." }], "kind": "bar", "stacked": false,
    /// "top": 50, "sort": "-count", "ariaLabel": {en,fa}, "emptyText": {en,fa} }</c>. The rows are read through the same resolve and authorize steps as the grid, at most 200 of them.
    /// </summary>
    private async Task<JsonObject?> BuildChartAsync(JsonObject chart, PipelineRequest request, CultureInfo culture, string appKey, string id, List<string> problems, CancellationToken ct)
    {
        if (data is null) { problems.Add($"Instance \"{id}\": the chart cannot be read here."); return null; }
        var source = (string?)chart["source"];
        var key = source is not null && System.Text.RegularExpressions.Regex.Match(source, "^/d/([a-z0-9][a-z0-9._-]*)$") is { Success: true } m ? m.Groups[1].Value : null;
        var labelField = (string?)chart["labelField"];
        if (key is null || string.IsNullOrWhiteSpace(labelField) || chart["series"] is not JsonArray series || series.Count == 0)
        { problems.Add($"Instance \"{id}\": the chart binding needs a /d/ source, a labelField and series."); return null; }
        var top = chart["top"] is JsonValue tv && tv.TryGetValue<int>(out var t) ? Math.Clamp(t, 1, 200) : 50;
        var query = new List<KeyValuePair<string, string?>> { new("page", "1"), new("pageSize", top.ToString(CultureInfo.InvariantCulture)) };
        if (chart["sort"] is JsonValue sv && sv.GetValueKind() == JsonValueKind.String) query.Add(new("sort", (string)sv!));
        var result = await data.QueryAsync(key, query, request.Culture, request.Roles, request.UserName, appKey, ct);
        switch (result.Status)
        {
            case DataStatus.Forbidden: problems.Add($"Instance \"{id}\": the chart data is not allowed for this user."); return null;
            case DataStatus.NotFound: problems.Add($"Instance \"{id}\": the chart source \"{key}\" does not exist."); return null;
            case DataStatus.Invalid: problems.Add($"Instance \"{id}\": the chart request is invalid: {result.Error}"); return null;
        }
        var rows = (result.Items ?? []).Select(i => JsonSerializer.SerializeToNode(i) as JsonObject).Where(r => r is not null).Cast<JsonObject>().ToList();
        string Cell(JsonObject row, string field) => row[field] is JsonValue v ? (v.GetValueKind() == JsonValueKind.String ? (string?)v ?? "" : v.ToJsonString().Trim('"')) : "";
        var labels = new JsonArray();
        foreach (var r in rows) labels.Add(Cell(r, labelField));
        var outSeries = new JsonArray();
        foreach (var sd in series.OfType<JsonObject>())
        {
            var field = (string?)sd["field"];
            if (string.IsNullOrWhiteSpace(field)) continue;
            var values = new JsonArray();
            foreach (var r in rows) values.Add(double.TryParse(Cell(r, field), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0d);
            var entry = new JsonObject { ["name"] = ChartText(sd["name"], culture) ?? field, ["values"] = values };
            if (sd["color"] is JsonValue cv && cv.GetValueKind() == JsonValueKind.String) entry["color"] = (string)cv!;
            outSeries.Add(entry);
        }
        var kind = chart["kind"] is JsonValue kv && kv.GetValueKind() == JsonValueKind.String ? (string)kv! : "bar";
        var spec = new JsonObject
        {
            ["kind"] = kind, ["ariaLabel"] = ChartText(chart["ariaLabel"], culture) ?? "", ["emptyText"] = ChartText(chart["emptyText"], culture) ?? "-",
            ["labels"] = labels, ["series"] = outSeries,
            ["stacked"] = chart["stacked"] is JsonValue st && st.GetValueKind() == JsonValueKind.True
        };
        if (kind == "donut" && outSeries.Count > 0) spec["centerLabel"] = (string?)outSeries[0]!["name"];
        return spec;
    }

    /// <summary>A text of a definition for the request's language: a plain string, or an object with fa and en.</summary>
    private static string? ChartText(JsonNode? node, CultureInfo culture)
    {
        if (node is JsonValue v && v.GetValueKind() == JsonValueKind.String) return (string)v!;
        if (node is not JsonObject o) return null;
        foreach (var k in new[] { culture.TwoLetterISOLanguageName, "fa", "en" })
            if (o[k] is JsonValue lv && lv.GetValueKind() == JsonValueKind.String && !string.IsNullOrWhiteSpace((string)lv!)) return (string)lv!;
        return null;
    }

    /// <summary>
    /// The record a details part shows (SCRUM-600): <c>{ "source": "/d/key", "idField": "workOrderId", "id": "{{params.id}}" }</c> reads one row of a grid data source through the same
    /// resolve and authorize steps as the grid endpoint (so the user's permission and the row-level rules apply); null values mean the record was not found.
    /// </summary>
    private async Task<JsonObject?> ReadRecordAsync(JsonObject record, PipelineRequest request, string appKey, string id, List<string> problems, CancellationToken ct)
    {
        if (data is null) { problems.Add($"Instance \"{id}\": the record cannot be read here."); return null; }
        var source = (string?)record["source"];
        var idField = (string?)record["idField"];
        var recordId = RuntimeValues.Substitute(record["id"]?.DeepClone(), request) is JsonValue v && v.GetValueKind() == JsonValueKind.String ? (string?)v : null;
        var key = source is not null && System.Text.RegularExpressions.Regex.Match(source, "^/d/([a-z0-9][a-z0-9._-]*)$") is { Success: true } m ? m.Groups[1].Value : null;
        if (key is null || string.IsNullOrWhiteSpace(idField)) { problems.Add($"Instance \"{id}\": the record binding needs a /d/ source and an idField."); return null; }
        if (string.IsNullOrWhiteSpace(recordId)) return null;   // no id on the request: the part shows "not found"
        var result = await data.QueryAsync(key, [new("page", "1"), new("pageSize", "1"), new("f." + idField, "eq:" + recordId)], request.Culture, request.Roles, request.UserName, appKey, ct);
        switch (result.Status)
        {
            case DataStatus.Forbidden: problems.Add($"Instance \"{id}\": the record is not allowed for this user."); return null;
            case DataStatus.NotFound: problems.Add($"Instance \"{id}\": the record source \"{key}\" does not exist."); return null;
            case DataStatus.Invalid: problems.Add($"Instance \"{id}\": the record request is invalid: {result.Error}"); return null;
        }
        if (result.Items is not { Count: > 0 }) return null;
        var row = JsonSerializer.SerializeToNode(result.Items[0]) as JsonObject;
        if (row is null) return null;
        var values = new JsonObject();
        foreach (var (name, node) in row)
            values[name] = node is null ? null : node is JsonValue jv && jv.GetValueKind() == JsonValueKind.String ? (string?)jv : node.ToJsonString().Trim('"');
        return values;
    }

    private static ExpressionContext BuildContext(ResolvedDefinition page, PipelineRequest request)
    {
        var roots = new Dictionary<string, object?>
        {
            ["user"] = new Dictionary<string, object?> { ["name"] = request.UserName, ["roles"] = request.Roles.ToList() },
            ["request"] = new Dictionary<string, object?> { ["culture"] = request.Culture, ["key"] = request.Key },
            ["params"] = request.Parameters is null ? new Dictionary<string, object?>() : request.Parameters.ToDictionary(p => p.Key, p => (object?)p.Value),
            ["values"] = new Dictionary<string, object?>(page.Hydrated),
            ["item"] = null
        };
        return new ExpressionContext(roots, DateTime.Today);
    }

    /// <summary>The title of the definition in the request's language (the two-letter code), else Persian, else English.</summary>
    public static string? TitleOf(string? titleJson, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(titleJson) || JsonNode.Parse(titleJson) is not JsonObject t) return null;
        var lang = culture.TwoLetterISOLanguageName;
        foreach (var k in new[] { lang, "fa", "en" })
            if (t[k] is JsonValue v && v.GetValueKind() == JsonValueKind.String && !string.IsNullOrWhiteSpace((string)v!)) return (string)v!;
        return null;
    }
}
