using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// ==============================================================================
/// طبق «PEMS Bilingual Help & Knowledge System» بخش ۸ (Administrative Management) و تصمیم
/// مجوزدهی مستندشده: نقش اختصاصی «Help Manager» ایجاد نشد - اضافه‌کردن یک SystemRole جدید
/// نیازمند تغییر در seed نقش‌ها و ریسک اثر جانبی روی آرایه‌های نقش موجود در جاهای دیگر پروژه
/// است، بدون شواهدی از نیاز واقعی به این تفکیک امروز. به‌جایش، دقیقاً همان الگوی دو-نقشیِ
/// Areas/Admin/EmployeeController و DepartmentController تکرار شده
/// (SystemAdmin + EngineeringManager) - محتوای راهنما مفهوماً «حکمرانی محتوا»یی مشابه است.
/// اگر بعداً تفکیک دقیق‌تر لازم شد، ماژول «Help» را می‌توان بدون تغییر معماری به سیستم پویای
/// IPermissionService/RolePermission (که در EngineeringDocuments از قبل استفاده می‌شود) اضافه
/// کرد.
/// ==============================================================================
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin + "," + SystemRoles.EngineeringManager)]
public class HelpController : Controller
{
    private readonly IHelpService _helpService;
    private readonly IHelpVisualService _helpVisualService;
    private readonly IHelpWorkflowDiagramService _helpWorkflowDiagramService;
    private readonly PEMSDbContext _context;

    public HelpController(
        IHelpService helpService, IHelpVisualService helpVisualService,
        IHelpWorkflowDiagramService helpWorkflowDiagramService, PEMSDbContext context)
    {
        _helpService = helpService;
        _helpVisualService = helpVisualService;
        _helpWorkflowDiagramService = helpWorkflowDiagramService;
        _context = context;
    }

