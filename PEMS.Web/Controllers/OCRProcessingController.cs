using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// داشبورد صف پردازش OCR/AI: وضعیت تمام پیوست‌ها (Pending/Processing/Completed/Failed)
/// و امکان Retry برای موارد ناموفق.
/// </summary>
/// <remarks>
/// طبق «PEMS — P0 AI Pipeline Endpoint Hardening»: این Controller قبلاً هیچ بررسی مجوزی نداشت -
/// Retry (که یک Background Job جدید در Hangfire صف می‌کند و منابع سرور/Ollama مصرف می‌کند) برای
/// هر کاربر احرازهویت‌شده‌ای باز بود - خطر مصرف منابع/DoS با صف‌کردن بی‌رویه‌ی Retry. ماژول جدید
/// "OCRProcessing" چون هیچ ماژول موجودی این قابلیت (کنترل صف پردازش OCR/AI سراسری، مستقل از
/// نوع مدرک) را پوشش نمی‌دهد.
/// </remarks>
public class OCRProcessingController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OCRProcessingController> _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public OCRProcessingController(
        PEMSDbContext context,
        IServiceScopeFactory scopeFactory,
        ILogger<OCRProcessingController> logger,
        ICurrentUserService currentUser,
        IPermissionService permissionService)
    {
        _context = context;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - P2): قبلاً Take(200) ثابت بود
    // (بدون Skip/pageSize واقعی) - حالا صفحه‌بندی واقعی SQL. شمارنده‌های Dashboard (counts)
    // از قبل هم SQL-side بودند (GroupBy واقعی، نه بارگذاری کامل جدول) - عمداً بدون تغییر
    // مانده‌اند چون Scope شان (کل جدول، مستقل از فیلتر status) با Grid فیلترشده فرق دارد؛
    // ادغام این دو Query رفتار Tabهای بالای صفحه را می‌شکست (طبق بند G-5 دستورالعمل فاز ۶B:
    // "اگر Counter عمداً Global است در حالی که Grid فیلترشده، آن رفتار را حفظ کن").
    public async Task<IActionResult> Index(string? status, int page = 1, int? pageSize = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "OCRProcessing", "View"))
            return Forbid();

        ViewData["Title"] = "OCR Processing";
        ViewData["Section"] = "AI Engineering Intelligence";

        var counts = await _context.DocumentAttachments
            .GroupBy(x => x.AIStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        ViewBag.Counts = counts.ToDictionary(x => x.Status, x => x.Count);

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var normalizedPage = page < 1 ? 1 : page;

        var query = _context.DocumentAttachments.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.AIStatus == status);

        var totalCount = await query.CountAsync();

        // طبق A-3: Include(Document) دیگر لازم نیست - فقط DocumentNo از آن استفاده می‌شود که
        // مستقیم در Select قابل‌دریافت است.
        var attachments = await query
            .OrderByDescending(x => x.UploadDate)
            .ThenByDescending(x => x.AttachmentId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new OCRProcessingAttachmentListDto
            {
                AttachmentId = x.AttachmentId,
                FileName = x.FileName,
                DocumentNo = x.Document != null ? x.Document.DocumentNo : null,
                FileExtension = x.FileExtension,
                FileSize = x.FileSize,
                AIStatus = x.AIStatus,
                AISummary = x.AISummary,
                AIError = x.AIError,
                UploadDate = x.UploadDate
            })
            .ToListAsync();

        ViewBag.SelectedStatus = status;

        return View(new PagedResult<OCRProcessingAttachmentListDto>
        {
            Items = attachments,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "OCRProcessing", "Create"))
            return Forbid();

        Hangfire.BackgroundJob.Enqueue<IDocumentAIIngestionService>(x => x.ProcessAttachmentAsync(id));

        TempData["Message"] = "Re-analysis started in the background.";
        return RedirectToAction(nameof(Index));
    }
}
