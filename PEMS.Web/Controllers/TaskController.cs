using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// وظایف مستقل (طبق نمونه‌ی کارتابل مرجع Pargar/Fonix: "اقدام وظایف" / "پیگیری وظایف") -
/// جدا از گردش‌کار ارجاع نامه، می‌تواند کاملاً مستقل باشد یا به هر موجودیتی وصل شود.
/// Task Manager (فاز ۰): مسیرها و کلیدهای مجوز قبلی حفظ شده‌اند؛ تغییر وضعیت حالا سطح-شیء (فقط درگیرها/Lead)،
/// فقط گذارهای مجاز و با کنترل RowVersion از ITaskManagerService انجام می‌شود. Controller به DbContext دسترسی ندارد.
/// </summary>
public class TaskController : Controller
{
    private readonly ITaskService _service;
    private readonly ITaskManagerService _manager;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly IStringLocalizer<SharedResource>? _l;

    public TaskController(ITaskService service, ITaskManagerService manager, ICurrentUserService currentUser,
        IPermissionService permissionService, IStringLocalizer<SharedResource>? localizer = null)
    {
        _service = service;
        _manager = manager;
        _currentUser = currentUser;
        _permissionService = permissionService;
        _l = localizer;
    }

    private Task<TaskActor> ActorAsync() => _manager.ResolveActorAsync(_currentUser.UserId, _currentUser.Roles, _currentUser.FullName);
}
