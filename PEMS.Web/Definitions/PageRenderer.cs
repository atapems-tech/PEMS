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
public sealed class PageRenderer(IPemsComponentRenderer renderer, ComponentManifestCatalog catalog, IDefinitionStore store)
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
        var regions = root["regions"] as JsonObject ?? new JsonObject();
        foreach (var name in regions.Select(r => r.Key).Where(n => !known.Contains(n)))
            problems.Add($"The page fills the region \"{name}\", which the layout \"{layoutKey}\" does not have.");

        var expressions = BuildContext(page, request);
        var html = new StringBuilder();
        await WriteNodeAsync(layoutRoot, regions, expressions, culture, html, problems, ct);
        return new RenderedPage(html.ToString(), TitleOf(page.Source.TitleJson, culture), problems);
    }

    private static void CollectRegions(JsonObject node, HashSet<string> into)
    {
        if (node["region"] is JsonValue r) { into.Add((string)r!); return; }
        if (node["children"] is JsonArray c) foreach (var child in c.OfType<JsonObject>()) CollectRegions(child, into);
    }

    private async Task WriteNodeAsync(JsonObject node, JsonObject regions, ExpressionContext ctx, CultureInfo culture, StringBuilder html, List<string> problems, CancellationToken ct)
    {
        if (node["region"] is JsonValue regionName)
        {
            if (regions[(string)regionName!] is JsonArray instances)
                foreach (var instance in instances.OfType<JsonObject>()) await WriteInstanceAsync(instance, ctx, culture, html, problems);
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
            foreach (var child in children.OfType<JsonObject>()) await WriteNodeAsync(child, regions, ctx, culture, html, problems, ct);
        html.Append("</").Append(tag).Append('>');
    }

    private async Task WriteInstanceAsync(JsonObject instance, ExpressionContext ctx, CultureInfo culture, StringBuilder html, List<string> problems)
    {
        var id = (string?)instance["id"] ?? "?";
        try
        {
            if (instance["visible"] is JsonValue v && v.GetValueKind() == JsonValueKind.String
                && !ExpressionCompiler.Compile((string)v!, PageDefinitionKind.PageExpressionRoots).EvaluateCondition(ctx))
                return;
            var component = (string)instance["component"]!;
            var parameters = catalog.ToParameters(component, (instance["options"] ?? new JsonObject()).ToJsonString());
            html.Append(await renderer.RenderAsync(component, parameters, culture));
        }
        catch (ExpressionException ex) { problems.Add($"Instance \"{id}\": {ex.Code}: {ex.Message}"); }
        catch (ArgumentException ex) { problems.Add($"Instance \"{id}\": {ex.Message}"); }
    }

    private static ExpressionContext BuildContext(ResolvedDefinition page, PipelineRequest request)
    {
        var roots = new Dictionary<string, object?>
        {
            ["user"] = new Dictionary<string, object?> { ["name"] = request.UserName, ["roles"] = request.Roles.ToList() },
            ["request"] = new Dictionary<string, object?> { ["culture"] = request.Culture, ["key"] = request.Key },
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
