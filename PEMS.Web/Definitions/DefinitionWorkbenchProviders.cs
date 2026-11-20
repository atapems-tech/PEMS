using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Definitions;
using PEMS.Persistence.Context;

namespace PEMS.Web.Definitions;

// SCRUM-449, 450, 451, 455: the definition workbench reads definitions as grid rows, so its list, details, usage and storage pages are stored pages over these data sources (entity-list, record-view,
// entity-chart): nothing about the workbench is a compiled screen. Each data source definition names a permission (Definitions.View); the providers only shape rows.

internal static class WorkbenchRows
{
    /// <summary>A title of a definition (a language map or a plain string) in the language of the request.</summary>
    public static string Title(string titleJson, string culture)
    {
        try
        {
            using var doc = JsonDocument.Parse(titleJson);
            if (doc.RootElement.ValueKind == JsonValueKind.String) return doc.RootElement.GetString() ?? "";
            foreach (var k in new[] { culture, "fa", "en" })
                if (doc.RootElement.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())) return v.GetString()!;
        }
        catch (JsonException) { }
        return "";
    }

    public static string FilterValue(GridDataRequest request, string field)
        => request.Grid.Filters.FirstOrDefault(f => f.Field == field && f.Op == "eq")?.Value ?? "";
}

/// <summary>The definitions of the store as rows: <c>ref</c> (appKey:kind:key, the id of a row), key, kind, appKey, title, origin, publishedVersion, hasDraft, isCustomized, revision, createdAt. Filters kind and appKey; the search matches key and title.</summary>
public sealed class DefinitionsGridProvider(IServiceScopeFactory scopes) : IGridDataProvider
{
    public string Key => "definitions";
    public IReadOnlyCollection<string>? AllowedRoles => null;

    public async Task<(IReadOnlyList<object> Items, int Total)> QueryAsync(GridDataRequest request, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var definitions = scope.ServiceProvider.GetRequiredService<IDefinitionService>();
        var kind = WorkbenchRows.FilterValue(request, "kind");
        var appKey = WorkbenchRows.FilterValue(request, "appKey");
        var result = await definitions.ListAsync(new DefinitionListQuery(kind.Length == 0 ? null : kind, appKey.Length == 0 ? null : appKey, request.Grid.Search, null, [], Math.Max(1, request.Grid.Page), request.Grid.PageSize), ct);
        IReadOnlyList<object> rows = result.Items.Select(d => (object)new Dictionary<string, object?>
        {
            ["ref"] = $"{d.AppKey}:{d.Kind}:{d.Key}", ["key"] = d.Key, ["kind"] = d.Kind, ["appKey"] = d.AppKey, ["title"] = WorkbenchRows.Title(d.TitleJson, request.Culture),
            ["origin"] = d.Origin.ToString(), ["publishedVersion"] = d.PublishedVersion, ["hasDraft"] = d.HasDraft, ["isCustomized"] = d.IsCustomized, ["revision"] = d.Revision,
            ["createdAt"] = d.CreatedAtUtc.ToString("s", System.Globalization.CultureInfo.InvariantCulture)
        }).ToList();
        return (rows, result.Total);
    }
}

/// <summary>
/// One row per registered definition kind (SCRUM-451, 455): <c>kind</c>, <c>owner</c> (where the kind lives), <c>definitions</c>, <c>published</c>, <c>drafts</c>, <c>versions</c>, <c>payloadBytes</c> (published payloads). The list of
/// kinds, their counts and their size on disk are one data source: the kind catalog and the storage report.
/// </summary>
public sealed class DefinitionKindsGridProvider(IServiceScopeFactory scopes, IEnumerable<IDefinitionKind> kinds, DefinitionOwnership ownership) : IGridDataProvider
{
    public string Key => "definition-kinds";
    public IReadOnlyCollection<string>? AllowedRoles => null;

