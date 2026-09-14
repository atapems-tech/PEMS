using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Web.Controllers;

public class VDRController : Controller
{
    private readonly IVDRService _service;
    private readonly IDocumentCommentService _commentService;
    private readonly IVdrWorkflowService _workflowService;
    private readonly ILetterService _letterService;
    private readonly IWorkflowRuntime _runtime;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IVdrDocumentNumberingService _numberingService;
    private readonly IFtpUploadService _ftpService;
    private readonly IFileStorageService _fileStorage;
    private readonly IPermissionService _permissionService;

    public VDRController(IVDRService service, IDocumentCommentService commentService,
        IVdrWorkflowService workflowService, ILetterService letterService, IWorkflowRuntime runtime,
        PEMSDbContext context, ICurrentUserService currentUser, IVdrDocumentNumberingService numberingService,
        IFtpUploadService ftpService, IFileStorageService fileStorage, IPermissionService permissionService)
    {
        _service = service;
        _commentService = commentService;
        _workflowService = workflowService;
        _letterService = letterService;
        _runtime = runtime;
        _context = context;
        _currentUser = currentUser;
        _numberingService = numberingService;
        _ftpService = ftpService;
        _fileStorage = fileStorage;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Index(int? projectId, int? vendorId, string? status, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Vendor Document Register (VDR)";
        ViewData["Section"] = "Engineering Management";

        var result = await _service.GetPagedAsync(projectId, vendorId, status, page, PEMS.Application.Common.DataGridPageSize.Normalize(pageSize));
        var dashboard = await _service.GetDashboardAsync(projectId);

        ViewBag.Dashboard = dashboard;

        ViewBag.Projects = await _context.Projects.Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();
        ViewBag.Vendors = await _context.Vendors.Select(x => new { x.VendorId, x.Name }).ToListAsync();

        ViewBag.SelectedProjectId = projectId;
        ViewBag.SelectedVendorId = vendorId;
        ViewBag.SelectedStatus = status;

        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "VDR Item Details";
        ViewData["Section"] = "Engineering Management";

        var item = await _service.GetByIdAsync(id);
        if (item == null) return NotFound();

        ViewBag.Comments = await _commentService.GetForDocumentAsync(id);

        ViewBag.AllDisciplines = await _context.Disciplines.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        ViewBag.ReviewStatus = await _workflowService.GetDisciplineReviewStatusAsync(id);
        ViewBag.MyDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();

        var activeInstance = await _context.WorkflowInstances
            .Where(x => x.Module == "VendorDocument" && x.BusinessObjectType == "VDRItem" && x.BusinessObjectId == id)
            .OrderByDescending(x => x.StartDate)
            .Select(x => new { x.Status, x.WorkflowInstanceId, StageName = x.CurrentStage != null ? x.CurrentStage.Name : null })
            .FirstOrDefaultAsync();

        ViewBag.WorkflowRunning = activeInstance != null && activeInstance.Status == "Running";
        ViewBag.CurrentStageName = activeInstance?.StageName;

        // طبق درخواست: تب نامه‌نگاری + تب تاریخچه‌ی واقعی گردش‌کار (از موتور، نه فقط رشته‌ی وضعیت)
        ViewBag.RelatedLetters = await _letterService.GetRelatedToDocumentAsync(id);
        ViewBag.WorkflowHistory = activeInstance != null
            ? await _runtime.GetHistoryAsync(activeInstance.WorkflowInstanceId)
            : new List<PEMS.WorkflowPlatform.Dtos.WorkflowHistoryDto>();

        return View(item);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "New VDR Item";
        ViewData["Section"] = "Engineering Management";

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Vendors = await _context.Vendors.Where(x => x.IsActive)
            .Select(x => new { x.VendorId, x.Name }).ToListAsync();

        ViewBag.Equipments = await _context.Equipments.Where(x => x.IsActive)
            .Select(x => new { x.EquipmentId, x.TagNumber }).ToListAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VDRItemDto item)
    {
        // طبق «PEMS Phase 2B — VDR/Vendor/Equipment Authorization Wiring»: ماتریس مجوز ماژول
        // "VDR" از قبل Seed شده بود (EngineeringManager/DocumentController: Create) اما هیچ‌جا
        // واقعاً بررسی نمی‌شد - این‌جا همون سیاست از‌قبل‌تعریف‌شده سیم‌کشی می‌شود، نه یک سیاست جدید
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "VDR", "Create"))
        {
            TempData["Error"] = "شما مجاز به ثبت مدرک VDR جدید نیستید.";
            return RedirectToAction(nameof(Index));
        }

