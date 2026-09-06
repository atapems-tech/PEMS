using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق CMMS Production Baseline v1 بند ۱۰.۳: دستورالعمل اجرای قابل استفاده‌ی مجدد - یک بار
/// ساخته می‌شود (Task ها + Required Skill/Material)، توسط چند MaintenancePlan استفاده می‌شود.
/// </summary>
public class JobPlanController : Controller
{
    private readonly IJobPlanService _service;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public JobPlanController(IJobPlanService service, PEMSDbContext context, ICurrentUserService currentUser)
    {
        _service = service;
        _context = context;
        _currentUser = currentUser;
    }

    // طبق ممیزی مستقل فاز ۶B Wave B1 (P1-01): قبلاً کل جدول فعال با ToListAsync (بدون Skip/Take)
    // بارگذاری می‌شد.
    public async Task<IActionResult> Index(int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Job Plans";
        ViewData["Section"] = "CMMS Integration";

        var items = await _service.GetListAsync(page, DataGridPageSize.Normalize(pageSize));
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string title, string? scope, decimal? estimatedDurationHours, string? safetyRequirements)
    {
        var (success, error, id) = await _service.CreateAsync(title, scope, estimatedDurationHours, safetyRequirements, _currentUser.UserName);

        if (!success)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index));
        }

        TempData["Message"] = "Job Plan created";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "Job Plan Details";
        ViewData["Section"] = "CMMS Integration";

        var jobPlan = await _service.GetDetailsAsync(id);
        if (jobPlan == null) return NotFound();

        ViewBag.AllSkills = await _context.Skills.Where(x => x.IsActive)
            .OrderBy(x => x.Name).Select(x => new { x.SkillId, x.Name }).ToListAsync();

        ViewBag.MaterialItems = await _context.MaterialItems.Where(x => x.IsActive && x.IsStockItem)
            .OrderBy(x => x.MaterialCode).Select(x => new { x.MaterialItemId, x.MaterialCode, x.Description }).ToListAsync();

        return View(jobPlan);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int jobPlanId, string title, string? scope, decimal? estimatedDurationHours, string? safetyRequirements)
    {
        var (success, error) = await _service.UpdateAsync(jobPlanId, title, scope, estimatedDurationHours, safetyRequirements, _currentUser.UserName);
        TempData[success ? "Message" : "Error"] = success ? "Job Plan updated" : error;
        return RedirectToAction(nameof(Details), new { id = jobPlanId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTask(int jobPlanId, int taskNumber, string title, string? description, int? estimatedDurationMinutes)
    {
        var (success, error) = await _service.AddTaskAsync(jobPlanId, taskNumber, title, description, estimatedDurationMinutes);
        TempData[success ? "Message" : "Error"] = success ? "Task added" : error;
        return RedirectToAction(nameof(Details), new { id = jobPlanId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTask(int maintenanceTaskId, int jobPlanId)
    {
        await _service.RemoveTaskAsync(maintenanceTaskId);
        return RedirectToAction(nameof(Details), new { id = jobPlanId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRequiredSkill(int jobPlanId, int skillId, string? minimumProficiencyLevel, decimal? plannedHours)
    {
        var (success, error) = await _service.AddRequiredSkillAsync(jobPlanId, skillId, minimumProficiencyLevel, plannedHours);
        TempData[success ? "Message" : "Error"] = success ? "Skill added" : error;
        return RedirectToAction(nameof(Details), new { id = jobPlanId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRequiredSkill(int jobPlanRequiredSkillId, int jobPlanId)
    {
        await _service.RemoveRequiredSkillAsync(jobPlanRequiredSkillId);
        return RedirectToAction(nameof(Details), new { id = jobPlanId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRequiredMaterial(int jobPlanId, int materialItemId, decimal requiredQuantity)
    {
        var (success, error) = await _service.AddRequiredMaterialAsync(jobPlanId, materialItemId, requiredQuantity);
        TempData[success ? "Message" : "Error"] = success ? "Material added" : error;
        return RedirectToAction(nameof(Details), new { id = jobPlanId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRequiredMaterial(int jobPlanRequiredMaterialId, int jobPlanId)
    {
        await _service.RemoveRequiredMaterialAsync(jobPlanRequiredMaterialId);
        return RedirectToAction(nameof(Details), new { id = jobPlanId });
    }
}
