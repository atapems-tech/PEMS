using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PEMS.Web.Api;

/// <summary>Base of every REST controller: problem+json errors, route prefix <see cref="Prefix"/>. No [Produces] here: it would override the problem+json content type of error results.</summary>
[ApiController]
[AutoValidateAntiforgeryToken]   // chapter 08, rule 14: cookie sign-in needs the antiforgery header on every unsafe verb
public abstract class ApiControllerBase : ControllerBase
{
    public const string Prefix = "api/v1";
    public const string AntiforgeryHeader = "X-CSRF-TOKEN";

    /// <summary>Sets the ETag header for a single-resource response.</summary>
    protected void SetETag(byte[]? rowVersion) => Response.Headers.ETag = ApiETag.Format(rowVersion);
}

/// <summary>Turns unhandled exceptions of an API controller into an RFC 9457 500 without leaking the stack trace.</summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _log;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> log) => _log = log;

    public void OnException(ExceptionContext context)
    {
        if (context.ActionDescriptor is not Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor d
            || !typeof(ApiControllerBase).IsAssignableFrom(d.ControllerTypeInfo)) return;
        _log.LogError(context.Exception, "API error on {Path}", context.HttpContext.Request.Path);
        context.Result = ApiProblem.Server();
        context.ExceptionHandled = true;
    }
}
