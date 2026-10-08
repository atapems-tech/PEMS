using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Domain.Entities;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// Discipline Workspace Architecture طبق سند راهنما (بخش ۵): هر دیسیپلین یک Workspace
/// اختصاصی دارد (Equipment, Documents, VDR, MR/MRQ, Tasks, KPI) ولی دیتابیس مشترک است —
/// این کنترلر همان دیتابیس مرکزی را فقط فیلترشده به‌ازای هر دیسیپلین نمایش می‌دهد.
/// </summary>
/// <remarks>
/// طبق «PEMS — P1 Organization &amp; Master Data Authorization Hardening»: ماژول مجوز
/// "DisciplineWorkspace" از قبل در PermissionService Seed شده بود (EngineeringSupervisor/
/// DisciplineEngineer) و در PermissionMatrixController هم لیست شده بود، اما هیچ‌جای این
/// Controller واقعاً HasPermissionAsync را صدا نمی‌زد - یعنی AddTask/SetTaskStatus برای هر
/// کاربر احرازهویت‌شده‌ای باز بود، با اینکه دقیقاً همین ماژول برایش از قبل طراحی شده بود. حالا
/// این سیم‌کشی تکمیل شد. چون سایدبار «Disciplines» از قبل به EngineeringManager/
/// DocumentController/Viewer هم نشان داده می‌شود (MenuAccess.EngineeringManagement)، این سه
/// نقش هم به همون ماژول اضافه شدند تا فعال‌کردن این بررسی لینک مرده نسازد (بند ۲۱-C).
/// </remarks>
public class DisciplineController : Controller
{
    private readonly IDisciplineWorkspaceService _service;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public DisciplineController(IDisciplineWorkspaceService service, PEMSDbContext context,
        ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _service = service;
        _context = context;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "DisciplineWorkspace", "View"))
            return Forbid();

        ViewData["Title"] = "Discipline Workspaces";
        ViewData["Section"] = "Administration";

        var summary = await _service.GetSummaryListAsync();
        return View(summary);
    }

    public async Task<IActionResult> Details(int id, int? projectId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "DisciplineWorkspace", "View"))
            return Forbid();

        ViewData["Title"] = "Discipline Workspace";
        ViewData["Section"] = "Administration";

        var workspace = await _service.GetWorkspaceAsync(id, projectId);
        if (workspace == null) return NotFound();

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Employees = await _context.Employees.Where(x => x.IsActive)
            .Select(x => new { x.EmployeeId, Name = x.FirstName + " " + x.LastName }).ToListAsync();

        ViewBag.SelectedProjectId = projectId;

        return View(workspace);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string code, string name, string? description, string? importAliases, string? transmittalCode)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "DisciplineWorkspace", "Create")) return Forbid();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Discipline code and name are required.";
            return RedirectToAction(nameof(Index));
        }

        var trimmedCode = code.Trim();
        var trimmedName = name.Trim();
        var duplicate = await _context.Disciplines.AnyAsync(x =>
            x.Code.ToLower() == trimmedCode.ToLower() || x.Name.ToLower() == trimmedName.ToLower());
        if (duplicate)
        {
            TempData["Error"] = "A discipline with the same code or name already exists.";
            return RedirectToAction(nameof(Index));
        }

        await _context.Disciplines.AddAsync(new Discipline
        {
            Code = trimmedCode,
            Name = trimmedName,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            ImportAliases = string.IsNullOrWhiteSpace(importAliases) ? null : importAliases.Trim(),
            TransmittalCode = string.IsNullOrWhiteSpace(transmittalCode) ? null : transmittalCode.Trim(),
            IsActive = true,
            CreatedDate = DateTime.Now
        });
        await _context.SaveChangesAsync();
        TempData["Message"] = "Discipline created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int disciplineId, string code, string name, string? description, string? importAliases, string? transmittalCode)
    {

        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "DisciplineWorkspace", "Edit")) return Forbid();        var discipline = await _context.Disciplines.FirstOrDefaultAsync(x => x.DisciplineId == disciplineId);
        if (discipline == null) return NotFound();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Discipline code and name are required.";
            return RedirectToAction(nameof(Index));
        }

        var trimmedCode = code.Trim();
        var trimmedName = name.Trim();
        var duplicate = await _context.Disciplines.AnyAsync(x => x.DisciplineId != disciplineId &&
            (x.Code.ToLower() == trimmedCode.ToLower() || x.Name.ToLower() == trimmedName.ToLower()));
        if (duplicate)
        {
            TempData["Error"] = "A discipline with the same code or name already exists.";
            return RedirectToAction(nameof(Index));
        }

        discipline.Code = trimmedCode;
        discipline.Name = trimmedName;
        discipline.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        discipline.ImportAliases = string.IsNullOrWhiteSpace(importAliases) ? null : importAliases.Trim();
        discipline.TransmittalCode = string.IsNullOrWhiteSpace(transmittalCode) ? null : transmittalCode.Trim();
        await _context.SaveChangesAsync();
        TempData["Message"] = "Discipline updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int disciplineId)
    {

        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "DisciplineWorkspace", "Edit")) return Forbid();        var discipline = await _context.Disciplines.FirstOrDefaultAsync(x => x.DisciplineId == disciplineId);
        if (discipline == null) return NotFound();
        discipline.IsActive = !discipline.IsActive;
        await _context.SaveChangesAsync();
        TempData["Message"] = discipline.IsActive ? "Discipline reactivated." : "Discipline deactivated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTask(int disciplineId, int? projectId, string title, string? description,
        string type, int? responsibleEmployeeId, DateTime? dueDate)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "DisciplineWorkspace", "Create"))
            return Forbid();

        await _service.AddTaskAsync(disciplineId, projectId, title, description, type, responsibleEmployeeId, dueDate);
        TempData["Message"] = "Task added";
        return RedirectToAction(nameof(Details), new { id = disciplineId });
    }

    // طبق ممیزی: انتقال وضعیت یک Task موجود (مثلاً ToDo->InProgress->Done) - بروزرسانی است، نه
    // یک تصمیم تایید نهایی؛ "Edit" کافی است (نه Approve).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTaskStatus(int disciplineId, int taskId, string status)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "DisciplineWorkspace", "Edit"))
            return Forbid();

        await _service.SetTaskStatusAsync(taskId, status);
        return RedirectToAction(nameof(Details), new { id = disciplineId });
    }
}
