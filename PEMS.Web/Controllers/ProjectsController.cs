using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Web.Resources;

namespace PEMS.Web.Controllers;

public class ProjectsController : Controller
{
    private readonly IProjectService _service;
    private readonly IStringLocalizer<SharedResource>? _localizer;

    public ProjectsController(IProjectService service, IStringLocalizer<SharedResource>? localizer = null)
    {
        _service = service;
        _localizer = localizer;
    }

    // SCRUM-558: a code another project already has goes to the form's validation box, like the required-field messages
    private async Task CheckUniqueCodeAsync(Project project, int? exceptProjectId)
    {
        if (ModelState.IsValid && await _service.CodeExistsAsync(project.ProjectCode, exceptProjectId))
            ModelState.AddModelError(nameof(Project.ProjectCode), _localizer?["Prj_Err_CodeExists", project.ProjectCode.Trim()].Value ?? "Prj_Err_CodeExists");
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Projects";
        ViewData["Section"] = "Engineering Management";

        var projects = await _service.GetAllAsync();
        return View(projects);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "New Project";
        ViewData["Section"] = "Engineering Management";
        return View(new Project { Status = "Planning" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Project project)
    {
        await CheckUniqueCodeAsync(project, null);
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "New Project";
            ViewData["Section"] = "Engineering Management";
            return View(project);
        }

        await _service.AddAsync(project);
        TempData["Message"] = "Project created successfully";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "Edit Project";
        ViewData["Section"] = "Engineering Management";

        var project = await _service.GetEntityByIdAsync(id);
        if (project == null) return NotFound();
        return View(project);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Project project)
    {
        await CheckUniqueCodeAsync(project, project.ProjectId);
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edit Project";
            ViewData["Section"] = "Engineering Management";
            return View(project);
        }

        await _service.UpdateAsync(project);
        TempData["Message"] = "Project updated successfully";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        await _service.ToggleActiveAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
