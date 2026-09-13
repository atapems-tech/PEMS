using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Web.Security;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PEMS.Web.Controllers;

/// <summary>
/// ماژول مکاتبات رسمی: Incoming/Outgoing/Internal یکپارچه، گردش‌کار ارجاع، جستجو، Dashboard.
/// </summary>
public class LetterController : Controller
{
    private readonly ILetterService _letterService;
    private readonly ILetterReferralService _referralService;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly ITagService _tagService;
    private readonly IRichTextSanitizerService _richTextSanitizer;
    private readonly ICacheService _cache;
    private readonly ILetterNumberingService _numberingService;
    private readonly IFileStorageService _fileStorage;

    public LetterController(ILetterService letterService, ILetterReferralService referralService,
        PEMSDbContext context, ICurrentUserService currentUser, IPermissionService permissionService,
        ITagService tagService, IRichTextSanitizerService richTextSanitizer, ICacheService cache,
        ILetterNumberingService numberingService, IFileStorageService fileStorage)
    {
        _letterService = letterService;
        _referralService = referralService;
        _context = context;
        _currentUser = currentUser;
        _permissionService = permissionService;
        _tagService = tagService;
        _richTextSanitizer = richTextSanitizer;
        _cache = cache;
        _numberingService = numberingService;
        _fileStorage = fileStorage;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - P1): قبلاً pageSize=25 مستقیم و
    // بدون Normalize پاس داده می‌شد و از QueryString هم پذیرفته نمی‌شد.
    public async Task<IActionResult> Index(string? letterType, string? letterNumber, string? subject,
        string? sender, string? receiver, int? projectId, int? disciplineId, string? status,
        DateTime? fromDate, DateTime? toDate, bool starred = false, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Correspondence";
        ViewData["Section"] = "Correspondence Management";

        var isSystemAdmin = _currentUser.Roles.Contains("SystemAdmin");
        var canViewConfidential = isSystemAdmin ||
            await _permissionService.HasPermissionAsync(_currentUser.Roles, "Correspondence", "Approve");

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var result = await _letterService.SearchAsync(letterType, letterNumber, subject, sender,
            receiver, projectId, disciplineId, status, fromDate, toDate, page, normalizedPageSize,
            canViewConfidential, _currentUser.UserId, starredOnly: starred);

        var dashboard = await _letterService.GetDashboardAsync(_currentUser.UserId);

        ViewBag.Dashboard = dashboard;

        // طبق بند ۲۴/۲۸: Index هرگز از Employee/Document/MR/Tag استفاده نمی‌کند (فقط Project و
        // Discipline برای فیلتر/میانبر) - قبلاً همون LoadDropdownsAsync سنگین Create را صدا
        // می‌زد و همه‌ی این‌ها را بی‌فایده بارگذاری می‌کرد
        await LoadIndexDropdownsAsync();

        ViewBag.Filters = new { letterType, letterNumber, subject, sender, receiver, projectId, disciplineId, status, fromDate, toDate, starred };

        return View(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStar(int letterId, string? returnUrl)
    {
        if (_currentUser.UserId.HasValue)
            await _letterService.ToggleStarAsync(letterId, _currentUser.UserId.Value);

        return string.IsNullOrEmpty(returnUrl) ? RedirectToAction(nameof(Index)) : LocalRedirect(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRecipient(int letterId, string recipientType, int? employeeId, int? departmentId, string? externalName)
    {
        await _letterService.AddRecipientAsync(letterId, recipientType, employeeId, departmentId, externalName);
        TempData["Message"] = "Recipient added";
        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRecipientRead(int letterId, int letterRecipientId)
    {
        if (_currentUser.UserId.HasValue)
            await _letterService.MarkRecipientReadAsync(letterRecipientId, _currentUser.UserId.Value);

        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AttachTag(int letterId, int? tagId, string? newTagName, string? newTagColor)
    {
        var idToAttach = tagId;

        if (!idToAttach.HasValue && !string.IsNullOrWhiteSpace(newTagName))
            idToAttach = await _tagService.CreateAsync(newTagName, newTagColor ?? "secondary", _currentUser.UserId);

        if (idToAttach.HasValue)
            await _tagService.AttachToLetterAsync(letterId, idToAttach.Value);

        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DetachTag(int letterId, int tagId)
    {
        await _tagService.DetachFromLetterAsync(letterId, tagId);
        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    public async Task<IActionResult> MyReferrals()
    {
        ViewData["Title"] = "My Referrals";
        ViewData["Section"] = "Correspondence Management";

        if (_currentUser.UserId == null) return Forbid();

        var letters = await _referralService.GetMyReferralsAsync(_currentUser.UserId.Value);

        return View(letters);
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "Letter Details";
        ViewData["Section"] = "Correspondence Management";

        var detail = await _letterService.GetDetailAsync(id);
        if (detail == null) return NotFound();

        ViewBag.Timeline = await _letterService.GetTimelineAsync(id);
        // طبق بند ۱۹: ParentLetterId/Thread از قبل در سرویس وجود داشت اما هرگز از Details
        // فراخوانی نمی‌شد - نمای مکالمه‌ای (Gmail-Inspired) دقیقاً به همین داده نیاز دارد
        ViewBag.Thread = await _letterService.GetThreadAsync(id);

        // طبق بند ۷: فقط پرچم UI - محافظت واقعی در سرویس (CheckOriginalAuthorCanEdit) است، نه این‌جا
        ViewBag.CanEditReturned = _currentUser.UserId.HasValue
            && _currentUser.UserId.Value == detail.Letter.RegisteredById
            && detail.Letter.Status == "ReturnedForRevision";

        ViewBag.CanEditDraft = _currentUser.UserId.HasValue
            && _currentUser.UserId.Value == detail.Letter.RegisteredById
            && detail.Letter.Status == "Draft";

        ViewBag.CanReview = await _permissionService.HasPermissionAsync(_currentUser.Roles, "Correspondence", "Approve");

        // طبق بند ۲۶: صفحه‌ی جزئیات (عمدتاً فقط-خواندنی) نباید Dropdown های سنگین (همه‌ی
        // Employee/Document/MR) را بارگذاری کند - فقط زمانی که کاربر واقعاً دیالوگ افزودن
        // گیرنده/ارجاع/تگ را باز می‌کند، لازم است. این‌جا فقط موارد سبک (Department/Tag) از قبل
        // آماده می‌مانند و Employee با یک Endpoint سبک جداگانه (به‌محض باز شدن دیالوگ) پر می‌شود.
        await LoadLightDropdownsForDetailsAsync();

        return View(detail);
    }

    [HttpGet]
    public async Task<IActionResult> Reply(int id)
    {
        ViewData["Title"] = "Reply";
        ViewData["Section"] = "Correspondence Management";

        var parent = await _letterService.GetDetailAsync(id);
        if (parent == null) return NotFound();

        ViewBag.Thread = await _letterService.GetThreadAsync(id);
        ViewBag.ParentLetterId = id;
        ViewBag.DefaultSubject = "Re: " + parent.Letter.Subject;
        ViewBag.DefaultReceiver = parent.Letter.LetterType == "Incoming" ? parent.Letter.Sender : parent.Letter.Receiver;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(int parentLetterId, string subject, string? body, string? receiver)
    {
        var letterNumber = await _numberingService.GenerateNextNumberAsync("Outgoing", null);

        var id = await _letterService.ReplyAsync(parentLetterId, letterNumber, subject, body, receiver, _currentUser.UserId);

        TempData["Message"] = "Reply drafted";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Create(string letterType = "Incoming")
    {
        ViewData["Title"] = "New Letter";
        ViewData["Section"] = "Correspondence Management";

        ViewBag.LetterType = letterType;
        ViewBag.CanManualNumber = await _permissionService.HasPermissionAsync(_currentUser.Roles, "Correspondence", "ManualNumber");
        await LoadCreateDropdownsAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string letterType, string? letterNumber, DateTime letterDate,
        string? sender, string? receiver, string subject, string? body, string priority,
        string confidentialityLevel, int? projectId, int? disciplineId, int? relatedDocumentId, int? relatedMRId,
        bool useManualNumber = false)
    {
        var canManualNumber = await _permissionService.HasPermissionAsync(_currentUser.Roles, "Correspondence", "ManualNumber");

        if (!useManualNumber || !canManualNumber || string.IsNullOrWhiteSpace(letterNumber))
            letterNumber = await _numberingService.GenerateNextNumberAsync(letterType, projectId);

        var id = await _letterService.CreateAsync(letterType, letterNumber, letterDate, sender, receiver,
            subject, body, priority, confidentialityLevel, projectId, disciplineId, relatedDocumentId,
            relatedMRId, _currentUser.UserId);

        TempData["Message"] = "Letter registered successfully";
        return RedirectToAction(nameof(Details), new { id });
    }

    // آپلود تصویر داخل متن (طبق بررسی نوار ابزار CKEditor که فرستادید - تنها شکاف واقعی)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadEditorImage(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest();

        var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExt.Contains(ext)) return BadRequest("Unsupported image type");

        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "letters", "inline");
        if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

        var storedName = Guid.NewGuid() + ext;
        var physicalPath = Path.Combine(uploadPath, storedName);

        using (var stream = new FileStream(physicalPath, FileMode.Create))
            await file.CopyToAsync(stream);

        return Json(new { url = "/uploads/letters/inline/" + storedName });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAttachment(int letterId, IFormFile file)
    {
        if (file != null && file.Length > 0)
        {
            string storageKey;
            await using (var uploadStream = file.OpenReadStream())
            {
                storageKey = await _fileStorage.SaveAsync("letters", file.FileName, uploadStream);
            }

            await _context.DocumentAttachments.AddAsync(new DocumentAttachment
            {
                LetterId = letterId,
                FileName = file.FileName,
                FileExtension = Path.GetExtension(file.FileName),
                FilePath = storageKey,
                FileSize = file.Length,
                MimeType = file.ContentType,
                UploadDate = DateTime.Now,
                IsActive = true,
                AIStatus = "NotApplicable"
            });

            await _context.SaveChangesAsync();
        }

        TempData["Message"] = "Attachment uploaded";
        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    // ==============================================================================
    // طبق «PEMS Letter Details — Controlled Lifecycle» بند ۳۳: جایگزین AdvanceStatus قدیمی
    // (وضعیت آزاد بدون اعتبارسنجی) - هر اکشن زیر یک اقدام کسب‌وکاری صریح است؛ کاربر هرگز
    // وضعیت دلخواه انتخاب نمی‌کند و سرویس خودش گذار قانونی را اعمال/رد می‌کند.
    // ==============================================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitForReview(int letterId)
    {
        var (success, error) = await _letterService.SubmitForReviewAsync(letterId, _currentUser.UserId);
        TempData[success ? "Message" : "Error"] = success ? "Submitted for review" : error;
        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Correspondence", "Approve")]
    public async Task<IActionResult> Approve(int letterId, string? comment)
    {
        var (success, error) = await _letterService.ApproveAsync(letterId, _currentUser.UserId, comment);
        TempData[success ? "Message" : "Error"] = success ? "Letter approved" : error;
        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Correspondence", "Approve")]
    public async Task<IActionResult> ReturnForRevision(int letterId, string reason)
    {
        var (success, error) = await _letterService.ReturnForRevisionAsync(letterId, _currentUser.UserId, reason);
        TempData[success ? "Message" : "Error"] = success ? "Letter returned for revision" : error;
        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Correspondence", "Approve")]
    public async Task<IActionResult> Reject(int letterId, string reason)
    {
        var (success, error) = await _letterService.RejectAsync(letterId, _currentUser.UserId, reason);
        TempData[success ? "Message" : "Error"] = success ? "Letter rejected" : error;
        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Correspondence", "Approve")]
    public async Task<IActionResult> Issue(int letterId)
    {
        var (success, error) = await _letterService.IssueAsync(letterId, _currentUser.UserId);
        TempData[success ? "Message" : "Error"] = success ? "Letter issued" : error;
        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Correspondence", "Approve")]
    public async Task<IActionResult> Close(int letterId)
    {
        var (success, error) = await _letterService.CloseAsync(letterId, _currentUser.UserId);
        TempData[success ? "Message" : "Error"] = success ? "Letter closed" : error;
        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    // طبق بند ۴/۷/۱۰: ذخیره‌ی پیش‌نویس - بدون تغییر Status (نه ارسال مجدد). طبق بند ۷ صریحاً:
    // این مجوز بر اساس Role نیست - فقط مالکیت (RegisteredById) + وضعیت (Draft/ReturnedForRevision)
    // که سرویس (CheckOriginalAuthorCanEdit) واقعاً اجرا می‌کند، نه یک [RequirePermission] نقشی
    // که می‌تواند نویسنده‌ی واقعی را (اگر نقشش سطح Edit ندارد) از ویرایش کار خودش محروم کند.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDraft(int letterId, string subject, string body, byte[]? rowVersion)
    {
        if (!_currentUser.UserId.HasValue) return Forbid();

        var (success, error) = await _letterService.SaveDraftEditAsync(letterId, subject, body, _currentUser.UserId.Value, rowVersion);
        TempData[success ? "Message" : "Error"] = success ? "Draft saved" : error;
        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    // طبق بند ۷/۹/۳۵: فقط نویسنده‌ی اصلی، فقط از ReturnedForRevision - محافظت واقعی در سرویس است
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resubmit(int letterId, string subject, string body, byte[]? rowVersion)
    {
        if (!_currentUser.UserId.HasValue) return Forbid();

        var (success, error) = await _letterService.ResubmitAsync(letterId, subject, body, _currentUser.UserId.Value, rowVersion);
        TempData[success ? "Message" : "Error"] = success ? "Letter revised and resubmitted for review" : error;
        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refer(int letterId, int? referredToEmployeeId,
        int? referredToDepartmentId, string? instructions, DateTime? dueDate)
    {
        await _referralService.CreateAsync(letterId, referredToEmployeeId, referredToDepartmentId,
            instructions, dueDate, _currentUser.FullName);

        TempData["Message"] = "Letter referred";
        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkReferralRead(int referralId, int letterId)
    {
        if (_currentUser.UserId != null)
            await _referralService.MarkReadAsync(referralId, _currentUser.UserId.Value);

        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteReferral(int referralId, int letterId, string? responseNote)
    {
        await _referralService.CompleteAsync(referralId, responseNote);
        TempData["Message"] = "Referral marked as completed";
        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sign(int letterId, string signedByName)
    {
        await _letterService.SignAsync(letterId, signedByName);
        TempData["Message"] = "Letter signed";
        return RedirectToAction(nameof(Details), new { id = letterId });
    }

    public async Task<IActionResult> ExportPdf(int id)
    {
        var detail = await _letterService.GetDetailAsync(id);
        if (detail == null) return NotFound();

        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header().Column(col =>
                {
                    col.Item().Text("PEMS - Official Correspondence").FontSize(16).Bold();
                    col.Item().Text($"Letter No: {detail.Letter.LetterNumber}");
                    col.Item().Text($"Date: {detail.Letter.LetterDate:yyyy-MM-dd}");
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Item().Text($"To: {detail.Letter.Receiver}");
                    col.Item().Text($"Subject: {detail.Letter.Subject}").Bold();
                    col.Item().PaddingTop(10).Text(_richTextSanitizer.ToPlainText(detail.Body));
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generated by PEMS - ");
                    x.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                });
            });
        });

        var pdfBytes = document.GeneratePdf();

        return File(pdfBytes, "application/pdf", $"{detail.Letter.LetterNumber}.pdf");
    }


    // طبق بند ۲۴/۲۸: Index (فهرست کارتابل) فقط از Project/Discipline برای فیلتر و میانبر
    // دیسیپلین استفاده می‌کند - نه Employee/Document/MR/Tag که فقط صفحه‌ی Create به آن‌ها نیاز دارد
    private async Task LoadIndexDropdownsAsync()
    {
        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Disciplines = await _cache.GetOrCreateAsync("dropdown:disciplines", async () =>
            await _context.Disciplines.Where(x => x.IsActive)
                .Select(x => new { x.DisciplineId, x.Name }).ToListAsync());
    }

    // طبق بند ۲۴/۲۸: Create.cshtml هرگز از Employee/Department/Tag استفاده نمی‌کند (فقط
    // Project/Discipline/RelatedDocument/RelatedMR در بخش «More Details») - نسخه‌ی قدیمی این
    // متد (LoadDropdownsAsync) این‌ها را هم بی‌فایده بارگذاری می‌کرد
    private async Task LoadCreateDropdownsAsync()
    {
        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Disciplines = await _cache.GetOrCreateAsync("dropdown:disciplines", async () =>
            await _context.Disciplines.Where(x => x.IsActive)
                .Select(x => new { x.DisciplineId, x.Name }).ToListAsync());

        ViewBag.RelatedDocuments = await _context.Documents.Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedDate).Take(200)
            .Select(x => new { x.DocumentId, x.DocumentNo, x.Title }).ToListAsync();

        ViewBag.RelatedMRs = await _context.MaterialRequisitions
            .OrderByDescending(x => x.CreatedDate).Take(200)
            .Select(x => new { x.MRId, x.MRNumber }).ToListAsync();

        ViewBag.AllTags = await _tagService.GetAllAsync();
    }

    // طبق بند ۲۶: Details عمدتاً فقط-خواندنی است - نباید همه‌ی Employee/Document/MR را بارگذاری
    // کند فقط برای این‌که دیالوگ‌های افزودن گیرنده/ارجاع/تگ اصلاً باز نشده‌اند. فقط Department
    // (سبک) و Tag (سبک، از قبل Cache شده در سرویس خودش) از پیش آماده می‌مانند؛ Employees با
    // GetEmployeesForPicker (زیر) فقط وقتی دیالوگ واقعاً باز می‌شود بارگذاری می‌شود.
    private async Task LoadLightDropdownsForDetailsAsync()
    {
        ViewBag.Departments = await _context.Departments.Where(x => x.IsActive)
            .Select(x => new { x.DepartmentId, x.Name }).ToListAsync();

        ViewBag.AllTags = await _tagService.GetAllAsync();
    }

    [HttpGet]
    public async Task<IActionResult> GetEmployeesForPicker()
    {
        var employees = await _context.Employees.Where(x => x.IsActive)
            .OrderBy(x => x.FirstName).ThenBy(x => x.LastName)
            .Select(x => new { x.EmployeeId, Name = x.FirstName + " " + x.LastName })
            .ToListAsync();

        return Json(employees);
    }
}
