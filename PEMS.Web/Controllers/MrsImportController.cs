using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق سند Inventory بخش ۲۰-۲۱: Upload → Stage → Review → Commit برای فایل‌های واقعی MRS -
/// دقیقاً همون گردش‌کار TableExtractionController، فقط منبع Excel است نه PDF.
/// </summary>
public class MrsImportController : Controller
{
    private readonly IMrsImportService _service;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;
    private readonly IPermissionService _permissionService;

    public MrsImportController(IMrsImportService service, PEMSDbContext context, ICurrentUserService currentUser,
        IFileStorageService fileStorage, IPermissionService permissionService)
    {
        _service = service;
        _context = context;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _permissionService = permissionService;
    }

    // طبق «PEMS — Global Engineering Management UI/UX, Navigation & RBAC Hardening» بند ۱۲:
    // این Pipeline قبلاً هیچ بررسی مجوزی نداشت - BulkCommit (که MaterialItem/MaterialStock/
    // InventoryTransaction معتبر می‌ساخت) برای هر کاربر احرازهویت‌شده‌ای باز بود.
    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MrsImport", "View"))
            return Forbid();

        ViewData["Title"] = "MRS Import";
        ViewData["Section"] = "Inventory & Materials";
        ViewData["HelpKey"] = "Imports.StagedBatchOverview";

