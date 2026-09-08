using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// مرکز بازبینی داده‌ی ساختاریافته‌ای که هوش مصنوعی از مدارک استخراج کرده (تگ تجهیز، مشخصات فنی)،
/// پیش از تایید نهایی و ورود به جداول اصلی (EquipmentTag و ...).
/// </summary>
/// <remarks>
/// طبق «PEMS — P0 AI Pipeline Endpoint Hardening»: این Controller قبلاً هیچ بررسی مجوزی نداشت -
/// ApproveExtractedTag/ApproveExtractedSpecification (که پیشنهاد AI را به داده‌ی معتبر Equipment
/// تبدیل می‌کنند) برای هر کاربر احرازهویت‌شده‌ای باز بودند. ماژول جدید "AIExtraction" چون هیچ
/// ماژول موجودی این قابلیت (بازبینی/تایید داده‌ی استخراج‌شده‌ی AI) را پوشش نمی‌دهد.
/// </remarks>
public class AIExtractionController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly IDocumentAIIngestionService _aiIngestionService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public AIExtractionController(PEMSDbContext context, IDocumentAIIngestionService aiIngestionService,
        ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _context = context;
        _aiIngestionService = aiIngestionService;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "AIExtraction", "View"))
            return Forbid();

        ViewData["Title"] = "AI Extraction";
        ViewData["Section"] = "AI Engineering Intelligence";

        var attachments = await _context.DocumentAttachments
            .Include(x => x.Document)
            .Where(x => x.AIStatus == "Completed")
            .Select(x => new
            {
                Attachment = x,
                PendingTags = x.ExtractedTags.Count(t => t.ReviewStatus == "Draft"),
                PendingSpecs = x.ExtractedSpecifications.Count(s => s.ReviewStatus == "Draft")
            })
            .OrderByDescending(x => x.Attachment.AIProcessedDate)
            .ToListAsync();

        return View(attachments.Select(x => (x.Attachment, x.PendingTags, x.PendingSpecs)).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Review(int attachmentId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "AIExtraction", "View"))
            return Forbid();

        ViewData["Title"] = "Review AI Extracted Data";
        ViewData["Section"] = "AI Engineering Intelligence";

        var attachment = await _context.DocumentAttachments
            .Include(x => x.Document)
            .Include(x => x.ExtractedTags)
            .Include(x => x.ExtractedSpecifications)
            .FirstOrDefaultAsync(x => x.AttachmentId == attachmentId);

        if (attachment == null) return NotFound();

        ViewBag.Equipments = new SelectList(_context.Equipments, "EquipmentId", "TagNumber");

        return View(attachment);
    }

    // طبق ممیزی: پرریسک‌ترین اکشن این Controller - پیشنهاد AI را به Equipment واقعی وصل می‌کند
    // (داده‌ی معتبر/authoritative می‌سازد). نیازمند "Approve".
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveExtractedTag(int extractedTagId, int equipmentId, int attachmentId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "AIExtraction", "Approve"))
            return Forbid();

        try
        {
            await _aiIngestionService.ApproveExtractedTagAsync(extractedTagId, equipmentId);
            TempData["Message"] = "Tag approved and linked to equipment";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Review), new { attachmentId });
    }

    // رد کردن، برخلاف Approve، هیچ داده‌ی معتبری نمی‌سازد (فقط وضعیت پیشنهاد AI را Draft/Rejected
    // می‌کند) - سطح ریسک پایین‌تر، "Create" کافی است.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectExtractedTag(int extractedTagId, int attachmentId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "AIExtraction", "Create"))
            return Forbid();

        await _aiIngestionService.RejectExtractedTagAsync(extractedTagId);
        return RedirectToAction(nameof(Review), new { attachmentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveExtractedSpecification(int extractedSpecificationId, int attachmentId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "AIExtraction", "Approve"))
            return Forbid();

        await _aiIngestionService.ApproveExtractedSpecificationAsync(extractedSpecificationId);
        return RedirectToAction(nameof(Review), new { attachmentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectExtractedSpecification(int extractedSpecificationId, int attachmentId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "AIExtraction", "Create"))
            return Forbid();

        await _aiIngestionService.RejectExtractedSpecificationAsync(extractedSpecificationId);
        return RedirectToAction(nameof(Review), new { attachmentId });
    }
}
