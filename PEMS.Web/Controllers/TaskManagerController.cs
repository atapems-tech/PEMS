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

    private IActionResult Back(string? returnUrl, string fallbackAction = nameof(Index), object? routeValues = null) =>
        !string.IsNullOrEmpty(returnUrl) && Url != null && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(fallbackAction, routeValues);

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

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await Can("View")) return Forbid();
        var actor = await ActorAsync();
        ViewData["Title"] = Text("Tm_Title");
        ViewData["Section"] = "Task Manager";
        ViewBag.IsAdmin = actor.IsSystemAdmin;
        ViewBag.HasEmployee = actor.EmployeeId != null;
        return View(await _tasks.GetAccessibleSpacesAsync(actor));
    }

    [HttpGet]
    public async Task<IActionResult> Space(string key, string view = "backlog")
    {
        if (!await Can("View")) return Forbid();
        var actor = await ActorAsync();
        view = view == "board" ? "board" : "backlog";
        ViewBag.ViewMode = view;

        if (view == "board")
        {
            var board = await _tasks.GetBoardAsync(key, actor);
            if (board == null) return NotFound();
            ViewData["Title"] = board.View.Space.Name;
            ViewData["Section"] = "Task Manager";
            ViewBag.Assignees = await _tasks.GetEligibleAssigneesAsync(board.View.Space.Id, actor);
            ViewBag.SpaceView = board.View;
            return View("Board", board);
        }

        var backlog = await _tasks.GetBacklogAsync(key, actor);
        if (backlog == null) return NotFound();
        ViewData["Title"] = backlog.View.Space.Name;
        ViewData["Section"] = "Task Manager";
        ViewBag.Assignees = await _tasks.GetEligibleAssigneesAsync(backlog.View.Space.Id, actor);
        ViewBag.SpaceView = backlog.View;
        return View("Backlog", backlog);
    }

    /// <summary>همه‌ی کارهای قابل‌دید کاربر با فیلتر بسته (همچنین «کارهای من» با assignee=me).</summary>
    [HttpGet]
    public async Task<IActionResult> AllWork([FromQuery] WorkItemQuery query)
    {
        if (!await Can("View")) return Forbid();
        var actor = await ActorAsync();
        ViewData["Title"] = Text(query.Assignee == "me" ? "Tm_MyTasks" : "Tm_AllWork");
        ViewData["Section"] = "Task Manager";
        ViewBag.Query = query;
        var spaces = await _tasks.GetAccessibleSpacesAsync(actor);
        ViewBag.Spaces = spaces;
        ViewBag.CanEdit = await Can("Edit");
        // با فیلتر یک فضا، نوار گروهی همان مقادیر فضا (وضعیت/مسئول/Sprint) را پیشنهاد می‌دهد؛ بدون آن فقط بایگانی
        var space = spaces.FirstOrDefault(s => s.Id == query.WorkSpaceId);
        if (space != null)
        {
            var view = await _tasks.GetSpaceViewAsync(space.Key, actor);
            ViewBag.SpaceView = view;
            ViewBag.Types = view?.Types;
            ViewBag.Assignees = await _tasks.GetEligibleAssigneesAsync(space.Id, actor);
        }
        return View(await _tasks.QueryAsync(query, actor));
    }

    // ==================================================================================== item writes

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WorkItemCreateInput input, bool createAnother = false, string? returnUrl = null)
    {
        if (!await Can("Create")) return Forbid();
        var r = await _tasks.CreateAsync(input, await ActorAsync());
        if (WantsJson) return Done(r, returnUrl, "Tm_Msg_Created");
        if (!r.Success) { TempData["Error"] = Text(r.ErrorKey!); return Back(returnUrl); }
        TempData["Message"] = Text("Tm_Msg_Created");
        if (createAnother) return Back(AppendQuery(returnUrl, "create=1"));
        return RedirectToAction("Details", "Task", new { id = r.Id });
    }

    private static string? AppendQuery(string? url, string q) =>
        string.IsNullOrEmpty(url) ? null : url + (url.Contains('?') ? "&" : "?") + q;

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(WorkItemUpdateInput input, string? returnUrl = null)
    {
        if (!await Can("Edit")) return Forbid();
        return Done(await _tasks.UpdateAsync(input, await ActorAsync()), returnUrl);
    }

    // ==================================================================================== sprints (Lead)

    // ==================================================================================== admin (SystemAdmin)

    [HttpGet]
    public async Task<IActionResult> Admin()
    {
        var actor = await ActorAsync();
        if (!actor.IsSystemAdmin) return Forbid();
        ViewData["Title"] = Text("Tm_Admin_Title");
        ViewData["Section"] = "Administration";
        ViewBag.Departments = await _spaces.GetDepartmentsAsync();
        return View(await _spaces.GetAllForAdminAsync(actor));
    }

    [HttpGet]
    public async Task<IActionResult> AdminSpace(int id)
    {
        var actor = await ActorAsync();
        if (!actor.IsSystemAdmin) return Forbid();
        var model = await _spaces.GetAdminAsync(id, actor);
        if (model == null) return NotFound();
        ViewData["Title"] = model.Space.Name;
        ViewData["Section"] = "Administration";
        ViewBag.Departments = await _spaces.GetDepartmentsAsync();
        ViewBag.Employees = await _spaces.GetActiveEmployeesAsync();
        return View(model);
    }

    private async Task<IActionResult> AdminDone(Func<TaskActor, Task<TmResult>> op, int? spaceId)
    {
        var actor = await ActorAsync();
        if (!actor.IsSystemAdmin) return Forbid();
        var r = await op(actor);
        if (r.Success) TempData["Message"] = Text("Tm_Msg_Saved");
        else if (r.ErrorKey == "Tm_Err_NotFound") return NotFound();
        else TempData["Error"] = Text(r.ErrorKey!);
        var target = r.Success && spaceId == null ? r.Id : spaceId;
        return target is int sid ? RedirectToAction(nameof(AdminSpace), new { id = sid }) : RedirectToAction(nameof(Admin));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveSpace(WorkSpaceInput input) => AdminDone(a => _spaces.SaveSpaceAsync(input, a), input.Id);
}
