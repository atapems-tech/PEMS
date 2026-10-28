using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace PEMS.Web.Api;

public static class ApiServiceCollectionExtensions
{
    public static bool IsApiRequest(PathString path) => path.StartsWithSegments("/api");

    /// <summary>Registers permission policies, the problem+json model-state response and the OpenAPI 3.1 document.</summary>
    public static IServiceCollection AddPemsApi(this IServiceCollection services)
    {
        services.Configure<Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions>(o => o.HeaderName = ApiControllerBase.AntiforgeryHeader);
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionRequirementHandler>();

        services.Configure<ApiBehaviorOptions>(o =>
        {
            o.InvalidModelStateResponseFactory = ctx =>
            {
                var errors = ctx.ModelState.Where(kv => kv.Value is { Errors.Count: > 0 })
                    .SelectMany(kv => kv.Value!.Errors.Select(e => new ApiFieldError(
                        "/" + char.ToLowerInvariant(kv.Key.FirstOrDefault('x')) + (kv.Key.Length > 1 ? kv.Key[1..] : ""), "invalid", null,
                        string.IsNullOrEmpty(e.ErrorMessage) ? "The value is not valid." : e.ErrorMessage)))
                    .ToList();
                return ApiProblem.Validation(errors);
            };
        });

        services.AddOpenApi("v1", o =>
        {
            o.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_1;
            o.ShouldInclude = d => d.RelativePath?.StartsWith(ApiControllerBase.Prefix + "/", StringComparison.Ordinal) == true;
            o.AddOperationTransformer(ApiOpenApi.DescribeStandardParts);
            o.AddDocumentTransformer((doc, _, _) =>
            {
                doc.Info = new() { Title = "PEMS API", Version = "v1", Description = "Resource-oriented REST API (ADR-0009, guideline chapter 08)." };
                return Task.CompletedTask;
            });
        });
        return services;
    }

    /// <summary>Cookie sign-in answers API calls with 401/403 problem+json instead of redirecting to the login page.</summary>
    public static IServiceCollection ConfigureApiAuthenticationResponses(this IServiceCollection services)
    {
        services.PostConfigure<CookieAuthenticationOptions>(Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme, o =>
        {
            var redirectToLogin = o.Events.OnRedirectToLogin;
            var redirectToDenied = o.Events.OnRedirectToAccessDenied;
            o.Events.OnRedirectToLogin = ctx => Respond(ctx.HttpContext, ApiProblem.Unauthorized(), redirectToLogin, ctx);
            o.Events.OnRedirectToAccessDenied = ctx => Respond(ctx.HttpContext, ApiProblem.Forbidden(), redirectToDenied, ctx);
        });
        return services;

        static Task Respond(HttpContext http, ObjectResult problem, Func<RedirectContext<CookieAuthenticationOptions>, Task> fallback,
            RedirectContext<CookieAuthenticationOptions> ctx)
        {
            if (!IsApiRequest(http.Request.Path)) return fallback(ctx);
            var executor = http.RequestServices.GetRequiredService<IActionResultExecutor<ObjectResult>>();
            return executor.ExecuteAsync(new ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(),
                new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()), problem);
        }
    }
}
