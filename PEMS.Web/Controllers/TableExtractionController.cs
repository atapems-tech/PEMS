using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.AI.Interfaces;
using PEMS.Application.AI.Models;
using PEMS.Application.AI.Parser;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Web.Security;
using System.IO;

namespace PEMS.Web.Controllers;

/// <summary>
/// استخراج جدولی از مدارک (Wiring Diagram / Instrument Index / Marshalling List / I-O List / Loop
/// Drawing) - هم با AI (Ollama) و هم بدون آن (DeterministicTableExtractor، فقط PDF Native). نتیجه
/// می‌تواند به‌عنوان پیش‌نمایش دیده شود یا Stage شود برای بازبینی/اتصال به Equipment/Instrument
/// (Review) - طبق همان قاعده‌ی «فقط با تایید صریح انسان» که در RelationshipService هست.
/// </summary>
public class TableExtractionController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly IDocumentAIService _documentAIService;
    private readonly ITableExtractionImportService _importService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public TableExtractionController(
        PEMSDbContext context,
        IDocumentAIService documentAIService,
        ITableExtractionImportService importService,
        ICurrentUserService currentUser,
        IPermissionService permissionService)
    {
        _context = context;
        _documentAIService = documentAIService;
        _importService = importService;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - P2): قبلاً Take(50) ثابت بدون
    // Skip/CountAsync/pageSize بود - یعنی هیچ راهی برای دیدن Attachmentهای قدیمی‌تر از ۵۰تای
    // اخیر نبود. حالا صفحه‌بندی واقعی SQL با Skip/Take/CountAsync است.
    public async Task<IActionResult> Index(int page = 1, int? pageSize = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "TableExtraction", "View"))
            return Forbid();

        ViewData["Title"] = "Table Extraction";
        ViewData["Section"] = "AI Engineering Intelligence";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var normalizedPage = page < 1 ? 1 : page;

        var query = _context.DocumentAttachments.Where(x => x.IsActive).AsQueryable();

        var totalCount = await query.CountAsync();

        var attachments = await query
            .OrderByDescending(x => x.UploadDate)
            .ThenByDescending(x => x.AttachmentId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new TableExtractionAttachmentListDto
            {
                AttachmentId = x.AttachmentId,
                FileName = x.FileName,
                FileExtension = x.FileExtension,
                UploadDate = x.UploadDate,
                AIStatus = x.AIStatus
            })
            .ToListAsync();

        return View(new PagedResult<TableExtractionAttachmentListDto>
        {
            Items = attachments,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Analyze(int attachmentId, string? documentHint)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "TableExtraction", "View"))
            return Forbid();

        ViewData["Title"] = "Table Extraction Result";
        ViewData["Section"] = "AI Engineering Intelligence";

        var attachment = await _context.DocumentAttachments.FirstOrDefaultAsync(x => x.AttachmentId == attachmentId);
        if (attachment == null) return NotFound();

        var physicalPath = ResolvePhysicalPath(attachment);
        if (!System.IO.File.Exists(physicalPath))
        {
            ViewBag.Attachment = attachment;
            return View(new AIAnalysisResult { Success = false, ErrorMessage = $"فایل روی دیسک یافت نشد: {physicalPath}" });
        }

        var result = await _documentAIService.AnalyzeTableDocumentAsync(physicalPath, attachment.FileExtension, documentHint);

        ViewBag.Attachment = attachment;
        return View(result);
    }

    // مسیر بدون هوش مصنوعی: بازسازی جدول فقط از روی مختصات کلمات در PDF (DeterministicTableExtractor) -
    // برای اسناد بزرگ (ده‌ها صفحه) هم سریع‌تر است (بدون فراخوانی متوالی Ollama) و هم دقیق‌تر چون
    // چینش ستونی واقعی حفظ می‌شود؛ فقط برای PDF های Native (نه اسکن) کار می‌کند.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnalyzeDeterministic(int attachmentId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "TableExtraction", "View"))
            return Forbid();

        ViewData["Title"] = "Table Extraction Result (No AI)";
        ViewData["Section"] = "AI Engineering Intelligence";

        var attachment = await _context.DocumentAttachments.FirstOrDefaultAsync(x => x.AttachmentId == attachmentId);
        if (attachment == null) return NotFound();

        ViewBag.Attachment = attachment;

        if (!DeterministicTableExtractor.CanHandle(attachment.FileExtension))
        {
            return View("Analyze", new AIAnalysisResult
            {
                Success = false,
                ErrorMessage = $"استخراج بدون AI فقط برای PDF پشتیبانی می‌شود (فایل فعلی: {attachment.FileExtension})."
            });
        }

        var physicalPath = ResolvePhysicalPath(attachment);
        if (!System.IO.File.Exists(physicalPath))
        {
            return View("Analyze", new AIAnalysisResult { Success = false, ErrorMessage = $"فایل روی دیسک یافت نشد: {physicalPath}" });
        }

        try
        {
            var tableData = await Task.Run(() => DeterministicTableExtractor.Extract(physicalPath));
            return View("Analyze", new AIAnalysisResult
            {
                Success = true,
                Summary = tableData.Summary,
                Confidence = 1.0,
                UsedOcr = false,
                Warnings = tableData.Warnings,
                ExtractedTableData = tableData
            });
        }
        catch (Exception ex)
        {
            return View("Analyze", new AIAnalysisResult { Success = false, ErrorMessage = $"خطا در استخراج بدون AI: {ex.Message}" });
        }
    }

    public async Task<IActionResult> Review(int batchId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "TableExtraction", "View"))
            return Forbid();

        ViewData["Title"] = "Table Extraction Review";
        ViewData["Section"] = "AI Engineering Intelligence";

        var batch = await _context.TableExtractionBatches
            .Include(b => b.SourceAttachment)
            .Include(b => b.Rows).ThenInclude(r => r.MatchedEquipment)
            .Include(b => b.Rows).ThenInclude(r => r.ResultInstrument)
            .FirstOrDefaultAsync(b => b.TableExtractionBatchId == batchId);

        if (batch == null) return NotFound();

        return View(batch);
    }

    // attachment.FilePath یک مسیر نسبی وب است (مثل /uploads/intake/xxx.pdf)، نه مسیر فیزیکی دیسک
    private static string ResolvePhysicalPath(DocumentAttachment attachment) =>
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/', '\\'));
}