    public async Task<(IReadOnlyList<object> Items, int Total)> QueryAsync(GridDataRequest request, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PEMSDbContext>();
        var perKind = await db.Definitions.AsNoTracking().GroupBy(d => d.Kind)
            .Select(g => new { Kind = g.Key, Definitions = g.Count(), Published = g.Count(d => d.PublishedVersionId != null) }).ToListAsync(ct);
        var versions = await db.DefinitionVersions.AsNoTracking().GroupBy(v => v.Definition!.Kind)
            .Select(g => new { Kind = g.Key, Versions = g.Count(), Drafts = g.Count(v => v.Status == PEMS.Domain.Entities.Definitions.DefinitionStatus.Draft || v.Status == PEMS.Domain.Entities.Definitions.DefinitionStatus.Validated) }).ToListAsync(ct);
        var bytes = await db.Definitions.AsNoTracking().Where(d => d.PublishedVersionId != null).GroupBy(d => d.Kind).Select(g => new { Kind = g.Key, Bytes = g.Sum(d => d.PublishedVersion!.PayloadJson.Length) }).ToListAsync(ct);

        var rows = kinds.Select(k => k.Key).Distinct().OrderBy(k => k, StringComparer.Ordinal).Select(k =>
        {
            var c = perKind.FirstOrDefault(x => x.Kind == k); var v = versions.FirstOrDefault(x => x.Kind == k); var b = bytes.FirstOrDefault(x => x.Kind == k);
            return (object)new Dictionary<string, object?>
            {
                ["kind"] = k, ["owner"] = ownership.OwnerOf(k).ToString(), ["definitions"] = c?.Definitions ?? 0, ["published"] = c?.Published ?? 0,
                ["drafts"] = v?.Drafts ?? 0, ["versions"] = v?.Versions ?? 0, ["payloadBytes"] = b?.Bytes ?? 0
            };
        }).ToList();
        return (rows, rows.Count);
    }
}

/// <summary>Where a definition is used (SCRUM-450): the definitions whose published version points at the kind and key of the filters <c>toKind</c> and <c>toKey</c>: <c>fromKind</c>, <c>fromKey</c>, <c>role</c>.</summary>
public sealed class DefinitionUsagesGridProvider(IServiceScopeFactory scopes) : IGridDataProvider
{
    public string Key => "definition-usages";
    public IReadOnlyCollection<string>? AllowedRoles => null;

    public async Task<(IReadOnlyList<object> Items, int Total)> QueryAsync(GridDataRequest request, CancellationToken ct)
    {
        var toKind = WorkbenchRows.FilterValue(request, "toKind");
        var toKey = WorkbenchRows.FilterValue(request, "toKey");
        if (toKind.Length == 0 || toKey.Length == 0) return ([], 0);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PEMSDbContext>();
        var query = db.DefinitionReferences.AsNoTracking()
            .Where(r => r.ToKind == toKind && r.ToKey == toKey && r.FromVersion!.Definition!.PublishedVersionId == r.FromVersionId)
            .Select(r => new { Kind = r.FromVersion!.Definition!.Kind, Key = r.FromVersion.Definition.Key, r.Role });
        var total = await query.CountAsync(ct);
        var page = Math.Max(1, request.Grid.Page);
        var rows = await query.OrderBy(x => x.Kind).ThenBy(x => x.Key).Skip((page - 1) * request.Grid.PageSize).Take(request.Grid.PageSize).ToListAsync(ct);
        return (rows.Select(x => (object)new Dictionary<string, object?> { ["fromKind"] = x.Kind, ["fromKey"] = x.Key, ["role"] = x.Role, ["ref"] = x.Kind + ":" + x.Key }).ToList(), total);
    }
}

/// <summary>
/// The heaviest definitions (SCRUM-455): one row per definition with <c>ref</c>, <c>kind</c>, <c>key</c>, <c>versions</c>, <c>publishedBytes</c>, <c>totalBytes</c> (every version), ordered by total size, so growth is visible where it
/// happens. Versions are kept forever; nothing here deletes.
/// </summary>
public sealed class DefinitionLargestGridProvider(IServiceScopeFactory scopes) : IGridDataProvider
{
    public string Key => "definition-largest";
    public IReadOnlyCollection<string>? AllowedRoles => null;

