using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// Task Manager (فضاها، Backlog، Board، Sprint، همه‌ی کارها، پیکربندی فضا). Controller نازک: هر تصمیم دسترسی/اعتبارسنجی
/// در ITaskManagerService/IWorkSpaceService (TaskAccess). دروازه‌ی نقش همان مجوزهای موجود Task.View/Create/Edit؛
/// پیکربندی فضا فقط SystemAdmin. شناسه‌ای که کاربر نمی‌بیند = 404.
/// </summary>
public class TaskManagerController : Controller
{
    private readonly ITaskManagerService _tasks;
    private readonly IWorkSpaceService _spaces;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissions;
    private readonly IStringLocalizer<SharedResource>? _l;

    public TaskManagerController(ITaskManagerService tasks, IWorkSpaceService spaces, ICurrentUserService currentUser,
        IPermissionService permissions, IStringLocalizer<SharedResource>? localizer = null)
    {
        _tasks = tasks;
        _spaces = spaces;
        _currentUser = currentUser;
        _permissions = permissions;
        _l = localizer;
    }

    private Task<TaskActor> ActorAsync() => _tasks.ResolveActorAsync(_currentUser.UserId, _currentUser.Roles, _currentUser.FullName);
    private Task<bool> Can(string action) => _permissions.HasPermissionAsync(_currentUser.Roles, "Task", action);
    private string Text(string key) => _l != null ? _l[key].Value : key;
    private bool WantsJson => string.Equals(Request?.Headers["X-Requested-With"].ToString(), "fetch", StringComparison.Ordinal);

    private IActionResult Back(string? returnUrl, string fallbackAction = "Index", object? routeValues = null) =>
        !string.IsNullOrEmpty(returnUrl) && Url != null && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : fallbackAction == "Index" ? Redirect("/p/task-manager") : RedirectToAction(fallbackAction, routeValues);

    /// <summary>نتیجه‌ی یکسان: JSON برای fetch (Board/DnD)، Flash + Redirect برای فرم. NotFound = 404 (بدون افشا).</summary>
    private IActionResult Done(TmResult r, string? returnUrl, string okKey = "Tm_Msg_Saved")
    {
        if (WantsJson)
        {
            if (!r.Success && r.ErrorKey == "Tm_Err_NotFound") return NotFound(new { ok = false, error = Text(r.ErrorKey) });
            return Json(new { ok = r.Success, error = r.Success ? null : Text(r.ErrorKey!), id = r.Id });
        }
        if (!r.Success && r.ErrorKey == "Tm_Err_NotFound") return NotFound();
        if (r.Success) TempData["Message"] = Text(okKey);
        else TempData["Error"] = Text(r.ErrorKey!);
        return Back(returnUrl);
    }

    // ==================================================================================== pages

    // SCRUM-831: the start page (the spaces of the user) is the definition /p/task-manager over ITaskManagerPagesOperations.

    // SCRUM-831: the board and the backlog of a space are the definition pages /p/task-board and /p/task-backlog (the old address keeps working).
    [HttpGet]
    public IActionResult Space(string key, string view = "backlog")
        => Redirect((view == "board" ? "/p/task-board?key=" : "/p/task-backlog?key=") + Uri.EscapeDataString(key ?? ""));

    // SCRUM-831: AllWork is the definition page /p/task-all-work over ITaskManagerPagesOperations.AllWorkAsync (also "my tasks").
}
