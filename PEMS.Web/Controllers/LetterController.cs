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
        DateTime? fromDate, DateTime? toDate, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Correspondence";
        ViewData["Section"] = "Correspondence Management";

        var isSystemAdmin = _currentUser.Roles.Contains("SystemAdmin");
        var canViewConfidential = isSystemAdmin ||
            await _permissionService.HasPermissionAsync(_currentUser.Roles, "Correspondence", "Approve");

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var result = await _letterService.SearchAsync(letterType, letterNumber, subject, sender,
            receiver, projectId, disciplineId, status, fromDate, toDate, page, normalizedPageSize,
            canViewConfidential, _currentUser.UserId);

        var dashboard = await _letterService.GetDashboardAsync(_currentUser.UserId);

        ViewBag.Dashboard = dashboard;
        await LoadDropdownsAsync();

        ViewBag.Filters = new { letterType, letterNumber, subject, sender, receiver, projectId, disciplineId, status, fromDate, toDate };

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

        await LoadDropdownsAsync();

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
        await LoadDropdownsAsync();

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Correspondence", "Approve")]
    public async Task<IActionResult> AdvanceStatus(int letterId, string newStatus)
    {
        await _letterService.AdvanceStatusAsync(letterId, newStatus, _currentUser.UserId);
        TempData["Message"] = $"Status updated to {newStatus}";
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
    public async Task<IActionResult> UpdateBody(int letterId, string body)
    {
        await _letterService.UpdateBodyAsync(letterId, body);
        TempData["Message"] = "Letter body updated";
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


    private async Task LoadDropdownsAsync()
    {
        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Disciplines = await _cache.GetOrCreateAsync("dropdown:disciplines", async () =>
            await _context.Disciplines.Where(x => x.IsActive)
                .Select(x => new { x.DisciplineId, x.Name }).ToListAsync());

        ViewBag.Departments = await _context.Departments.Where(x => x.IsActive)
            .Select(x => new { x.DepartmentId, x.Name }).ToListAsync();

        ViewBag.Employees = await _context.Employees.Where(x => x.IsActive)
            .Select(x => new { x.EmployeeId, Name = x.FirstName + " " + x.LastName }).ToListAsync();

        ViewBag.RelatedDocuments = await _context.Documents.Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedDate).Take(200)
            .Select(x => new { x.DocumentId, x.DocumentNo, x.Title }).ToListAsync();

        ViewBag.RelatedMRs = await _context.MaterialRequisitions
            .OrderByDescending(x => x.CreatedDate).Take(200)
            .Select(x => new { x.MRId, x.MRNumber }).ToListAsync();

        ViewBag.AllTags = await _tagService.GetAllAsync();
    }
}
