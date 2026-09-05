using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Controllers;

/// <summary>
/// Discipline Workspace Architecture طبق سند راهنما (بخش ۵): هر دیسیپلین یک Workspace
/// اختصاصی دارد (Equipment, Documents, VDR, MR/MRQ, Tasks, KPI) ولی دیتابیس مشترک است —
/// این کنترلر همان دیتابیس مرکزی را فقط فیلترشده به‌ازای هر دیسیپلین نمایش می‌دهد.
/// </summary>
public class DisciplineController : Controller
{
    private readonly IDisciplineWorkspaceService _service;
    private readonly PEMSDbContext _context;

    public DisciplineController(IDisciplineWorkspaceService service, PEMSDbContext context)
    {
        _service = service;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Discipline Workspaces";
        ViewData["Section"] = "Administration";

        var summary = await _service.GetSummaryListAsync();
        return View(summary);
    }

    public async Task<IActionResult> Details(int id, int? projectId)
    {
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
    public async Task<IActionResult> AddTask(int disciplineId, int? projectId, string title, string? description,
        string type, int? responsibleEmployeeId, DateTime? dueDate)
    {
        await _service.AddTaskAsync(disciplineId, projectId, title, description, type, responsibleEmployeeId, dueDate);
        TempData["Message"] = "Task added";
        return RedirectToAction(nameof(Details), new { id = disciplineId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTaskStatus(int disciplineId, int taskId, string status)
    {
        await _service.SetTaskStatusAsync(taskId, status);
        return RedirectToAction(nameof(Details), new { id = disciplineId });
    }
}
