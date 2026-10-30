using PEMS.Domain.Entities.Definitions;

namespace PEMS.Application.Definitions;

/// <summary>Starting values of the limits in the design document, section 6; settings <c>Definitions:*</c> in env/*.env.</summary>
public sealed class DefinitionLimits
{
    public int MaxPayloadBytes { get; init; } = 1_048_576;
    public int MaxInstancesPerPage { get; init; } = 500;
    public int MaxJsonDepth { get; init; } = 32;
}

public sealed record DefinitionFieldError(string Pointer, string Code, string MessageKey, string Message);

/// <summary>What a registered kind knows about its payload. A kind that is not registered cannot be stored.</summary>
public interface IDefinitionKind
{
    string Key { get; }
    /// <summary>Checks the payload; an empty list means valid. Pointers are JSON Pointers into the payload.</summary>
    IReadOnlyList<DefinitionFieldError> Validate(string payloadJson, DefinitionLimits limits);
    /// <summary>The definitions and components the payload points to (written to DefinitionReferences).</summary>
    IReadOnlyList<DefinitionRef> References(string payloadJson);
}

public sealed record DefinitionRef(string ToKind, string ToKey, string Role);

public enum DefinitionFailure { None, NotFound, Concurrency, Conflict, Invalid }

public sealed record DefinitionOutcome<T>(T? Value, DefinitionFailure Failure = DefinitionFailure.None, string? Code = null,
    IReadOnlyList<DefinitionFieldError>? Errors = null)
{
    public bool Success => Failure == DefinitionFailure.None;
    public static DefinitionOutcome<T> Ok(T value) => new(value);
    public static DefinitionOutcome<T> Fail(DefinitionFailure f, string code, IReadOnlyList<DefinitionFieldError>? errors = null) => new(default, f, code, errors);
    public static DefinitionOutcome<T> NotFound() => Fail(DefinitionFailure.NotFound, "not-found");
    public static DefinitionOutcome<T> Stale() => Fail(DefinitionFailure.Concurrency, "precondition-failed");
    public static DefinitionOutcome<T> Invalid(IReadOnlyList<DefinitionFieldError> errors) => Fail(DefinitionFailure.Invalid, "validation", errors);
}

public sealed record DefinitionVersionInfo(int Number, DefinitionStatus Status, string ContentHash, string? ChangeSummary, int? ClonedFromNumber,
    DateTime CreatedAtUtc, DateTime? ValidatedAtUtc, DateTime? PublishedAtUtc, string? PublishedBy, DateTime? RetiredAtUtc, long Revision);

public sealed record DefinitionVersionPayload(DefinitionVersionInfo Info, string PayloadJson);

public sealed record DefinitionSummary(int Id, string Kind, string Key, string AppKey, DefinitionOrigin Origin, string TitleJson, string? DescriptionJson,
    int? PublishedVersion, bool HasDraft, bool IsCustomized, long Revision, DateTime CreatedAtUtc);

public sealed record DefinitionDetail(DefinitionSummary Summary, IReadOnlyList<DefinitionVersionInfo> Versions);

public sealed record DefinitionListQuery(string? Kind, string? AppKey, string? Q, bool? Published, IReadOnlyList<(string Field, bool Descending)> Sort, int Page, int PageSize);

public sealed record DefinitionPageResult(IReadOnlyList<DefinitionSummary> Items, int Total);

public sealed record CreateDefinition(string Kind, string Key, string AppKey, string TitleJson, string? DescriptionJson, string PayloadJson, string? ChangeSummary);

/// <summary>Export/import package: hash-checked now; the signature of the signed package comes with #23.</summary>
public sealed record DefinitionPackageItem(string Kind, string Key, string AppKey, string TitleJson, string? DescriptionJson, string PayloadJson, string ContentHash, int VersionNumber);

public sealed record DefinitionPackage(string Format, DateTime ExportedAtUtc, IReadOnlyList<DefinitionPackageItem> Items)
{
    public const string CurrentFormat = "pems.definitions/1";
}

public sealed record DefinitionImportResult(int Created, int Updated, IReadOnlyList<int> DefinitionIds);

/// <summary>Write side (designers and integrations through the REST API). Every method that changes data takes the signed-in user's name.</summary>
public interface IDefinitionService
{
    Task<DefinitionPageResult> ListAsync(DefinitionListQuery query, CancellationToken ct = default);
    Task<DefinitionOutcome<DefinitionDetail>> GetAsync(int id, CancellationToken ct = default);
    Task<DefinitionOutcome<DefinitionVersionPayload>> GetVersionAsync(int id, int versionNumber, CancellationToken ct = default);
    Task<DefinitionOutcome<DefinitionVersionPayload>> GetDraftAsync(int id, CancellationToken ct = default);

    Task<DefinitionOutcome<DefinitionDetail>> CreateAsync(CreateDefinition create, string actor, CancellationToken ct = default);
    Task<DefinitionOutcome<DefinitionDetail>> UpdateEnvelopeAsync(int id, string titleJson, string? descriptionJson, long expectedRevision, string actor, CancellationToken ct = default);
    /// <summary>Opens a draft cloned from the published version (or from <paramref name="fromVersion"/>); one open draft per definition.</summary>
    Task<DefinitionOutcome<DefinitionVersionPayload>> CreateDraftAsync(int id, int? fromVersion, long expectedEnvelopeRevision, string actor, CancellationToken ct = default);
    Task<DefinitionOutcome<DefinitionVersionPayload>> SaveDraftAsync(int id, string payloadJson, string? changeSummary, long expectedDraftRevision, string actor, CancellationToken ct = default);
    Task<DefinitionOutcome<DefinitionVersionPayload>> ValidateAsync(int id, long expectedDraftRevision, string actor, CancellationToken ct = default);
    Task<DefinitionOutcome<DefinitionDetail>> PublishAsync(int id, long expectedDraftRevision, string actor, CancellationToken ct = default);
    Task<DefinitionOutcome<DefinitionDetail>> RollbackAsync(int id, int toVersion, long expectedEnvelopeRevision, string actor, CancellationToken ct = default);
    Task<DefinitionOutcome<DefinitionDetail>> DiscardDraftAsync(int id, long expectedDraftRevision, string actor, CancellationToken ct = default);

    /// <summary>Exports the published version, or <paramref name="version"/> when given.</summary>
    Task<DefinitionOutcome<DefinitionPackage>> ExportAsync(int id, int? version, CancellationToken ct = default);
    Task<DefinitionOutcome<DefinitionImportResult>> ImportAsync(DefinitionPackage package, string actor, CancellationToken ct = default);
}

public sealed record PublishedDefinition(string Kind, string Key, string AppKey, int VersionNumber, string PayloadJson, string ContentHash);

/// <summary>Read side for the runtime (page host, renderers): the published version only, cached and refreshed by the per-kind stamp.</summary>
public interface IDefinitionStore
{
    Task<PublishedDefinition?> GetPublishedAsync(string kind, string key, string appKey = "core", CancellationToken ct = default);
}
