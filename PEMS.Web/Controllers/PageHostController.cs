using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Definitions;
using PEMS.Web.Definitions;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>The one generic host of the page engine (SCRUM-438, ADR-0011 point 8, ADR-0014): <c>/p/{key}</c> runs the pipeline for a page definition (resolve, authorize, hydrate) and renders it. The reserved prefix never shadows a code route; the page is whatever its definition says.</summary>
[Route("p/{key}")]
public sealed class PageHostController(DefinitionPipeline pipeline, PageRenderer renderer, ICurrentUserService currentUser, ILogger<PageHostController> log) : Controller
{
    private static readonly Regex KeyShape = new("^[a-z0-9][a-z0-9._-]{0,80}$", RegexOptions.Compiled);

    [HttpGet("")]
    public async Task<IActionResult> Index(string key, CancellationToken ct)
    {
        if (!KeyShape.IsMatch(key)) return NotFound();
        var culture = CultureInfo.CurrentUICulture;
        var roles = User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
        var request = new PipelineRequest("page", key, "core", culture.TwoLetterISOLanguageName, roles, currentUser.UserName);

        var result = await pipeline.RunAsync(request, ct);
        if (result.Status == PipelineStatus.NotFound) return NotFound();
        if (result.Status == PipelineStatus.Forbidden) return Forbid();

        var page = await renderer.RenderAsync(result.Resolved!, request, culture, ct);
        foreach (var problem in page.Problems) log.LogWarning("Page definition {Key}: {Problem}", key, problem);
        ViewData["Title"] = page.Title ?? key;
        return View("Index", page);
    }
}
