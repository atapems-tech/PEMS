using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Definitions;
using PEMS.Web.Security;

namespace PEMS.Web.Api.V1;

/// <summary>
/// The definition store REST API (SCRUM-434, ADR-0009/0010/0011): create, edit with If-Match, validate, publish, roll back, export and import of UI definitions.
/// Designers and integrations are ordinary clients of it. The lifecycle rules live in <see cref="IDefinitionService"/>; every data change is audited by the
/// DbContext audit trail; publish and rollback need the permission "Approve" of the module "Definitions".
/// </summary>
[Route(Prefix + "/definitions")]
public sealed class DefinitionsApiController : ApiControllerBase
{
    private const string Module = "Definitions";
    private static readonly IReadOnlySet<string> SortFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "kind", "key", "createdDate" };
    private static readonly IReadOnlySet<string> FilterFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "kind", "appKey", "published" };

    private readonly IDefinitionService _service;
    private readonly IAuthorizationService _authorization;
    private readonly ICurrentUserService _currentUser;

    public DefinitionsApiController(IDefinitionService service, IAuthorizationService authorization, ICurrentUserService currentUser)
    {
        _service = service;
        _authorization = authorization;
        _currentUser = currentUser;
    }

    private string Actor => _currentUser.UserName ?? User.Identity?.Name ?? "unknown";

    // ------------------------------------------------------------------ reads

    [HttpGet]
    [ApiPermission(Module, "View")]
    [ProducesResponseType<ApiPage<DefinitionResource>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblem.ContentType)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var errors = new List<ApiFieldError>();
        ApiListQuery.TryParse(Request.Query, out var q, out var listErrors);
        errors.AddRange(listErrors);
        ApiFields.TryParse(Request.Query, DefinitionResource.FieldNames, out var fields, out var fieldErrors);
        errors.AddRange(fieldErrors);
        foreach (var (field, _) in q.Sort.Where(s => !SortFields.Contains(s.Field))) errors.Add(new ApiFieldError("/sort", "unknown-field", "Api_Err_UnknownField", $"Cannot sort by '{field}'."));
        foreach (var f in q.Filters.Keys.Where(k => !FilterFields.Contains(k))) errors.Add(new ApiFieldError($"/filter/{f}", "unknown-field", "Api_Err_UnknownField", $"Cannot filter by '{f}'."));
        bool? published = null;
        if (q.Filters.TryGetValue("published", out var rawPublished))
        {
            if (bool.TryParse(rawPublished, out var b)) published = b;
            else errors.Add(new ApiFieldError("/filter/published", "wrong-type", null, "Use true or false."));
        }
        if (errors.Count > 0) return ApiProblem.Validation(errors);

        q.Filters.TryGetValue("kind", out var kind);
        q.Filters.TryGetValue("appKey", out var appKey);
        var page = await _service.ListAsync(new DefinitionListQuery(kind, appKey, q.Q, published, q.Sort, q.Page, q.PageSize), ct);
        var items = page.Items.Select(s => ApiFields.Project(DefinitionResource.From(s, null, Array.Empty<string>()), fields)).ToList();
        return Ok(new ApiPage<object>(items, q.Page, q.PageSize, page.Total));
    }

    [HttpGet("{id:int}")]
    [ApiPermission(Module, "View")]
    [ProducesResponseType<DefinitionResource>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status404NotFound, ApiProblem.ContentType)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var r = await _service.GetAsync(id, ct);
        if (!r.Success) return Problem(r);
        SetRevisionETag(r.Value!.Summary.Revision);
        return Ok(await ResourceAsync(r.Value));
    }

    [HttpGet("{id:int}/versions/{number:int}")]
    [ApiPermission(Module, "View")]
    [ProducesResponseType<DefinitionVersionPayloadResource>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status404NotFound, ApiProblem.ContentType)]
    public async Task<IActionResult> GetVersion(int id, int number, CancellationToken ct)
    {
        var r = await _service.GetVersionAsync(id, number, ct);
        if (!r.Success) return Problem(r);
        SetRevisionETag(r.Value!.Info.Revision);
        return Ok(DefinitionVersionPayloadResource.From(r.Value));
    }

    [HttpGet("{id:int}/draft")]
    [ApiPermission(Module, "View")]
    [ProducesResponseType<DefinitionVersionPayloadResource>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status404NotFound, ApiProblem.ContentType)]
    public async Task<IActionResult> GetDraft(int id, CancellationToken ct)
    {
        var r = await _service.GetDraftAsync(id, ct);
        if (!r.Success) return Problem(r);
        SetRevisionETag(r.Value!.Info.Revision);
        return Ok(DefinitionVersionPayloadResource.From(r.Value));
    }

    [HttpGet("{id:int}/export")]
    [ApiPermission(Module, "View")]
    [ProducesResponseType<DefinitionPackageResource>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status404NotFound, ApiProblem.ContentType)]
    public async Task<IActionResult> Export(int id, [FromQuery] int? version, CancellationToken ct)
    {
        var r = await _service.ExportAsync(id, version, ct);
        if (!r.Success) return Problem(r);
        return Ok(new DefinitionPackageResource(r.Value!.Format, DefinitionVersionResource.Utc(r.Value.ExportedAtUtc), r.Value.Items.Select(i => new DefinitionPackageItemResource(
            i.Kind, i.Key, i.AppKey, DefinitionResource.Parse(i.TitleJson)!.Value, i.DescriptionJson == null ? null : DefinitionResource.Parse(i.DescriptionJson),
            DefinitionResource.Parse(i.PayloadJson)!.Value, i.ContentHash, i.VersionNumber)).ToList()));
    }

    // ------------------------------------------------------------------ writes

    [HttpPost]
    [ApiPermission(Module, "Create")]
    [ProducesResponseType<DefinitionResource>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblem.ContentType)]
    public async Task<IActionResult> Create([FromBody] DefinitionCreateInput input, CancellationToken ct)
    {
        var missing = Missing(("title", input.Title), ("payload", input.Payload));
        if (missing.Count > 0) return ApiProblem.Validation(missing);
        var r = await _service.CreateAsync(new CreateDefinition(input.Kind ?? "", input.Key ?? "", input.AppKey ?? "core", input.Title.GetRawText(),
            input.Description is { ValueKind: not JsonValueKind.Null } d ? d.GetRawText() : null, input.Payload.GetRawText(), input.ChangeSummary), Actor, ct);
        if (!r.Success) return Problem(r);
        SetRevisionETag(r.Value!.Summary.Revision);
        return Created($"/{Prefix}/definitions/{r.Value.Summary.Id}", await ResourceAsync(r.Value));
    }

    [HttpPut("{id:int}")]
    [ApiPermission(Module, "Edit")]
    [ProducesResponseType<DefinitionResource>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status412PreconditionFailed, ApiProblem.ContentType)]
    public async Task<IActionResult> UpdateEnvelope(int id, [FromBody] DefinitionEnvelopeInput input, CancellationToken ct)
    {
        var missing = Missing(("title", input.Title));
        if (missing.Count > 0) return ApiProblem.Validation(missing);
        var (gate, revision) = await EnvelopeGateAsync(id, ct);
        if (gate != null) return gate;
        var r = await _service.UpdateEnvelopeAsync(id, input.Title.GetRawText(), input.Description is { ValueKind: not JsonValueKind.Null } d ? d.GetRawText() : null, revision, Actor, ct);
        return await DetailResultAsync(r);
    }

    [HttpPut("{id:int}/draft")]
    [ApiPermission(Module, "Edit")]
    [ProducesResponseType<DefinitionVersionPayloadResource>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status412PreconditionFailed, ApiProblem.ContentType)]
    public async Task<IActionResult> SaveDraft(int id, [FromBody] DefinitionDraftInput input, CancellationToken ct)
    {
        var missing = Missing(("payload", input.Payload));
        if (missing.Count > 0) return ApiProblem.Validation(missing);
        var (gate, revision) = await DraftGateAsync(id, ct);
        if (gate != null) return gate;
        return VersionResult(await _service.SaveDraftAsync(id, input.Payload.GetRawText(), input.ChangeSummary, revision, Actor, ct));
    }

    [HttpDelete("{id:int}/draft")]
    [ApiPermission(Module, "Edit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status412PreconditionFailed, ApiProblem.ContentType)]
    public async Task<IActionResult> DiscardDraft(int id, CancellationToken ct)
    {
        var (gate, revision) = await DraftGateAsync(id, ct);
        if (gate != null) return gate;
        var r = await _service.DiscardDraftAsync(id, revision, Actor, ct);
        return r.Success ? NoContent() : Problem(r);
    }

    [HttpPost("{id:int}/actions/create-draft")]
    [ApiPermission(Module, "Edit")]
    [ProducesResponseType<DefinitionVersionPayloadResource>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status409Conflict, ApiProblem.ContentType)]
    public async Task<IActionResult> CreateDraft(int id, [FromBody] CreateDraftInput? input, CancellationToken ct)
    {
        var (gate, revision) = await EnvelopeGateAsync(id, ct);
        if (gate != null) return gate;
        var r = await _service.CreateDraftAsync(id, input?.FromVersion, revision, Actor, ct);
        if (!r.Success) return Problem(r);
        SetRevisionETag(r.Value!.Info.Revision);
        return Created($"/{Prefix}/definitions/{id}/draft", DefinitionVersionPayloadResource.From(r.Value));
    }

    [HttpPost("{id:int}/actions/validate")]
    [ApiPermission(Module, "Edit")]
    [ProducesResponseType<DefinitionVersionPayloadResource>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblem.ContentType)]
    public async Task<IActionResult> Validate(int id, CancellationToken ct)
    {
        var (gate, revision) = await DraftGateAsync(id, ct);
        if (gate != null) return gate;
        return VersionResult(await _service.ValidateAsync(id, revision, Actor, ct));
    }

    [HttpPost("{id:int}/actions/publish")]
    [ApiPermission(Module, "Approve")]
    [ProducesResponseType<DefinitionResource>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblem.ContentType)]
    public async Task<IActionResult> Publish(int id, CancellationToken ct)
    {
        var (gate, revision) = await DraftGateAsync(id, ct);
        if (gate != null) return gate;
        return await DetailResultAsync(await _service.PublishAsync(id, revision, Actor, ct));
    }

    [HttpPost("{id:int}/actions/rollback")]
    [ApiPermission(Module, "Approve")]
    [ProducesResponseType<DefinitionResource>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status409Conflict, ApiProblem.ContentType)]
    public async Task<IActionResult> Rollback(int id, [FromBody] RollbackInput input, CancellationToken ct)
    {
        if (input.ToVersion is null or < 1) return ApiProblem.Validation(new[] { new ApiFieldError("/toVersion", "required", "Def_Err_Required", "Say which version to publish again.") });
        var (gate, revision) = await EnvelopeGateAsync(id, ct);
        if (gate != null) return gate;
        return await DetailResultAsync(await _service.RollbackAsync(id, input.ToVersion.Value, revision, Actor, ct));
    }

    [HttpPost("import")]
    [ApiPermission(Module, "Create")]
    [ProducesResponseType<DefinitionImportResource>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblem.ContentType)]
    public async Task<IActionResult> Import([FromBody] DefinitionPackageResource package, CancellationToken ct)
    {
        var items = package.Items?.Select(i => new DefinitionPackageItem(i.Kind ?? "", i.Key ?? "", i.AppKey ?? "core", i.Title.ValueKind == JsonValueKind.Undefined ? "" : i.Title.GetRawText(),
            i.Description is { ValueKind: not JsonValueKind.Null } d ? d.GetRawText() : null, i.Payload.ValueKind == JsonValueKind.Undefined ? "" : i.Payload.GetRawText(), i.ContentHash ?? "", i.VersionNumber)).ToList()
            ?? new List<DefinitionPackageItem>();
        var r = await _service.ImportAsync(new DefinitionPackage(package.Format ?? "", package.ExportedAt, items), Actor, ct);
        if (!r.Success) return Problem(r);
        return StatusCode(StatusCodes.Status201Created, new DefinitionImportResource(r.Value!.Created, r.Value.Updated, r.Value.DefinitionIds));
    }

    // ------------------------------------------------------------------ helpers

    private static List<ApiFieldError> Missing(params (string Name, JsonElement Value)[] required) =>
        required.Where(r => r.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            .Select(r => new ApiFieldError("/" + r.Name, "required", "Def_Err_Required", $"{r.Name} is required.")).ToList();

    private void SetRevisionETag(long revision) => Response.Headers.ETag = ApiETag.Format(BitConverter.GetBytes(revision));

    /// <summary>404, then 428 without If-Match, then 412 when stale; otherwise the envelope revision the write must still match.</summary>
    private async Task<(IActionResult? Gate, long Revision)> EnvelopeGateAsync(int id, CancellationToken ct)
    {
        var r = await _service.GetAsync(id, ct);
        if (!r.Success) return (Problem(r), 0);
        return (Precondition(r.Value!.Summary.Revision), r.Value.Summary.Revision);
    }

    private async Task<(IActionResult? Gate, long Revision)> DraftGateAsync(int id, CancellationToken ct)
    {
        var r = await _service.GetDraftAsync(id, ct);
        if (!r.Success) return (Problem(r), 0);
        return (Precondition(r.Value!.Info.Revision), r.Value.Info.Revision);
    }

    private IActionResult? Precondition(long currentRevision) =>
        ApiETag.Evaluate(Request, BitConverter.GetBytes(currentRevision)) switch
        {
            ApiETag.Check.Missing => ApiProblem.PreconditionRequired(),
            ApiETag.Check.Stale => ApiProblem.PreconditionFailed(),
            _ => null,
        };

    private IActionResult VersionResult(DefinitionOutcome<DefinitionVersionPayload> r)
    {
        if (!r.Success) return Problem(r);
        SetRevisionETag(r.Value!.Info.Revision);
        return Ok(DefinitionVersionPayloadResource.From(r.Value));
    }

    private async Task<IActionResult> DetailResultAsync(DefinitionOutcome<DefinitionDetail> r)
    {
        if (!r.Success) return Problem(r);
        SetRevisionETag(r.Value!.Summary.Revision);
        return Ok(await ResourceAsync(r.Value));
    }

    private async Task<DefinitionResource> ResourceAsync(DefinitionDetail d)
    {
        var allowed = new List<string>();
        var canEdit = (await _authorization.AuthorizeAsync(User, null, ApiPermissionAttribute.Prefix + Module + ".Edit")).Succeeded;
        var canPublish = (await _authorization.AuthorizeAsync(User, null, ApiPermissionAttribute.Prefix + Module + ".Approve")).Succeeded;
        var s = d.Summary;
        if (canEdit)
        {
            allowed.Add("edit");
            if (s.HasDraft) { allowed.Add("save-draft"); allowed.Add("validate"); if (d.Versions.Count > 1) allowed.Add("discard-draft"); }
            else if (s.PublishedVersion != null) allowed.Add("create-draft");
        }
        if (canPublish)
        {
            if (s.HasDraft) allowed.Add("publish");
            if (d.Versions.Count(v => v.Status is PEMS.Domain.Entities.Definitions.DefinitionStatus.Published or PEMS.Domain.Entities.Definitions.DefinitionStatus.Retired) > 1) allowed.Add("rollback");
        }
        return DefinitionResource.From(s, d.Versions, allowed);
    }

    private IActionResult Problem<T>(DefinitionOutcome<T> r) => r.Failure switch
    {
        DefinitionFailure.NotFound => ApiProblem.Build(404, r.Code ?? "not-found", "The resource was not found."),
        DefinitionFailure.Concurrency => ApiProblem.PreconditionFailed(),
        DefinitionFailure.Conflict => ApiProblem.Conflict(r.Code ?? "conflict"),
        DefinitionFailure.Invalid => ApiProblem.Validation((r.Errors ?? Array.Empty<DefinitionFieldError>())
            .Select(e => new ApiFieldError(e.Pointer, e.Code, e.MessageKey, e.Message)).ToList()),
        _ => ApiProblem.Server(),
    };
}
