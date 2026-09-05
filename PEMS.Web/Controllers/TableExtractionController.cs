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

    public TableExtractionController(
        PEMSDbContext context,
        IDocumentAIService documentAIService,
        ITableExtractionImportService importService,
        ICurrentUserService currentUser)
    {
        _context = context;
        _documentAIService = documentAIService;
        _importService = importService;
        _currentUser = currentUser;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - P2): قبلاً Take(50) ثابت بدون
    // Skip/CountAsync/pageSize بود - یعنی هیچ راهی برای دیدن Attachmentهای قدیمی‌تر از ۵۰تای
    // اخیر نبود. حالا صفحه‌بندی واقعی SQL با Skip/Take/CountAsync است.
    public async Task<IActionResult> Index(int page = 1, int? pageSize = null)
    {
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

    // جدول را دوباره استخراج می‌کند (بدون AI - سریع) و به‌عنوان یک Batch برای بازبینی/اتصال به
    // Equipment می‌نشیند. هیچ Equipment/Instrument ای این‌جا ساخته یا تغییر داده نمی‌شود - فقط
    // تطبیق پیشنهادی محاسبه می‌شود؛ نوشتن واقعی فقط با ConfirmRow اتفاق می‌افتد.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StageForReview(int attachmentId)
    {
        var attachment = await _context.DocumentAttachments.FirstOrDefaultAsync(x => x.AttachmentId == attachmentId);
        if (attachment == null) return NotFound();

        if (!DeterministicTableExtractor.CanHandle(attachment.FileExtension))
        {
            TempData["Error"] = $"Stage کردن فقط برای PDF پشتیبانی می‌شود (فایل فعلی: {attachment.FileExtension}).";
            return RedirectToAction(nameof(Index));
        }

        var physicalPath = ResolvePhysicalPath(attachment);
        if (!System.IO.File.Exists(physicalPath))
        {
            TempData["Error"] = $"فایل روی دیسک یافت نشد: {physicalPath}";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var tableData = await Task.Run(() => DeterministicTableExtractor.Extract(physicalPath));
            var inputs = tableData.Rows.Select(ToInput).ToList();

            var employeeId = await GetCurrentEmployeeIdAsync();
            var batchId = await _importService.StageAsync(attachmentId, "Deterministic", inputs, employeeId);

            return RedirectToAction(nameof(Review), new { batchId });
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"خطا در استخراج/Stage: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    public async Task<IActionResult> Review(int batchId)
    {
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

    // برای ردیف‌هایی که از قبل با Equipment تطبیق پیدا کرده‌اند (MatchedNoInstrument/MatchedHasInstrument) -
    // equipmentId مستقیم از پیشنهاد Stage می‌آید، نه انتخاب دستی
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmRow(int rowId, int equipmentId, int batchId)
    {
        try
        {
            await _importService.ConfirmRowAsync(rowId, equipmentId, await GetCurrentEmployeeIdAsync());
            TempData["Message"] = "تایید شد و به Instrument متصل شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Review), new { batchId });
    }

    // بجای اینکه کاربر مجبور باشد صدها ردیف را یکی‌یکی Confirm کند (هر کدام یک POST/Reload کامل)،
    // ردیف‌هایی که از قبل تطبیق خودکار پیدا کرده‌اند (چک‌باکس‌های صفحه‌ی Review) در یک درخواست
    // Confirm می‌شوند - طبق همان الگوی RelationshipController.BulkConfirm
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkConfirm(int batchId, List<int>? selectedRowIds)
    {
        if (selectedRowIds == null || selectedRowIds.Count == 0)
        {
            TempData["Error"] = "هیچ ردیفی انتخاب نشده.";
            return RedirectToAction(nameof(Review), new { batchId });
        }

        var (confirmed, errors) = await _importService.BulkConfirmMatchedAsync(selectedRowIds, await GetCurrentEmployeeIdAsync());

        TempData["Message"] = $"{confirmed} ردیف تایید و به Instrument متصل شد.";
        if (errors.Any())
            TempData["Error"] = $"{errors.Count} ردیف خطا داشت: " + string.Join(" | ", errors.Take(5));

        return RedirectToAction(nameof(Review), new { batchId });
    }

    // برای ردیف‌های Unmatched - کاربر شماره‌تگ را دستی وارد می‌کند (به‌جای یک Dropdown چندصدتایی
    // که برای صدها ردیف صفحه را سنگین می‌کرد)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmRowManual(int rowId, int batchId, string equipmentTagNumber)
    {
        var equipment = await _context.Equipments.FirstOrDefaultAsync(e => e.TagNumber == equipmentTagNumber);
        if (equipment == null)
        {
            TempData["Error"] = $"هیچ Equipment ای با تگ «{equipmentTagNumber}» پیدا نشد.";
            return RedirectToAction(nameof(Review), new { batchId });
        }

        try
        {
            await _importService.ConfirmRowAsync(rowId, equipment.EquipmentId, await GetCurrentEmployeeIdAsync());
            TempData["Message"] = "تایید شد و به Instrument متصل شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Review), new { batchId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRow(int rowId, int batchId, string? comment)
    {
        await _importService.RejectRowAsync(rowId, await GetCurrentEmployeeIdAsync(), comment);
        return RedirectToAction(nameof(Review), new { batchId });
    }

    private static TableExtractionRowInput ToInput(ExtractedTableRow r) => new()
    {
        TagNumber = r.TagNumber,
        Description = r.Description,
        JunctionBox = r.JunctionBox,
        Terminal = r.Terminal,
        CableNo = r.CableNo,
        MarshallingCabinet = r.MarshallingCabinet,
        MarshallingTerminal = r.MarshallingTerminal,
        SystemCabinet = r.SystemCabinet,
        IoCard = r.IoCard,
        IoChannel = r.IoChannel,
        IoAddress = r.IoAddress,
        DcsTagNo = r.DcsTagNo,
        LoopNo = r.LoopNo,
        System = r.System,
        SignalClass = r.SignalClass,
        SpareOrHotSpare = r.SpareOrHotSpare,
        Remarks = r.Remarks,
        RawExtra = r.RawExtra
    };

    // attachment.FilePath یک مسیر نسبی وب است (مثل /uploads/intake/xxx.pdf)، نه مسیر فیزیکی دیسک -
    // همان الگوی resolve که IntakeService.ProcessIntakeAsync و DocumentAIIngestionService استفاده می‌کنند
    private static string ResolvePhysicalPath(DocumentAttachment attachment) =>
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/', '\\'));

    // همان الگوی GetCurrentEmployeeIdAsync در MOCController/TechnicalQueryController - UserId هویتی
    // (Identity) به EmployeeId واقعی map می‌شود، چون FK های Reviewed/CreatedBy به Employee اشاره دارند
    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;

        return await _context.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();
    }
}