        var batches = await _service.GetBatchesAsync();
        return View(batches);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile file, string? sheetName)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MrsImport", "Create"))
            return Forbid();

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "فایلی انتخاب نشده.";
            return RedirectToAction(nameof(Index));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xlsm")
        {
            TempData["Error"] = "فقط فایل Excel (.xlsx) پشتیبانی می‌شود.";
            return RedirectToAction(nameof(Index));
        }

        // طبق سند Metadata Extraction بخش ۶ (Do Not Lose Document Context): خودِ فایل باید یک
        // DocumentAttachment واقعی شود، نه فقط SourceFileName متنی. Stream فایل آپلودی فقط یک‌بار
        // قابل‌خواندن است، پس ابتدا در حافظه بافر می‌شود تا هم روی دیسک ذخیره شود هم ClosedXML آن
        // را Parse کند - بدون این‌که استریم اصلی دو بار مصرف شود.
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);

        var attachmentId = await SaveMrsSourceFileAsync(file, buffer.ToArray());

        buffer.Position = 0;
        var (success, error, batchId) = await _service.StageFromExcelAsync(
            buffer, file.FileName, sheetName, await GetCurrentEmployeeIdAsync(), attachmentId);

        if (!success) { TempData["Error"] = error; return RedirectToAction(nameof(Index)); }

        return RedirectToAction(nameof(Review), new { batchId });
    }

    private async Task<int> SaveMrsSourceFileAsync(IFormFile file, byte[] bytes)
    {
        string storageKey;
        await using (var uploadStream = new MemoryStream(bytes))
        {
            storageKey = await _fileStorage.SaveAsync("mrs-import", file.FileName, uploadStream);
        }

        var attachment = new PEMS.Domain.Entities.DocumentAttachment
        {
            FileName = file.FileName,
            FileExtension = Path.GetExtension(file.FileName),
            FilePath = storageKey,
            FileSize = file.Length,
            MimeType = file.ContentType,
            UploadedById = await GetCurrentEmployeeIdAsync(),
            UploadDate = DateTime.Now,
            IsActive = true,
            AIStatus = "NotApplicable"
        };

        await _context.DocumentAttachments.AddAsync(attachment);
        await _context.SaveChangesAsync();

        return attachment.AttachmentId;
    }

    public async Task<IActionResult> Review(int batchId, string? matchStatusFilter, int page = 1, int? pageSize = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MrsImport", "View"))
            return Forbid();

        ViewData["Title"] = "MRS Import Review";
        ViewData["Section"] = "Inventory & Materials";

        // طبق ممیزی «Global 500-Row Batch Limitation Remediation»: صفحه‌بندی واقعی سمت سرور به‌جای
        // یک سقف ۵۰۰ ردیفی ثابت که ردیف‌های بعدی را برای همیشه نامرئی/غیرقابل‌عمل می‌کرد.
        var normalizedPageSize = PEMS.Application.Common.DataGridPageSize.Normalize(pageSize);
        var detail = await _service.GetBatchDetailAsync(batchId, matchStatusFilter, page, normalizedPageSize);
        if (detail == null) return NotFound();

        ViewBag.MatchStatusFilter = matchStatusFilter;
        return View(detail);
    }

    // طبق بند ۱۲ ممیزی: پرریسک‌ترین اکشن این Pipeline - MaterialItem/MaterialStock/
    // InventoryTransaction معتبر می‌سازد/تغییر می‌دهد.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkCommit(int batchId, List<int>? selectedRowIds)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MrsImport", "Approve"))
            return Forbid();

        if (selectedRowIds == null || selectedRowIds.Count == 0)
        {
            TempData["Error"] = "هیچ ردیفی انتخاب نشده.";
            return RedirectToAction(nameof(Review), new { batchId });
        }

        var (success, error, committed, skipped) = await _service.CommitAsync(batchId, selectedRowIds, await GetCurrentEmployeeIdAsync());

        if (!success) { TempData["Error"] = error; return RedirectToAction(nameof(Review), new { batchId }); }

        TempData["Message"] = $"{committed} ردیف تایید و به MaterialItem/InventoryTransaction تبدیل شد." + (skipped > 0 ? $" ({skipped} ردیف نامعتبر رد شد)" : "");
        return RedirectToAction(nameof(Review), new { batchId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkReject(int batchId, List<int>? selectedRowIds)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MrsImport", "Create"))
            return Forbid();

        if (selectedRowIds == null || selectedRowIds.Count == 0)
        {
            TempData["Error"] = "هیچ ردیفی انتخاب نشده.";
            return RedirectToAction(nameof(Review), new { batchId });
        }

        await _service.RejectRowsAsync(batchId, selectedRowIds, await GetCurrentEmployeeIdAsync());
        TempData["Message"] = $"{selectedRowIds.Count} ردیف رد شد.";
        return RedirectToAction(nameof(Review), new { batchId });
    }

    // طبق ممیزی «Global 500-Row Batch Limitation Remediation»: Commit یک دسته‌ی کامل (مثلاً همه‌ی
    // ردیف‌های New) در سراسر Batch - نه فقط ردیف‌های صفحه‌ی جاری - با یک اقدام صریح. کاربر باید
    // یک فیلتر مشخص انتخاب کرده باشد (بررسی انسانی حفظ می‌شود)؛ خودِ Commit همچنان از همون منطق
    // تطبیق/ساخت Vendor/MaterialItem استفاده می‌کند که BulkCommit استفاده می‌کند.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CommitByStatus(int batchId, string matchStatusFilter)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MrsImport", "Approve"))
            return Forbid();

        var (success, error, committed, skipped) = await _service.CommitByStatusAsync(batchId, matchStatusFilter, await GetCurrentEmployeeIdAsync());

        if (!success) { TempData["Error"] = error; return RedirectToAction(nameof(Review), new { batchId, matchStatusFilter }); }

        TempData["Message"] = $"{committed} ردیف («{matchStatusFilter}»، سراسر Batch) تایید و به MaterialItem/InventoryTransaction تبدیل شد." + (skipped > 0 ? $" ({skipped} ردیف نامعتبر رد شد)" : "");
        return RedirectToAction(nameof(Review), new { batchId, matchStatusFilter });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectByStatus(int batchId, string matchStatusFilter)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MrsImport", "Create"))
            return Forbid();

        var (success, error, count) = await _service.RejectByStatusAsync(batchId, matchStatusFilter, await GetCurrentEmployeeIdAsync());

        if (!success) { TempData["Error"] = error; return RedirectToAction(nameof(Review), new { batchId, matchStatusFilter }); }

        TempData["Message"] = $"{count} ردیف («{matchStatusFilter}»، سراسر Batch) رد شد.";
        return RedirectToAction(nameof(Review), new { batchId, matchStatusFilter });
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }
}
