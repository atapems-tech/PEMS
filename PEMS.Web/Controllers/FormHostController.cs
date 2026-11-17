using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Definitions;
using PEMS.UI.Abstractions;
using PEMS.UI.Rendering;
using PEMS.Web.Definitions;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// The one generic host of the form engine (SCRUM-578, ADR-0014): <c>/f/{key}</c> runs the pipeline for a form definition (resolve, authorize, hydrate), evaluates its conditions (ADR-0015) and renders it
/// with the form component; the post validates the values from the definition (both languages) and hands the valid ones to the action definition named by the form (SCRUM-437). Nothing about a form lives
/// in code: a changed definition is a changed form.
/// </summary>
[Route("f/{key}")]
public sealed class FormHostController(DefinitionPipeline pipeline, ActionExecutor actions, ICurrentUserService currentUser, IAntiforgery antiforgery, IPemsComponentRenderer renderer, ILogger<FormHostController> log) : Controller
{
    private static readonly Regex KeyShape = new("^[a-z0-9][a-z0-9._-]{0,80}$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Strict = new(PemsJson.Options);

    [HttpGet("")]
    public async Task<IActionResult> Index(string key, CancellationToken ct)
    {
        var loaded = await LoadAsync(key, ct);
        if (loaded.Early is not null) return loaded.Early;
        var values = loaded.Form!.Fields.Where(f => !string.IsNullOrEmpty(f.Name)).ToDictionary(f => f.Name, f => (string?)null, StringComparer.Ordinal);
        foreach (var (name, v) in Request.Query) if (values.ContainsKey(name)) values[name] = v.ToString();   // a link may pre-fill a field
        return await Show(key, loaded, values, new Dictionary<string, TranslatableText>(), []);
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(string key, CancellationToken ct)
    {
        var loaded = await LoadAsync(key, ct);
        if (loaded.Early is not null) return loaded.Early;
        var form = loaded.Form!;
        var posted = form.Fields.ToDictionary(f => f.Name, f => (string?)(Request.Form.TryGetValue(f.Name, out var v) ? v.ToString() : null), StringComparer.Ordinal);   // only the declared names are read
        // the choices a Select accepts are the ones the data source gave this user, so the post is checked against the form as it was shown
        var shown = FormEngine.Effective(form, FormEngine.Evaluate(form, posted, loaded.Context), loaded.Options);
        var validation = FormEngine.Validate(shown, posted, loaded.Context);
        if (!validation.IsValid) return await Show(key, loaded, posted, validation.Errors, validation.FormErrors);

        if (string.IsNullOrEmpty(form.ActionKey)) return await Show(key, loaded, posted, validation.Errors, [TranslatableText.Of(("en", "This form has no action."), ("fa", "این فرم کنشی ندارد."))]);
        var result = await actions.RunAsync(form.ActionKey, new ActionInvocation(validation.Values, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, loaded.Context.Roles, loaded.Context.UserName), "core", ct);
        switch (result.Status)
        {
            case ActionStatus.Forbidden: return Forbid();
            case ActionStatus.NotFound: log.LogWarning("Form definition {Key}: the action \"{Action}\" does not exist.", key, form.ActionKey); return NotFound();
            case ActionStatus.Refused:
                return await Show(key, loaded, posted, result.Outcome!.FieldErrors ?? new Dictionary<string, TranslatableText>(), result.Outcome.FormError is { } fe ? [fe] : []);
        }
        if (result.Message is { IsEmpty: false } message) TempData["Message"] = message.Resolve(CultureInfo.CurrentUICulture);
        var target = result.Redirect;
        return target is not null && Url.IsLocalUrl(target) ? LocalRedirect(target) : RedirectToAction(nameof(Index), new { key });
    }

    private sealed record Loaded(IActionResult? Early, FormDefinition? Form, FormRequestContext Context, IReadOnlyDictionary<string, IReadOnlyList<FieldOption>> Options, string? Title);

    private async Task<Loaded> LoadAsync(string key, CancellationToken ct)
    {
        var context = new FormRequestContext(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, key,
            User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList(), currentUser.UserName);
        if (!KeyShape.IsMatch(key)) return new Loaded(NotFound(), null, context, new Dictionary<string, IReadOnlyList<FieldOption>>(), null);

        var request = new PipelineRequest("form", key, "core", context.Culture, context.Roles, context.UserName);
        var result = await pipeline.RunAsync(request, ct);
        if (result.Status == PipelineStatus.NotFound) return new Loaded(NotFound(), null, context, new Dictionary<string, IReadOnlyList<FieldOption>>(), null);
        if (result.Status == PipelineStatus.Forbidden) return new Loaded(Forbid(), null, context, new Dictionary<string, IReadOnlyList<FieldOption>>(), null);

        var form = result.Resolved!.Payload.Deserialize<FormDefinition>(Strict)!;
        var options = result.Resolved.Hydrated.TryGetValue(FormOptionsHydrator.OptionsKey, out var o) && o is IReadOnlyDictionary<string, IReadOnlyList<FieldOption>> map ? map : new Dictionary<string, IReadOnlyList<FieldOption>>();
        if (result.Resolved.Hydrated.TryGetValue(FormOptionsHydrator.ProblemsKey, out var p) && p is IEnumerable<string> problems)
            foreach (var problem in problems) log.LogWarning("Form definition {Key}: {Problem}", key, problem);
        return new Loaded(null, form with { Id = key, Action = Url.Action(nameof(Submit), new { key })! }, context, options, PageRenderer.TitleOf(result.Resolved.Source.TitleJson, CultureInfo.CurrentUICulture));
    }

    private async Task<IActionResult> Show(string key, Loaded loaded, IReadOnlyDictionary<string, string?> values, IReadOnlyDictionary<string, TranslatableText> errors, IReadOnlyList<TranslatableText> formErrors)
    {
        var conditions = FormEngine.Evaluate(loaded.Form!, values, loaded.Context);
        var shown = FormEngine.Effective(loaded.Form!, conditions, loaded.Options);
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        var hidden = conditions.Where(c => !c.Value.Visible).Select(c => c.Key).ToList();
        var rules = JsonSerializer.Serialize(new
        {
            user = new { name = loaded.Context.UserName, roles = loaded.Context.Roles },
            request = new { culture = loaded.Context.Culture, key },
            fields = shown.Fields.Select(f => new { name = f.Name, kind = f.Kind.ToString().ToLowerInvariant(), visible = f.Visible, requiredWhen = f.RequiredWhen, readOnlyWhen = f.ReadOnlyWhen, required = loaded.Form!.Fields.First(x => x.Name == f.Name).Required, readOnly = loaded.Form.Fields.First(x => x.Name == f.Name).ReadOnly })
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        var state = new FormState { Values = values, Errors = errors, Hidden = hidden, FormErrors = formErrors };
        var html = await renderer.RenderAsync("form", new Dictionary<string, object?> { ["Definition"] = shown, ["State"] = state, ["RequestToken"] = tokens.RequestToken, ["RequestTokenName"] = tokens.FormFieldName ?? "__RequestVerificationToken", ["RulesJson"] = rules }, CultureInfo.CurrentUICulture);
        ViewData["Title"] = loaded.Title ?? key;
        return View("~/Views/PageHost/Index.cshtml", new RenderedPage(html, loaded.Title, []));
    }
}

/// <summary>A stored action on its own (SCRUM-437): <c>POST /a/{key}</c> with the values of the action as form fields runs the action definition, for a button on a page that is not a form. Same rights, same handler.</summary>
[Route("a/{key}")]
public sealed class ActionHostController(ActionExecutor actions, ICurrentUserService currentUser) : Controller
{
    private static readonly Regex KeyShape = new("^[a-z0-9][a-z0-9._-]{0,80}$", RegexOptions.Compiled);

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(string key, string? returnUrl, CancellationToken ct)
    {
        if (!KeyShape.IsMatch(key)) return NotFound();
        var values = Request.Form.Where(p => p.Key != "__RequestVerificationToken" && p.Key != "returnUrl").ToDictionary(p => p.Key, p => (string?)p.Value.ToString(), StringComparer.Ordinal);
        var roles = User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
        var result = await actions.RunAsync(key, new ActionInvocation(values, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, roles, currentUser.UserName), "core", ct);
        switch (result.Status)
        {
            case ActionStatus.NotFound: return NotFound();
            case ActionStatus.Forbidden: return Forbid();
            case ActionStatus.Refused:
                TempData["Error"] = (result.Outcome!.FormError ?? result.Outcome.FieldErrors?.Values.FirstOrDefault())?.Resolve(CultureInfo.CurrentUICulture);
                break;
            default:
                if (result.Message is { IsEmpty: false } m) TempData["Message"] = m.Resolve(CultureInfo.CurrentUICulture);
                break;
        }
        var target = result.Redirect ?? returnUrl;
        return !string.IsNullOrEmpty(target) && Url.IsLocalUrl(target) ? LocalRedirect(target) : Redirect("~/");
    }
}
