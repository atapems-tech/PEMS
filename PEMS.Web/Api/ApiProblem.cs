using Microsoft.AspNetCore.Mvc;

namespace PEMS.Web.Api;

/// <summary>One field-level error of an RFC 9457 problem (guideline chapter 08, rule 8).</summary>
public sealed record ApiFieldError(string Pointer, string Code, string? MessageKey = null, string? Message = null,
    IReadOnlyDictionary<string, object?>? Params = null);

/// <summary>RFC 9457 problem details carrying the <c>errors</c> list.</summary>
public sealed class ApiProblemDetails : ProblemDetails
{
    public IReadOnlyList<ApiFieldError> Errors { get; set; } = Array.Empty<ApiFieldError>();
}

/// <summary>Builds the problem responses of the standard; every API error goes through here, never an ad hoc body.</summary>
public static class ApiProblem
{
    public const string ContentType = "application/problem+json";
    private const string TypePrefix = "urn:pems:problem:";

    public static ObjectResult Validation(IReadOnlyList<ApiFieldError> errors) =>
        Build(422, "validation", "The request is not valid.", errors);

    public static ObjectResult NotFound(string? detail = null) => Build(404, "not-found", "The resource was not found.", detail: detail);

    public static ObjectResult Conflict(string code, string? detail = null) => Build(409, code, "The request conflicts with the current state.", detail: detail);

    public static ObjectResult PreconditionFailed() =>
        Build(412, "precondition-failed", "The resource was changed by someone else; read it again.");

    public static ObjectResult PreconditionRequired() =>
        Build(428, "precondition-required", "Send the ETag you read in the If-Match header.");

    public static ObjectResult Unauthorized() => Build(401, "unauthorized", "Sign in is required.");

    public static ObjectResult Forbidden() => Build(403, "forbidden", "You are not allowed to do this.");

    public static ObjectResult BadRequest(string title, string? detail = null) => Build(400, "bad-request", title, detail: detail);

    public static ObjectResult Server() => Build(500, "server-error", "The server could not complete the request.");

    public static ObjectResult Build(int status, string code, string title, IReadOnlyList<ApiFieldError>? errors = null, string? detail = null)
    {
        var body = new ApiProblemDetails
        {
            Type = TypePrefix + code,
            Title = title,
            Status = status,
            Detail = detail,
            Errors = errors ?? Array.Empty<ApiFieldError>(),
        };
        var result = new ObjectResult(body) { StatusCode = status };
        result.ContentTypes.Add(ContentType);
        return result;
    }
}