    public async Task<IActionResult> Index(int? categoryId, string? articleType, bool? isPublished)
    {
        ViewData["Title"] = "Help Administration";
        ViewData["Section"] = "Administration";

        ViewBag.Articles = await _helpService.GetAllForAdminAsync("en", categoryId, articleType, isPublished);
        ViewBag.Categories = await _context.HelpCategories.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToListAsync();
        ViewBag.SelectedCategoryId = categoryId;
        ViewBag.SelectedArticleType = articleType;
        ViewBag.SelectedIsPublished = isPublished;

        return View();
    }

    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "New Help Article";
        ViewData["Section"] = "Administration";
        await LoadFormDropdownsAsync();
        return View(new HelpArticleAdminEditDto { IsPublished = false, ArticleType = "Module" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(HelpArticleAdminEditDto dto)
    {
        try
        {
            var id = await _helpService.CreateArticleAsync(dto, GetCurrentEmployeeId());
            TempData["Message"] = "Help article created.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            await LoadFormDropdownsAsync();
            return View(dto);
        }
    }

    public async Task<IActionResult> Edit(int id)
    {
        var dto = await _helpService.GetForEditAsync(id);
        if (dto == null) return NotFound();

        ViewData["Title"] = "Edit Help Article";
        ViewData["Section"] = "Administration";
        await LoadFormDropdownsAsync(id);
        ViewBag.Visuals = await _helpVisualService.GetAllForArticleAdminAsync(id);
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, HelpArticleAdminEditDto dto)
    {
        try
        {
            await _helpService.UpdateArticleAsync(id, dto);
            TempData["Message"] = "Help article updated.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            await LoadFormDropdownsAsync(id);
            ViewBag.Visuals = await _helpVisualService.GetAllForArticleAdminAsync(id);
            dto.HelpArticleId = id;
            return View(dto);
        }
    }

    // ============ Visuals (طبق «PEMS Help System — Phase 3» بخش ۱۳) ============

    public async Task<IActionResult> VisualCreate(int articleId)
    {
        ViewData["Title"] = "New Help Visual";
        ViewData["Section"] = "Administration";
        ViewBag.ArticleId = articleId;
        ViewBag.WorkflowSources = await _helpWorkflowDiagramService.GetAvailableWorkflowSourcesAsync();
        return View(new HelpVisualAdminEditDto { HelpArticleId = articleId, VisualType = "WorkflowDiagram", IsPublished = false });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VisualCreate(HelpVisualAdminEditDto dto)
    {
        try
        {
            await _helpVisualService.CreateAsync(dto);
            TempData["Message"] = "Visual content created.";
            return RedirectToAction(nameof(Edit), new { id = dto.HelpArticleId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            ViewBag.ArticleId = dto.HelpArticleId;
            ViewBag.WorkflowSources = await _helpWorkflowDiagramService.GetAvailableWorkflowSourcesAsync();
            return View(dto);
        }
    }

    public async Task<IActionResult> VisualEdit(int id)
    {
        var dto = await _helpVisualService.GetForEditAsync(id);
        if (dto == null) return NotFound();

        ViewData["Title"] = "Edit Help Visual";
        ViewData["Section"] = "Administration";
        ViewBag.ArticleId = dto.HelpArticleId;
        ViewBag.WorkflowSources = await _helpWorkflowDiagramService.GetAvailableWorkflowSourcesAsync();
        return View("VisualCreate", dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VisualEdit(int id, HelpVisualAdminEditDto dto)
    {
        try
        {
            await _helpVisualService.UpdateAsync(id, dto);
            TempData["Message"] = "Visual content updated.";
            return RedirectToAction(nameof(Edit), new { id = dto.HelpArticleId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            ViewBag.ArticleId = dto.HelpArticleId;
            ViewBag.WorkflowSources = await _helpWorkflowDiagramService.GetAvailableWorkflowSourcesAsync();
            return View("VisualCreate", dto);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VisualSetPublished(int id, int articleId, bool isPublished)
    {
        await _helpVisualService.SetPublishedAsync(id, isPublished);
        TempData["Message"] = isPublished ? "Visual published." : "Visual unpublished.";
        return RedirectToAction(nameof(Edit), new { id = articleId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VisualDelete(int id, int articleId)
    {
        await _helpVisualService.DeleteAsync(id);
        TempData["Message"] = "Visual deleted.";
        return RedirectToAction(nameof(Edit), new { id = articleId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPublished(int id, bool isPublished)
    {
        await _helpService.SetPublishedAsync(id, isPublished);
        TempData["Message"] = isPublished ? "Article published." : "Article unpublished (moved back to draft).";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _helpService.DeleteArticleAsync(id);
        TempData["Message"] = "Article deleted.";
        return RedirectToAction(nameof(Index));
    }

    // طبق بخش ۸ سند («View revision history»): به‌جای HelpRevision موازی، از AuditLog موجود
    // پروژه استفاده می‌شود - همان مکانیزمی که برای هر Entity دیگر پروژه رایگان کار می‌کند
    public async Task<IActionResult> History(int id)
    {
        ViewData["Title"] = "Help Article Change History";
        ViewData["Section"] = "Administration";

        var entries = await _context.AuditLogs
            .Where(a => (a.EntityName == "HelpArticle" || a.EntityName == "HelpArticleTranslation") && a.EntityKeyValue == id.ToString())
            .OrderByDescending(a => a.Timestamp)
            .Take(100)
            .ToListAsync();

        ViewBag.HelpArticleId = id;
        return View(entries);
    }

    private async Task LoadFormDropdownsAsync(int? excludeArticleId = null)
    {
        ViewBag.Categories = await _context.HelpCategories.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToListAsync();

        var others = await _context.HelpArticles
            .Include(a => a.Translations)
            .Where(a => !excludeArticleId.HasValue || a.HelpArticleId != excludeArticleId.Value)
            .OrderBy(a => a.Code)
            .ToListAsync();

        ViewBag.AllArticlesForRelated = others.Select(a => new
        {
            a.HelpArticleId,
            a.Code,
            Title = a.Translations.FirstOrDefault(t => t.LanguageCode == "en")?.Title ?? a.Code
        }).ToList();
    }

    private int? GetCurrentEmployeeId()
    {
        var claim = User.FindFirst("EmployeeId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