    public async Task<(IReadOnlyList<object> Items, int Total)> QueryAsync(GridDataRequest request, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PEMSDbContext>();
        var rows = await db.DefinitionVersions.AsNoTracking().GroupBy(v => new { v.Definition!.Kind, v.Definition.Key, v.Definition.AppKey })
            .Select(g => new { g.Key.Kind, g.Key.Key, g.Key.AppKey, Versions = g.Count(), TotalBytes = g.Sum(v => v.PayloadJson.Length) })
            .OrderByDescending(x => x.TotalBytes).Take(Math.Max(1, request.Grid.PageSize)).ToListAsync(ct);
        IReadOnlyList<object> items = rows.Select(x => (object)new Dictionary<string, object?>
            { ["ref"] = $"{x.AppKey}:{x.Kind}:{x.Key}", ["kind"] = x.Kind, ["key"] = x.Key, ["versions"] = x.Versions, ["totalBytes"] = x.TotalBytes }).ToList();
        return (items, items.Count);
    }
}

/// <summary>
/// Missing translations of published definitions (SCRUM-419, 609): every language map (an object with <c>en</c> or <c>fa</c>) of a published form, page or menu whose other language is empty or absent is one row: <c>ref</c>,
/// <c>kind</c>, <c>key</c>, <c>path</c> in the payload, <c>missing</c> (the language), <c>shown</c> (the text a user sees through the fallback). Filter <c>kind</c> narrows to one kind.
/// </summary>
public sealed class DefinitionTranslationsGridProvider(IServiceScopeFactory scopes) : IGridDataProvider
{
    private static readonly string[] Languages = ["en", "fa"];
    public string Key => "definition-translations";
    public IReadOnlyCollection<string>? AllowedRoles => null;

    public async Task<(IReadOnlyList<object> Items, int Total)> QueryAsync(GridDataRequest request, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PEMSDbContext>();
        var kind = WorkbenchRows.FilterValue(request, "kind");
        var query = db.Definitions.AsNoTracking().Where(d => d.PublishedVersionId != null && (kind == "" ? d.Kind == "form" || d.Kind == "page" || d.Kind == "menu" : d.Kind == kind));
        var published = await query.Select(d => new { d.Kind, d.Key, d.AppKey, Payload = d.PublishedVersion!.PayloadJson }).ToListAsync(ct);
        var rows = new List<object>();
        foreach (var d in published)
        {
            try
            {
                using var doc = JsonDocument.Parse(d.Payload);
                Walk(doc.RootElement, "", (path, missing, shown) => rows.Add(new Dictionary<string, object?>
                    { ["ref"] = $"{d.AppKey}:{d.Kind}:{d.Key}", ["kind"] = d.Kind, ["key"] = d.Key, ["path"] = path, ["missing"] = missing, ["shown"] = shown }));
            }
            catch (JsonException) { }
        }
        var page = Math.Max(1, request.Grid.Page);
        return (rows.Skip((page - 1) * request.Grid.PageSize).Take(request.Grid.PageSize).ToList(), rows.Count);
    }

    private static void Walk(JsonElement e, string path, Action<string, string, string> found)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            var isMap = e.EnumerateObject().Any(p => Languages.Contains(p.Name)) && e.EnumerateObject().All(p => p.Value.ValueKind == JsonValueKind.String);
            if (isMap)
            {
                foreach (var lang in Languages)
                {
                    var has = e.TryGetProperty(lang, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString());
                    if (!has)
                    {
                        var other = Languages.First(l => l != lang);
                        found(path.Length == 0 ? "/" : path, lang, e.TryGetProperty(other, out var o) ? o.GetString() ?? "" : "");
                    }
                }
                return;
            }
            foreach (var p in e.EnumerateObject()) Walk(p.Value, path + "/" + p.Name, found);
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var item in e.EnumerateArray()) Walk(item, path + "/" + i++, found);
        }
    }
}
