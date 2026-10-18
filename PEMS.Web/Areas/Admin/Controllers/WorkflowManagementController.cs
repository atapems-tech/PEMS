using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Identity;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// Phase 4 - Administration → Workflow Management. کنترلر نازک: هیچ منطق/EF ای اینجا نیست؛ همه‌ی عملیات از IWorkflowAdministrationService
/// (روی Workflow Platform موجود) می‌گذرد که خودش SystemAdmin + WorkflowManagement.* را سمت سرور اعمال می‌کند.
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class WorkflowManagementController : Controller
{
    private readonly IWorkflowAdministrationService _admin;
    private readonly ICurrentUserContext _user;

    public WorkflowManagementController(IWorkflowAdministrationService admin, ICurrentUserContext user)
    {
        _admin = admin; _user = user;
    }

    private WorkflowAdminActor Actor() => new(_user.UserId ?? 0, User.Identity?.Name ?? "user#" + (_user.UserId ?? 0), _user.Roles);

    private static string L(string fa, string en) => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa" ? fa : en;

    private async Task<Dictionary<string, bool>> PermsAsync()
    {
        var d = new Dictionary<string, bool>();
        foreach (var a in WorkflowManagementPermissions.All) d[a] = (await _admin.AuthorizeAsync(Actor(), a)).Success;
        return d;
    }

    private IActionResult Done(WorkflowAdminResult r, string okMessage, Func<IActionResult> onOk, Func<IActionResult> onFail)
    {
        if (r.Success) { TempData["Message"] = okMessage; return onOk(); }
        TempData["Error"] = r.Error;
        if (r.Validation != null)
            TempData["ValidationJson"] = JsonSerializer.Serialize(r.Validation.Issues.Select(i => new { i.Code, Severity = i.Severity.ToString(), Message = PEMS.Persistence.Workflow.WorkflowValidationText.Localize(i.Code, i.Message), i.StageId, i.TransitionId }));
        return onFail();
    }

    // ---------------- Views ----------------
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = L("مدیریت گردش‌کار", "Workflow Management"); ViewData["Section"] = "Administration";
        var auth = await _admin.AuthorizeAsync(Actor(), WorkflowManagementPermissions.View);
        if (!auth.Success) return View("Denied", auth.Error);
        ViewBag.Perms = await PermsAsync();
        return View(await _admin.ListAsync(Actor()));
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = L("جزئیات گردش‌کار", "Workflow Details"); ViewData["Section"] = "Administration";
        var auth = await _admin.AuthorizeAsync(Actor(), WorkflowManagementPermissions.View);
        if (!auth.Success) return View("Denied", auth.Error);
        var m = await _admin.GetDetailsAsync(Actor(), id);
        if (m == null) return NotFound();
        ViewBag.Perms = await PermsAsync();
        return View(m);
    }

    public async Task<IActionResult> Designer(int versionId, int? stage, int? transition, bool addStage = false, bool addTransition = false)
    {
        ViewData["Title"] = L("طراح گردش‌کار", "Workflow Designer"); ViewData["Section"] = "Administration";
        var auth = await _admin.AuthorizeAsync(Actor(), WorkflowManagementPermissions.View);
        if (!auth.Success) return View("Denied", auth.Error);
        var m = await _admin.GetDesignerAsync(Actor(), versionId);
        if (m == null) return NotFound();
        ViewBag.Perms = await PermsAsync();
        ViewBag.SelectedStage = stage; ViewBag.SelectedTransition = transition; ViewBag.AddStage = addStage; ViewBag.AddTransition = addTransition;
        if (TempData["ValidationJson"] is string json) ViewBag.Validation = JsonSerializer.Deserialize<List<ValidationRow>>(json);
        return View(m);
    }

    public sealed record ValidationRow(string Code, string Severity, string Message, int? StageId, int? TransitionId);

    public async Task<IActionResult> Bindings()
    {
        ViewData["Title"] = L("اتصال‌های گردش‌کار", "Workflow Bindings"); ViewData["Section"] = "Administration";
        var auth = await _admin.AuthorizeAsync(Actor(), WorkflowManagementPermissions.View);
        if (!auth.Success) return View("Denied", auth.Error);
        ViewBag.Perms = await PermsAsync();
        return View(await _admin.ListBindingsAsync(Actor()));
    }

    public async Task<IActionResult> BindingImpact(int bindingId, int targetVersionId)
    {
        ViewData["Title"] = L("تأیید تغییر اتصال", "Confirm Binding Change"); ViewData["Section"] = "Administration";
        var auth = await _admin.AuthorizeAsync(Actor(), WorkflowManagementPermissions.Bind);
        if (!auth.Success) return View("Denied", auth.Error);
        var m = await _admin.GetBindingImpactAsync(Actor(), bindingId, targetVersionId);
        if (m == null) return NotFound();
        ViewBag.TargetVersionId = targetVersionId;
        return View(m);
    }

    // ---------------- Commands ----------------
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CloneVersion(int versionId, int definitionId, string? summary)
    {
        var r = await _admin.CloneVersionAsync(Actor(), versionId, summary);
        return Done(r, L("Draft جدید ساخته شد.", "A new draft was created."), () => RedirectToAction(nameof(Designer), new { versionId = r.Id }), () => RedirectToAction(nameof(Details), new { id = definitionId }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDefinition(int id, string name, string? nameFa, string? description)
    {
        var r = await _admin.UpdateDefinitionAsync(Actor(), id, name, nameFa, description);
        return Done(r, L("مشخصات تعریف ذخیره شد.", "Definition details saved."), () => RedirectToAction(nameof(Details), new { id }), () => RedirectToAction(nameof(Details), new { id }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DiscardDraft(int versionId, int definitionId, string? rowVersion)
    {
        var r = await _admin.DiscardDraftAsync(Actor(), versionId, rowVersion);
        return Done(r, L("Draft رها شد.", "The draft was discarded."), () => RedirectToAction(nameof(Details), new { id = definitionId }), () => RedirectToAction(nameof(Designer), new { versionId }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveStage(int versionId, string? rowVersion, WorkflowStageEditModel stage)
    {
        var r = stage.StageId == 0 ? await _admin.AddStageAsync(Actor(), versionId, rowVersion, stage) : await _admin.UpdateStageAsync(Actor(), versionId, rowVersion, stage);
        int? sel = stage.StageId == 0 ? null : stage.StageId;
        return Done(r, L("مرحله ذخیره شد.", "Stage saved."), () => RedirectToAction(nameof(Designer), new { versionId, stage = sel }), () => RedirectToAction(nameof(Designer), new { versionId, stage = sel }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteStage(int versionId, string? rowVersion, int stageId)
    {
        var r = await _admin.RemoveStageAsync(Actor(), versionId, rowVersion, stageId);
        return Done(r, L("مرحله حذف شد.", "Stage removed."), () => RedirectToAction(nameof(Designer), new { versionId }), () => RedirectToAction(nameof(Designer), new { versionId, stage = stageId }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTransition(int versionId, string? rowVersion, WorkflowTransitionEditModel transition)
    {
        var r = transition.TransitionId == 0 ? await _admin.AddTransitionAsync(Actor(), versionId, rowVersion, transition) : await _admin.UpdateTransitionAsync(Actor(), versionId, rowVersion, transition);
        int? sel = transition.TransitionId == 0 ? null : transition.TransitionId;
        return Done(r, L("گذار ذخیره شد.", "Transition saved."), () => RedirectToAction(nameof(Designer), new { versionId, transition = sel }), () => RedirectToAction(nameof(Designer), new { versionId, transition = sel }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTransition(int versionId, string? rowVersion, int transitionId)
    {
        var r = await _admin.RemoveTransitionAsync(Actor(), versionId, rowVersion, transitionId);
        return Done(r, L("گذار حذف شد.", "Transition removed."), () => RedirectToAction(nameof(Designer), new { versionId }), () => RedirectToAction(nameof(Designer), new { versionId, transition = transitionId }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Validate(int versionId, string? rowVersion)
    {
        var r = await _admin.ValidateAsync(Actor(), versionId, rowVersion);
        if (r.Success && r.Validation != null && r.Validation.Warnings.Any())
            TempData["ValidationJson"] = JsonSerializer.Serialize(r.Validation.Issues.Select(i => new { i.Code, Severity = i.Severity.ToString(), Message = PEMS.Persistence.Workflow.WorkflowValidationText.Localize(i.Code, i.Message), i.StageId, i.TransitionId }));
        return Done(r, L("گردش‌کار معتبر است.", "The workflow is valid."), () => RedirectToAction(nameof(Designer), new { versionId }), () => RedirectToAction(nameof(Designer), new { versionId }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int versionId, int definitionId, string? rowVersion)
    {
        var r = await _admin.PublishAsync(Actor(), versionId, rowVersion);
        return Done(r, L("نسخه منتشر شد و اکنون تغییرناپذیر است.", "The version was published and is now immutable."), () => RedirectToAction(nameof(Details), new { id = definitionId }), () => RedirectToAction(nameof(Designer), new { versionId }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Retire(int versionId, int definitionId)
    {
        var r = await _admin.RetireAsync(Actor(), versionId);
        return Done(r, L("نسخه بازنشسته شد.", "The version was retired."), () => RedirectToAction(nameof(Details), new { id = definitionId }), () => RedirectToAction(nameof(Details), new { id = definitionId }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var r = await _admin.ArchiveDefinitionAsync(Actor(), id);
        return Done(r, L("تعریف بایگانی شد.", "The definition was archived."), () => RedirectToAction(nameof(Index)), () => RedirectToAction(nameof(Details), new { id }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeBinding(int bindingId, int targetVersionId, int definitionId, bool confirmed)
    {
        var r = await _admin.ChangeBindingAsync(Actor(), bindingId, targetVersionId, confirmed);
        return Done(r, L("Binding تغییر کرد؛ فقط Instance های جدید از نسخه‌ی جدید استفاده می‌کنند.", "The binding was changed; only new instances use the new version."),
            () => RedirectToAction(nameof(Details), new { id = definitionId }), () => RedirectToAction(nameof(BindingImpact), new { bindingId, targetVersionId }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateBinding(int bindingId, int definitionId, bool confirmed)
    {
        var r = await _admin.DeactivateBindingAsync(Actor(), bindingId, confirmed);
        return Done(r, L("Binding غیرفعال شد.", "The binding was deactivated."), () => RedirectToAction(nameof(Details), new { id = definitionId }), () => RedirectToAction(nameof(Details), new { id = definitionId }));
    }
}
