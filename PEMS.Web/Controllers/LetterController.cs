using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Web.Helpers;
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
    private readonly INotificationService _notifications;
    private readonly ILetterTemplateService? _templates;
    private readonly ILetterReminderService? _reminders;
    private readonly ILetterComposeService? _composeService;
    private readonly IConfiguration _configuration;
    private readonly ILetterCartableService? _cartable;
    private readonly ILetterTagTreeService? _tagTree;
    private readonly ILetterUserSettingsService? _userSettings;

    public LetterController(ILetterService letterService, ILetterReferralService referralService,
        PEMSDbContext context, ICurrentUserService currentUser, IPermissionService permissionService,
        ITagService tagService, IRichTextSanitizerService richTextSanitizer, ICacheService cache,
        ILetterNumberingService numberingService, IFileStorageService fileStorage,
        INotificationService notifications, ILetterTemplateService? templateService = null,
        ILetterReminderService? reminderService = null, ILetterComposeService? composeService = null,
        IConfiguration? configuration = null, ILetterCartableService? cartable = null,
        ILetterTagTreeService? tagTree = null, ILetterUserSettingsService? userSettings = null)
    {
        _cartable = cartable;
        _tagTree = tagTree;
        _userSettings = userSettings;
        _composeService = composeService;
        _configuration = configuration ?? new ConfigurationBuilder().Build();
        _templates = templateService;
        _reminders = reminderService;
        _notifications = notifications;
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

    private async Task<bool> CanViewConfidentialAsync()
        => _currentUser.Roles.Contains("SystemAdmin") || await _permissionService.HasPermissionAsync(_currentUser.Roles, "Correspondence", "Approve");

    // بیننده‌ی فعلی برای قاعده‌ی دسترسی نامه (LetterAccessRules): کاربر، کارمند مرتبط، و «دسترسی کامل» (SystemAdmin یا Approve)
    private async Task<LetterViewer> BuildViewerAsync()
    {
        var userId = _currentUser.UserId;
        int? employeeId = userId.HasValue
            ? await _context.Users.Where(u => u.Id == userId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync()
            : null;
        var canSeeAll = _currentUser.Roles.Contains("SystemAdmin")
            || await _permissionService.HasPermissionAsync(_currentUser.Roles, "Correspondence", "Approve");

        return new LetterViewer(userId, employeeId, canSeeAll, _currentUser.UserName);
    }

    // هر اقدام روی یک نامه‌ی مشخص (ستاره، گیرنده، برچسب، پیوست، ارجاع، امضا، پاسخ...) پیش از اجرا حق دیدن همان نامه را می‌سنجد؛
    // قبلاً فقط باز کردن صفحه کنترل نمی‌شد و ID حدس‌زده‌شده برای نامه‌ی محرمانه‌ی دیگران هم کار می‌کرد.
    // بخش کاربر جاری برای پیشوند شماره‌ی نامه ([DEPT]-[YEAR]-[SEQ])؛ بدون کارمند/بخش → null و شمارنده GEN را می‌گذارد
    private async Task<int?> GetCurrentDepartmentIdAsync()
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue) return null;
        var employeeId = await _context.Users.Where(u => u.Id == userId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
        if (!employeeId.HasValue) return null;
        return await _context.Employees.Where(e => e.EmployeeId == employeeId.Value).Select(e => (int?)e.DepartmentId).FirstOrDefaultAsync();
    }

    private async Task<IActionResult?> DenyIfNoAccessAsync(int letterId)
        => await _letterService.CanViewAsync(letterId, await BuildViewerAsync()) ? null : Forbid();

    // پاسخ در همان فرم رسمی نوشتن نامه باز می‌شود (پیوست، تایید، محرمانگی، ذخیره‌ی خودکار)؛ دسترسی به نامه‌ی اصلی آنجا بررسی می‌شود
    [HttpGet]
    public IActionResult Reply(int id, int? templateId = null)
        => RedirectToAction("Compose", "LetterCompose", new { replyTo = id, templateId });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(int parentLetterId, string subject, string? body, string? receiver)
    {
        var denied = await DenyIfNoAccessAsync(parentLetterId);
        if (denied != null) return denied;

        var letterNumber = await _numberingService.GenerateNextNumberAsync("Outgoing", null, await GetCurrentDepartmentIdAsync());

        var id = await _letterService.ReplyAsync(parentLetterId, letterNumber, subject, body, receiver, _currentUser.UserId);

        TempData["Message"] = "Reply drafted";
        return Redirect($"/p/letter-details?id={id}");
    }

    [HttpGet]
    public async Task<IActionResult> Create(string letterType = "Incoming", int? templateId = null)
    {
        // Internal / Outgoing letters are written in the formal composer (sender block, To/Cc/Bcc, category, approval chain, auto-save)
        if (letterType is "Internal" or "Outgoing")
            return RedirectToAction("Compose", "LetterCompose", new { letterType, templateId });

        ViewData["Title"] = "New Letter";
        ViewData["Section"] = "Correspondence Management";

        ViewBag.LetterType = letterType;
        ViewBag.CanManualNumber = await _permissionService.HasPermissionAsync(_currentUser.Roles, "Correspondence", "ManualNumber");
        await LoadCreateDropdownsAsync();
        await LoadTemplatesAsync(letterType);

        // قالب فقط مقدار اولیه‌ی فرم را پر می‌کند؛ قالب غیرفعال یا نامناسب برای این نوع نادیده گرفته می‌شود
        if (templateId.HasValue && _templates != null)
        {
            var template = await _templates.GetAsync(templateId.Value);
            if (template != null && template.IsActive && (template.LetterType == null || template.LetterType == letterType))
            {
                ViewBag.Prefill = ApplyPlaceholders(template, PlaceholderValues(letterType, null));
                ViewBag.SelectedTemplateId = template.LetterTemplateId;
            }
        }

        return View();
    }

    // مقادیر نشانه‌های قالب. نشانه‌های پاسخ (Parent*) فقط وقتی نامه‌ی مادر هست مقدار می‌گیرند؛ در ایجاد نامه دست‌نخورده می‌مانند.
    private Dictionary<string, string> PlaceholderValues(string letterType, LetterDetailDto? parent)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Date"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["UserName"] = _currentUser.FullName ?? _currentUser.UserName ?? "",
            ["LetterType"] = letterType
        };

        if (parent != null)
        {
            var p = parent.Letter;
            values["ParentNumber"] = p.LetterNumber;
            values["ParentSubject"] = p.Subject;
            values["ParentDate"] = p.LetterDate.ToString("yyyy-MM-dd");
            values["Counterparty"] = (p.LetterType == "Incoming" ? p.Sender : p.Receiver) ?? "";
            values["ParentProject"] = p.ProjectName ?? "";
        }

        return values;
    }

    private static LetterTemplateDto ApplyPlaceholders(LetterTemplateDto template, IReadOnlyDictionary<string, string> values) => new()
    {
        LetterTemplateId = template.LetterTemplateId,
        Name = template.Name,
        LetterType = template.LetterType,
        Subject = LetterTemplatePlaceholders.Apply(template.Subject, values, htmlEncode: false),
        Body = LetterTemplatePlaceholders.Apply(template.Body, values, htmlEncode: true),
        Priority = template.Priority,
        ConfidentialityLevel = template.ConfidentialityLevel,
        IsActive = template.IsActive
    };

    private async Task LoadTemplatesAsync(string letterType)
    {
        ViewBag.Templates = _templates == null
            ? new List<LetterTemplateDto>()
            : await _templates.ListAsync(letterType, includeInactive: false);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string letterType, string? letterNumber, DateTime letterDate,
        string? sender, string? receiver, string subject, string? body, string priority,
        string confidentialityLevel, int? projectId, int? disciplineId, int? relatedDocumentId, int? relatedMRId,
        bool useManualNumber = false, bool responseRequired = false, DateTime? responseDueDate = null)
    {
        // اعتبارسنجی پاسخ‌خواهی پیش از هر تولید شماره؛ در خطا، فرم با همان مقادیر تایپ‌شده دوباره نمایش داده می‌شود (متن نامه از دست نمی‌رود)
        var responseError = LetterResponseRules.Validate(responseRequired, responseDueDate, letterDate);
        if (responseError != null)
        {
            ViewData["Title"] = "New Letter";
            ViewData["Section"] = "Correspondence Management";
            ModelState.AddModelError("responseDueDate", responseError);
            ViewBag.LetterType = letterType;
            ViewBag.CanManualNumber = await _permissionService.HasPermissionAsync(_currentUser.Roles, "Correspondence", "ManualNumber");
            await LoadCreateDropdownsAsync();
            await LoadTemplatesAsync(letterType);
            ViewBag.Prefill = new LetterTemplateDto { Subject = subject, Body = body, Priority = priority, ConfidentialityLevel = confidentialityLevel };
            ViewBag.PrefillSender = sender;
            ViewBag.PrefillReceiver = receiver;
            ViewBag.PrefillDate = letterDate;
            ViewBag.PrefillResponseRequired = responseRequired;
            ViewBag.PrefillResponseDue = responseDueDate;
            ViewBag.ResponseError = responseError;
            return View();
        }

        var canManualNumber = await _permissionService.HasPermissionAsync(_currentUser.Roles, "Correspondence", "ManualNumber");

        if (!useManualNumber || !canManualNumber || string.IsNullOrWhiteSpace(letterNumber))
            letterNumber = await _numberingService.GenerateNextNumberAsync(letterType, projectId, await GetCurrentDepartmentIdAsync());

        var id = await _letterService.CreateAsync(letterType, letterNumber, letterDate, sender, receiver,
            subject, body, priority, confidentialityLevel, projectId, disciplineId, relatedDocumentId,
            relatedMRId, _currentUser.UserId, responseRequired, responseDueDate);

        TempData["Message"] = "Letter registered successfully";
        return Redirect($"/p/letter-details?id={id}");
    }

    // آپلود تصویر داخل متن (طبق بررسی نوار ابزار CKEditor که فرستادید - تنها شکاف واقعی)
    // SCRUM-202: type, size and content are checked by the upload guard; the image is stored outside wwwroot and served by Files/Image.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [PEMS.Web.Security.Uploads.UploadPolicy(PEMS.Application.Uploads.UploadProfile.Image)]
    public async Task<IActionResult> UploadEditorImage(IFormFile file, [FromServices] PEMS.Application.Uploads.IProtectedImageStore images)
    {
        if (file == null || file.Length == 0) return BadRequest();

        await using var stream = file.OpenReadStream();
        var url = await images.SaveAsync(PEMS.Application.Uploads.ProtectedImageStore.LetterImages, file.FileName, stream);
        return Json(new { url });
    }

    // ==============================================================================
    // طبق «PEMS Letter Details — Controlled Lifecycle» بند ۳۳: جایگزین AdvanceStatus قدیمی
    // (وضعیت آزاد بدون اعتبارسنجی) - هر اکشن زیر یک اقدام کسب‌وکاری صریح است؛ کاربر هرگز
    // وضعیت دلخواه انتخاب نمی‌کند و سرویس خودش گذار قانونی را اعمال/رد می‌کند.
    // ==============================================================================

    // تایید/برگشت/رد: دارنده‌ی مجوز Approve ماژول، مدیر سیستم، یا تاییدکننده‌ی تعیین‌شده برای «مرحله‌ی جاری» زنجیره‌ی تایید همین نامه.
    // تعیین‌شدن در زنجیره (توسط نویسنده، از فهرست کارکنان/مافوق‌ها) همان اختیار است؛ سرویس هم دوباره فقط مرحله‌ی جاری و نه نویسنده را می‌پذیرد.
    private async Task<bool> CanReviewLetterAsync(int letterId)
    {
        if (await CanViewConfidentialAsync()) return true;
        var userId = _currentUser.UserId;
        if (!userId.HasValue) return false;
        var current = await _context.LetterApprovalSteps.AsNoTracking()
            .Where(s => s.LetterId == letterId && s.Status == "Pending").OrderBy(s => s.StepOrder).Select(s => (int?)s.ApproverUserId).FirstOrDefaultAsync();
        return current == userId.Value;
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

        ViewBag.AllTags = await _tagService.GetAllAsync(_currentUser.UserId);
    }

    // طبق بند ۲۶: Details عمدتاً فقط-خواندنی است - نباید همه‌ی Employee/Document/MR را بارگذاری
    // کند فقط برای این‌که دیالوگ‌های افزودن گیرنده/ارجاع/تگ اصلاً باز نشده‌اند. فقط Department
    // (سبک) و Tag (سبک، از قبل Cache شده در سرویس خودش) از پیش آماده می‌مانند؛ Employees با
    // GetEmployeesForPicker (زیر) فقط وقتی دیالوگ واقعاً باز می‌شود بارگذاری می‌شود.
    private async Task LoadLightDropdownsForDetailsAsync()
    {
        ViewBag.Departments = await _context.Departments.Where(x => x.IsActive)
            .Select(x => new { x.DepartmentId, x.Name }).ToListAsync();

        ViewBag.AllTags = await _tagService.GetAllAsync(_currentUser.UserId);
    }
}
