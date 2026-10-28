using System.Text.Json;
using PEMS.Application.Definitions;

namespace PEMS.Web.Api.V1;

public sealed record DefinitionCreateInput(string? Kind, string? Key, string? AppKey, JsonElement Title, JsonElement? Description, JsonElement Payload, string? ChangeSummary);

public sealed record DefinitionEnvelopeInput(JsonElement Title, JsonElement? Description);

public sealed record DefinitionDraftInput(JsonElement Payload, string? ChangeSummary);

public sealed record CreateDraftInput(int? FromVersion);

public sealed record RollbackInput(int? ToVersion);

public sealed record DefinitionVersionResource(int Number, string Status, string ContentHash, string? ChangeSummary, int? ClonedFromVersion, DateTime CreatedAt,
    DateTime? ValidatedAt, DateTime? PublishedAt, string? PublishedBy, DateTime? RetiredAt)
{
    public static DefinitionVersionResource From(DefinitionVersionInfo v) =>
        new(v.Number, v.Status.ToString(), v.ContentHash, v.ChangeSummary, v.ClonedFromNumber, Utc(v.CreatedAtUtc), Utc(v.ValidatedAtUtc), Utc(v.PublishedAtUtc), v.PublishedBy, Utc(v.RetiredAtUtc));

    internal static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);
    internal static DateTime? Utc(DateTime? d) => d is { } x ? Utc(x) : null;
}

public sealed record DefinitionResource(int Id, string Kind, string Key, string AppKey, string Origin, JsonElement Title, JsonElement? Description,
    int? PublishedVersion, bool HasDraft, bool IsCustomized, DateTime CreatedAt, IReadOnlyList<DefinitionVersionResource>? Versions, IReadOnlyList<string> AllowedActions)
{
    public static readonly IReadOnlySet<string> FieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "id", "kind", "key", "appKey", "origin", "title", "description", "publishedVersion", "hasDraft", "isCustomized", "createdAt", "versions", "allowedActions" };

    public static DefinitionResource From(DefinitionSummary s, IReadOnlyList<DefinitionVersionInfo>? versions, IReadOnlyList<string> allowed) =>
        new(s.Id, s.Kind, s.Key, s.AppKey, s.Origin.ToString(), Parse(s.TitleJson)!.Value, s.DescriptionJson == null ? null : Parse(s.DescriptionJson), s.PublishedVersion,
            s.HasDraft, s.IsCustomized, DefinitionVersionResource.Utc(s.CreatedAtUtc), versions?.Select(DefinitionVersionResource.From).ToList(), allowed);

    internal static JsonElement? Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}

/// <summary>A version with its payload: the draft or a historical version.</summary>
public sealed record DefinitionVersionPayloadResource(DefinitionVersionResource Version, JsonElement Payload)
{
    public static DefinitionVersionPayloadResource From(DefinitionVersionPayload p) =>
        new(DefinitionVersionResource.From(p.Info), JsonDocument.Parse(p.PayloadJson).RootElement.Clone());
}

public sealed record DefinitionPackageResource(string Format, DateTime ExportedAt, IReadOnlyList<DefinitionPackageItemResource> Items);

public sealed record DefinitionPackageItemResource(string Kind, string Key, string AppKey, JsonElement Title, JsonElement? Description, JsonElement Payload, string ContentHash, int VersionNumber);

public sealed record DefinitionImportResource(int Created, int Updated, IReadOnlyList<int> Ids);
