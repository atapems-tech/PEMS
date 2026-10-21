using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

// ==============================================================================
// بخش DCC (Document Control Center) - کاملاً جدا از ماژول عمومی Correspondence، طبق
// درخواست صریح: "این بخش باید کاملاً جدا ساخته بشه". این کنترلر جای عملیاتی‌شدن طراحی
// سازمانی DCC (که قبلاً بدون کد ارائه شد) است:
//   - داشبورد: کارهای در انتظار DCC (مدارک آماده‌ی صدور، مکاتبات وارده‌ی وصل‌نشده)
//   - ثبت نامه‌ی وارده: در یک مرحله - پیوست فایل + لینک MR/VDR - نه فرم عمومی نامه که این
//     دو تا را در دو مرحله‌ی جدا می‌خواست
// ==============================================================================
public class DCCController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly ILetterService _letterService;
    private readonly ILetterNumberingService _numberingService;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;

    public DCCController(
        PEMSDbContext context,
        ILetterService letterService,
        ILetterNumberingService numberingService,
        ICurrentUserService currentUser,
        IFileStorageService fileStorage)
    {
        _context = context;
        _letterService = letterService;
        _numberingService = numberingService;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
    }


    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Document Control Center (DCC)";
        ViewData["Section"] = "DCC";

        // مدارک آماده‌ی صدور (Approved، منتظر اقدام DCC)
        ViewBag.PendingIssueCount = await _context.EngineeringDocuments
            .CountAsync(x => x.IsActive && x.Status == "Approved");

        var pendingIssueDocs = await _context.EngineeringDocuments
            .Where(x => x.IsActive && x.Status == "Approved")
            .OrderByDescending(x => x.ModifiedDate)
            .Take(10)
            .Select(x => new { x.DocumentId, x.DocumentNo, x.Title, x.ModifiedDate })
            .ToListAsync();
        ViewBag.PendingIssueDocs = pendingIssueDocs;

        // مکاتبات وارده‌ای که هنوز به هیچ مدرک/MR وصل نشده‌اند - نیاز به بررسی DCC دارند
        var unlinkedIncoming = await _context.Letters
            .Where(l => l.IsActive && l.LetterType == "Incoming"
                        && l.RelatedDocumentId == null && l.RelatedMRId == null)
            .OrderByDescending(l => l.LetterDate)
            .Take(15)
            .Select(l => new { l.LetterId, l.LetterNumber, l.Sender, l.Subject, l.LetterDate })
            .ToListAsync();
        ViewBag.UnlinkedIncomingCount = unlinkedIncoming.Count;
        ViewBag.UnlinkedIncoming = unlinkedIncoming;

        // آخرین مکاتبات وارده (لینک‌شده یا نشده) برای دید کلی
        ViewBag.RecentIncoming = await _context.Letters
            .Where(l => l.IsActive && l.LetterType == "Incoming")
            .OrderByDescending(l => l.LetterDate)
            .Take(10)
            .Select(l => new { l.LetterId, l.LetterNumber, l.Sender, l.Subject, l.LetterDate, l.RelatedDocumentId, l.RelatedMRId })
            .ToListAsync();

        return View();
    }


    [HttpGet]
    public async Task<IActionResult> RegisterIncoming()
    {
        ViewData["Title"] = "ثبت مکاتبه‌ی وارده - DCC";
        ViewData["Section"] = "DCC";

        await LoadDropdownsAsync();

        return View();
    }


    // ==============================================================================
    // طبق درخواست: نامه‌ی وارده ممکن است ایمیل، نامه‌ی رسمی، یا مکاتبه‌ی وندور باشد - همه در
    // یک فرم واحد، با امکان پیوست فایل و لینک مستقیم MR/VDR در همین مرحله (نه دو مرحله‌ی جدا).
    // ==============================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> RegisterIncoming(
        string sourceType, string sender, string subject, string? body,
        int? projectId, int? disciplineId, string priority,
        int? relatedDocumentId, int? relatedVdrItemId, int? relatedMRId,
        IFormFile? attachment)
    {
        if (string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(subject))
        {
            TempData["Error"] = "فرستنده و موضوع الزامی است.";
            return RedirectToAction(nameof(RegisterIncoming));
        }

        var employeeId = await GetCurrentEmployeeIdAsync();

        var letterNumber = await _numberingService.GenerateNextNumberAsync("Incoming", projectId, employeeId.HasValue
            ? await _context.Employees.Where(e => e.EmployeeId == employeeId.Value).Select(e => (int?)e.DepartmentId).FirstOrDefaultAsync()
            : null);

        // منبع (ایمیل/نامه‌ی رسمی/مکاتبه‌ی وندور) در ابتدای متن ثبت می‌شود - طبق تصمیم فعلی
        // بدون افزودن ستون جدید به Letter (Priority/Subject/Body برای این سطح دسته‌بندی کافی‌اند)
        var sourceLabel = sourceType switch
        {
            "Email" => "ایمیل",
            "VendorCommunication" => "مکاتبه‌ی وندور",
            _ => "نامه‌ی رسمی"
        };
        var fullBody = $"[منبع: {sourceLabel}]\n\n{body}";

        // اگر مدرک وندور (VDR) انتخاب شده، همان‌جا لینک می‌شود (VDRItem خودش یک Document است)
        var effectiveDocumentId = relatedVdrItemId ?? relatedDocumentId;

        var letterId = await _letterService.CreateAsync(
            letterType: "Incoming",
            letterNumber: letterNumber,
            letterDate: DateTime.Now,
            sender: sender,
            receiver: null,
            subject: subject,
            body: fullBody,
            priority: string.IsNullOrWhiteSpace(priority) ? "Normal" : priority,
            confidentialityLevel: "Internal",
            projectId: projectId,
            disciplineId: disciplineId,
            relatedDocumentId: effectiveDocumentId,
            relatedMRId: relatedMRId,
            registeredById: _currentUser.UserId);

        if (attachment != null && attachment.Length > 0)
        {
            string storageKey;
            await using (var uploadStream = attachment.OpenReadStream())
            {
                storageKey = await _fileStorage.SaveAsync("letters", attachment.FileName, uploadStream);
            }

            await _letterService.AddAttachmentAsync(
                letterId,
                attachment.FileName,
                Path.GetExtension(attachment.FileName),
                storageKey,
                attachment.Length,
                attachment.ContentType,
                employeeId);
        }

        TempData["Message"] = $"مکاتبه‌ی وارده با شماره‌ی {letterNumber} ثبت شد.";
        return RedirectToAction("Details", "Letter", new { id = letterId });
    }


    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;

        return await _context.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();
    }

    private async Task LoadDropdownsAsync()
    {
        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();

        // طبق درخواست: مدرک مهندسی و مدرک وندور (VDR) جدا از هم لیست می‌شوند - نه یک
        // Dropdown مشترک بزرگ که پیدا کردن VDR توش سخت باشه
        ViewBag.RelatedEngineeringDocuments = await _context.Set<EngineeringDocument>()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedDate)
            .Take(200)
            .Select(x => new { x.DocumentId, x.DocumentNo, x.Title })
            .ToListAsync();

        ViewBag.RelatedVdrItems = await _context.Set<VDRItem>()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedDate)
            .Take(200)
            .Select(x => new { x.DocumentId, x.DocumentNo, x.Title })
            .ToListAsync();

        ViewBag.RelatedMRs = await _context.MaterialRequisitions
            .OrderByDescending(x => x.CreatedDate)
            .Take(200)
            .Select(x => new { x.MRId, x.MRNumber })
            .ToListAsync();
    }
}