        var id = await _service.CreateManualAsync(item);
        TempData["Message"] = "VDR item created";
        return RedirectToAction(nameof(Details), new { id });
    }

    // طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» بخش ۴.۳.۱: تولید شماره‌ی رسمی مدرک به
    // فرمت CONTRACTOR. هر داده‌ی مرجع (کد فروشنده/پلنت/واحد/متریال) که کاربر این‌جا وارد کند،
    // همزمان روی Vendor/Project/VDRItem ذخیره می‌شود - دفعه‌ی بعد از قبل پرشده می‌آید.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateContractorNumber(
        int vdrItemId, string? vendorCode, string? plantCode, string? unitCode, string? materialCode)
    {
        if (!await EnsurePermissionAsync("Edit", vdrItemId, "شما مجاز به ویرایش این مدرک VDR نیستید."))
            return RedirectToAction(nameof(Details), new { id = vdrItemId });

        var item = await _context.VDRItems
            .Include(x => x.Vendor)
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.DocumentId == vdrItemId);

        if (item == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(vendorCode) && item.Vendor != null) item.Vendor.VendorCode = vendorCode.Trim();
        if (!string.IsNullOrWhiteSpace(plantCode) && item.Project != null) item.Project.PlantCode = plantCode.Trim();
        if (!string.IsNullOrWhiteSpace(unitCode) && item.Project != null) item.Project.UnitCode = unitCode.Trim();
        if (!string.IsNullOrWhiteSpace(materialCode)) item.MaterialCode = materialCode.Trim();

        await _context.SaveChangesAsync();

        try
        {
            var number = await _numberingService.GenerateContractorNumberAsync(vdrItemId);

            item.DocumentNumber = number;
            item.DocumentNo = number;
            await _context.SaveChangesAsync();

            TempData["Message"] = $"شماره‌ی رسمی مدرک ساخته شد: {number}";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = vdrItemId });
    }

    // طبق بخش ۴.۳.۲ همان سند: تولید شماره‌ی رسمی مدرک به فرمت SUB-CONTRACTOR/Vendor - هم‌الگو
    // با GenerateContractorNumber بالا (هر فیلد مرجع خالی همزمان روی Project/VDR Item ذخیره می‌شود).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateVendorNumber(
        int vdrItemId, string? jobNumber, string? unitCode, string? typologyCode)
    {
        if (!await EnsurePermissionAsync("Edit", vdrItemId, "شما مجاز به ویرایش این مدرک VDR نیستید."))
            return RedirectToAction(nameof(Details), new { id = vdrItemId });

        var item = await _context.VDRItems
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.DocumentId == vdrItemId);

        if (item == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(jobNumber) && item.Project != null) item.Project.JobNumber = jobNumber.Trim();
        if (!string.IsNullOrWhiteSpace(unitCode) && item.Project != null) item.Project.UnitCode = unitCode.Trim();
        if (!string.IsNullOrWhiteSpace(typologyCode)) item.TypologyCode = typologyCode.Trim();

        await _context.SaveChangesAsync();

        try
        {
            var number = await _numberingService.GenerateVendorNumberAsync(vdrItemId);

            item.VendorFormatDocumentNumber = number;
            await _context.SaveChangesAsync();

            TempData["Message"] = $"شماره‌ی فرمت Vendor ساخته شد: {number}";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = vdrItemId });
    }

    // طبق بخش ۵.۲ همان سند: آپلود واقعی فایل روی FTP Server. طبق تصمیم کاربر بخش ایمیل حذف شده -
    // ثبت FtpUploadedDate این‌جا همان نقش تأییدیه را ایفا می‌کند.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadToFtp(int vdrItemId, int submissionId, IFormFile file)
    {
        if (!await EnsurePermissionAsync("Edit", vdrItemId, "شما مجاز به آپلود روی FTP برای این مدرک VDR نیستید."))
            return RedirectToAction(nameof(Details), new { id = vdrItemId });

        var item = await _context.VDRItems
            .Include(x => x.Project)
            .Include(x => x.Vendor)
            .FirstOrDefaultAsync(x => x.DocumentId == vdrItemId);

        var submission = await _context.VDRSubmissions.FirstOrDefaultAsync(x => x.VDRSubmissionId == submissionId);

        if (item == null || submission == null) return NotFound();

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "فایلی انتخاب نشده.";
            return RedirectToAction(nameof(Details), new { id = vdrItemId });
        }

        var remoteFolder = !string.IsNullOrWhiteSpace(item.FolderNoOnFtp)
            ? item.FolderNoOnFtp!
            : $"{item.Project?.ProjectCode}/{item.Vendor?.Name}/{item.DocumentNumber}";

        try
        {
            await using var stream = file.OpenReadStream();
            var remotePath = await _ftpService.UploadAsync(stream, file.FileName, remoteFolder);

            submission.FtpUploadedDate = DateTime.Now;
            submission.FtpRemotePath = remotePath;
            if (string.IsNullOrWhiteSpace(item.FolderNoOnFtp)) item.FolderNoOnFtp = remoteFolder;

            await _context.SaveChangesAsync();
            TempData["Message"] = $"روی FTP آپلود شد: {remotePath}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"آپلود روی FTP ناموفق بود: {ex.Message}";
        }

        return RedirectToAction(nameof(Details), new { id = vdrItemId });
    }

    // ==============================================================================
    // طبق ممیزی: قبلاً یک اکشن تخت (AddSubmission) همه‌چیز را با هم می‌گرفت - بدون RBAC،
    // بدون بازبینی موازی دیسیپلین، بدون تاریخچه‌ی واقعی. حالا دقیقاً مثل مدرک مهندسی چهار
    // مرحله‌ی مجزا از موتور عمومی گردش‌کار عبور می‌کند.
    // ==============================================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitForReview(
        int vdrItemId, int revisionNo, string vendorTransmittalNo,
        DateTime? vendorActualIssuedDate, string? purposeOfIssueCode, List<int>? disciplineIds)
    {
        if (!await EnsurePermissionAsync("Edit", vdrItemId, "شما مجاز به شروع چرخه‌ی بازبینی این مدرک VDR نیستید."))
            return RedirectToAction(nameof(Details), new { id = vdrItemId });

        try
        {
            await _workflowService.SubmitForReviewAsync(
                vdrItemId, revisionNo, vendorTransmittalNo, vendorActualIssuedDate, purposeOfIssueCode,
                disciplineIds, await GetCurrentEmployeeIdAsync(), _currentUser.Roles, GetClientIp(), GetUserAgent());

            TempData["Message"] = "چرخه‌ی بازبینی شروع شد";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = vdrItemId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisciplineRespond(int vdrItemId, string decision, string? comment)
    {
        if (!await EnsurePermissionAsync("Edit", vdrItemId, "شما مجاز به ثبت پاسخ بازبینی برای این مدرک VDR نیستید."))
            return RedirectToAction(nameof(Details), new { id = vdrItemId });

        var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
        var myEmployeeId = await GetCurrentEmployeeIdAsync();

        if (!myDisciplineId.HasValue || !myEmployeeId.HasValue)
        {
            TempData["Error"] = "کارمند مرتبط با این کاربر یا دیسیپلینش مشخص نیست.";
            return RedirectToAction(nameof(Details), new { id = vdrItemId });
        }

        try
        {
            await _workflowService.DisciplineRespondAsync(
                vdrItemId, decision, comment, myEmployeeId.Value, myDisciplineId.Value, GetClientIp(), GetUserAgent());

            TempData["Message"] = "پاسخ بازبینی شما ثبت شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = vdrItemId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LeadReview(int vdrItemId, string reviewCategory, string? clientCommentSheetNo, DateTime? clientCommentDate)
    {
        if (!await EnsurePermissionAsync("Approve", vdrItemId, "شما مجاز به ثبت تصمیم نهایی برای این مدرک VDR نیستید."))
            return RedirectToAction(nameof(Details), new { id = vdrItemId });

        try
        {
            await _workflowService.LeadReviewAsync(
                vdrItemId, reviewCategory, clientCommentSheetNo, clientCommentDate,
                await GetCurrentEmployeeIdAsync(), _currentUser.Roles, GetClientIp(), GetUserAgent());

            TempData["Message"] = "تصمیم نهایی ثبت شد";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = vdrItemId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnToVendor(int vdrItemId, string? replySheetNo, string? clientTransmittalNo)
    {
        if (!await EnsurePermissionAsync("Approve", vdrItemId, "شما مجاز به بازگرداندن این مدرک VDR به وندور نیستید."))
            return RedirectToAction(nameof(Details), new { id = vdrItemId });

        try
        {
            await _workflowService.ReturnToVendorAsync(
                vdrItemId, replySheetNo, clientTransmittalNo,
                await GetCurrentEmployeeIdAsync(), _currentUser.Roles, GetClientIp(), GetUserAgent());

            TempData["Message"] = "به وندور بازگشت داده شد و نامه‌ی رسمی ساخته شد";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = vdrItemId });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int vdrItemId, int vdrSubmissionId, string commentText, string? raisedByName,
        string? sheet, string? zone, string? reference, string? category, string? severity, string? requiredAction,
        DateTime? dueDate, IFormFile? attachment, int? pageNumber, double? pinX, double? pinY)
    {
        var attachmentId = attachment != null ? await SaveVdrFileAsync(attachment, "comments") : null;

        await _commentService.AddCommentAsync(vdrItemId, commentText, raisedByName, vdrSubmissionId,
            sheet, zone, reference, category, severity, requiredAction, dueDate, attachmentId,
            pageNumber, pinX, pinY);

        TempData["Message"] = "Comment recorded";
        return RedirectToAction(nameof(Details), new { id = vdrItemId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCommentAction(int vdrItemId, int commentId, string actionStatus)
    {
        await _commentService.SetActionStatusAsync(commentId, actionStatus);
        return RedirectToAction(nameof(Details), new { id = vdrItemId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordCommentResponse(int vdrItemId, int commentId, string? responseText, IFormFile? replyAttachment)
    {
        // طبق درخواست: کاربر نخواهد همه‌چیز را دستی تایپ کند - می‌تواند خودِ فایل پاسخ را
        // پیوست کند به‌جای/همراه با متن
        var replyAttachmentId = replyAttachment != null ? await SaveVdrFileAsync(replyAttachment, "replies") : null;

        await _commentService.RecordResponseAsync(commentId, responseText, replyAttachmentId);
        TempData["Message"] = "Response recorded";
        return RedirectToAction(nameof(Details), new { id = vdrItemId });
    }

    // طبق درخواست: پیوست‌کردن خودِ فایل Comment Sheet / Reply Sheet روی هر چرخه‌ی بازبینی -
    // قبلاً فقط شماره‌ی متنی این‌ها ثبت می‌شد، بدون امکان آپلود واقعی فایل
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadCommentSheet(int vdrItemId, int submissionId, IFormFile file)
    {
        var attachmentId = await SaveVdrFileAsync(file, "comment-sheets");

        var submission = await _context.VDRSubmissions.FirstOrDefaultAsync(x => x.VDRSubmissionId == submissionId);
        if (submission != null && attachmentId.HasValue)
        {
            submission.CommentSheetAttachmentId = attachmentId.Value;
            await _context.SaveChangesAsync();
            TempData["Message"] = "Comment sheet attached";
        }

        return RedirectToAction(nameof(Details), new { id = vdrItemId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadReplySheet(int vdrItemId, int submissionId, IFormFile file)
    {
        var attachmentId = await SaveVdrFileAsync(file, "reply-sheets");

        var submission = await _context.VDRSubmissions.FirstOrDefaultAsync(x => x.VDRSubmissionId == submissionId);
        if (submission != null && attachmentId.HasValue)
        {
            submission.ReplySheetAttachmentId = attachmentId.Value;
            await _context.SaveChangesAsync();
            TempData["Message"] = "Reply sheet attached";
        }

        return RedirectToAction(nameof(Details), new { id = vdrItemId });
    }

    // طبق «PEMS Phase 2B»: بررسی مجوز مشترک برای اکشن‌های تغییردهنده - در صورت رد، پیام دوستانه
    // + بازگشت به Details (نه Forbid خام) تا با سبک خطاهای موجود این کنترلر (TempData["Error"])
    // یکدست بماند
    private async Task<bool> EnsurePermissionAsync(string action, int vdrItemId, string friendlyMessage)
    {
        if (await _permissionService.HasPermissionAsync(_currentUser.Roles, "VDR", action))
            return true;

        TempData["Error"] = friendlyMessage;
        return false;
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;

        return await _context.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();
    }

    private async Task<int?> GetCurrentEmployeeDisciplineIdAsync()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (!employeeId.HasValue) return null;

        return await _context.Employees
            .Where(e => e.EmployeeId == employeeId.Value)
            .Select(e => (int?)e.DisciplineId)
            .FirstOrDefaultAsync();
    }

    private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetUserAgent() => Request.Headers["User-Agent"].ToString();

    private async Task<int?> SaveVdrFileAsync(IFormFile file, string subFolder)
    {
        if (file == null || file.Length == 0) return null;

        string storageKey;
        await using (var uploadStream = file.OpenReadStream())
        {
            storageKey = await _fileStorage.SaveAsync($"vdr/{subFolder}", file.FileName, uploadStream);
        }

        var attachment = new PEMS.Domain.Entities.DocumentAttachment
        {
            FileName = file.FileName,
            FileExtension = Path.GetExtension(file.FileName),
            FilePath = storageKey,
            FileSize = file.Length,
            MimeType = file.ContentType,
            UploadDate = DateTime.Now,
            IsActive = true,
            AIStatus = "NotApplicable"
        };

        await _context.DocumentAttachments.AddAsync(attachment);
        await _context.SaveChangesAsync();

        return attachment.AttachmentId;
    }
}
