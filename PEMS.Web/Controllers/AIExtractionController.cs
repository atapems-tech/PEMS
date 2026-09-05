using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Controllers;

/// <summary>
/// مرکز بازبینی داده‌ی ساختاریافته‌ای که هوش مصنوعی از مدارک استخراج کرده (تگ تجهیز، مشخصات فنی)،
/// پیش از تایید نهایی و ورود به جداول اصلی (EquipmentTag و ...).
/// </summary>
public class AIExtractionController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly IDocumentAIIngestionService _aiIngestionService;

    public AIExtractionController(PEMSDbContext context, IDocumentAIIngestionService aiIngestionService)
    {
        _context = context;
        _aiIngestionService = aiIngestionService;
    }

    public async Task<IActionResult> Index()
    {
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveExtractedTag(int extractedTagId, int equipmentId, int attachmentId)
    {
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectExtractedTag(int extractedTagId, int attachmentId)
    {
        await _aiIngestionService.RejectExtractedTagAsync(extractedTagId);
        return RedirectToAction(nameof(Review), new { attachmentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveExtractedSpecification(int extractedSpecificationId, int attachmentId)
    {
        await _aiIngestionService.ApproveExtractedSpecificationAsync(extractedSpecificationId);
        return RedirectToAction(nameof(Review), new { attachmentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectExtractedSpecification(int extractedSpecificationId, int attachmentId)
    {
        await _aiIngestionService.RejectExtractedSpecificationAsync(extractedSpecificationId);
        return RedirectToAction(nameof(Review), new { attachmentId });
    }
}
