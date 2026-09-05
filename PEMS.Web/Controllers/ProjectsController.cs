using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;

namespace PEMS.Web.Controllers;

public class ProjectsController : Controller
{
    private readonly IProjectService _service;

    public ProjectsController(IProjectService service)
    {
        _service = service;
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
