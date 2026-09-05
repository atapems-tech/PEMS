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

    public TaskController(ITaskService service, PEMSDbContext context, ICurrentUserService currentUser)
    {
        _service = service;
        _context = context;
        _currentUser = currentUser;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): قبلاً کل جدول فیلترشده با
    // ToListAsync (بدون Skip/Take) بارگذاری می‌شد.
    public async Task<IActionResult> Index(string tab = "action", int page = 1, int? pageSize = null)
    {
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
        await _service.CreateAsync(title, description, assignedToEmployeeId, dueDate, priority,
            null, null, _currentUser.UserId, _currentUser.FullName);

        TempData["Message"] = "Task created";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int taskItemId, string status, string? completionNote, string tab = "action")
    {
        await _service.UpdateStatusAsync(taskItemId, status, completionNote);
        return RedirectToAction(nameof(Index), new { tab });
    }
}
