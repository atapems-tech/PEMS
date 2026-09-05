using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// وظایف مستقل (طبق نمونه‌ی کارتابل مرجع Pargar/Fonix: "اقدام وظایف" / "پیگیری وظایف") -
/// جدا از گردش‌کار ارجاع نامه، می‌تواند کاملاً مستقل باشد یا به هر موجودیتی وصل شود.
/// </summary>
public class TaskController : Controller
{
    private readonly ITaskService _service;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public TaskController(ITaskService service, PEMSDbContext context, ICurrentUserService currentUser,
        IPermissionService permissionService)
    {
        _service = service;
        _context = context;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): قبلاً کل جدول فیلترشده با
    // ToListAsync (بدون Skip/Take) بارگذاری می‌شد.
    // طبق ممیزی مستقل فاز ۶B Wave A (P0): این Action هیچ بررسی مجوزی نداشت - هر کاربر
    // احرازهویت‌شده‌ای (بدون توجه به نقش) می‌توانست وظایف را ببیند. الگوی
    // WorkOrder/WorkRequest/MaterialRequisition/Inspection/Permit عیناً استفاده شده.
    public async Task<IActionResult> Index(string tab = "action", int page = 1, int? pageSize = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Task", "View"))
            return Forbid();

        ViewData["Title"] = "Tasks";
        ViewData["Section"] = "Correspondence Management";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);

        if (_currentUser.UserId == null)
            return View(new PagedResult<TaskItemDto> { Page = page, PageSize = normalizedPageSize });

        var tasks = tab == "followup"
            ? await _service.GetAssignedByMeAsync(_currentUser.UserId.Value, page: page, pageSize: normalizedPageSize)
            : await _service.GetMyActionItemsAsync(_currentUser.UserId.Value, page: page, pageSize: normalizedPageSize);

        ViewBag.Tab = tab;

        ViewBag.Employees = await _context.Employees.Where(x => x.IsActive)
            .Select(x => new { x.EmployeeId, Name = x.FirstName + " " + x.LastName }).ToListAsync();

        return View(tasks);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string title, string? description, int? assignedToEmployeeId,
        DateTime? dueDate, string priority)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Task", "Create"))
            return Forbid();

        await _service.CreateAsync(title, description, assignedToEmployeeId, dueDate, priority,
            null, null, _currentUser.UserId, _currentUser.FullName);

        TempData["Message"] = "Task created";
        return RedirectToAction(nameof(Index));
    }

    // طبق ممیزی مستقل فاز ۶B Wave A (P0): تغییر وضعیت یک رکورد موجود است - طبق الگوی پروژه
    // (مثلاً MaterialRequisitionController.ChangeStatus) نیازمند مجوز "Edit" است، نه یک مجوز
    // اختصاصیِ جدید.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int taskItemId, string status, string? completionNote, string tab = "action")
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Task", "Edit"))
            return Forbid();

        await _service.UpdateStatusAsync(taskItemId, status, completionNote);
        return RedirectToAction(nameof(Index), new { tab });
    }
}
