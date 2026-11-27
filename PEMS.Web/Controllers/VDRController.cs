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
    private readonly IDocumentHoldService? _holdService;   // SCRUM-559
    private readonly Microsoft.Extensions.Localization.IStringLocalizer<PEMS.Web.Resources.SharedResource>? _localizer;

    public VDRController(IVDRService service, IDocumentCommentService commentService,
        IVdrWorkflowService workflowService, ILetterService letterService, IWorkflowRuntime runtime,
        PEMSDbContext context, ICurrentUserService currentUser, IVdrDocumentNumberingService numberingService,
        IFtpUploadService ftpService, IFileStorageService fileStorage, IPermissionService permissionService,
        IDocumentHoldService? holdService = null, Microsoft.Extensions.Localization.IStringLocalizer<PEMS.Web.Resources.SharedResource>? localizer = null)
    {
        _holdService = holdService;
        _localizer = localizer;
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
        // SCRUM-559: the More actions menu shows each item only with its permission
        ViewBag.CanEditItem = await _permissionService.HasPermissionAsync(_currentUser.Roles, "VDR", "Edit");
        ViewBag.CanHold = await _permissionService.HasPermissionAsync(_currentUser.Roles, "VDR", "Hold");
        ViewBag.CanReleaseHold = await _permissionService.HasPermissionAsync(_currentUser.Roles, "VDR", "ReleaseHold");
        ViewBag.CanDeactivate = await _permissionService.HasPermissionAsync(_currentUser.Roles, "VDR", "Delete");
        ViewBag.WorkflowHistory = activeInstance != null
            ? await _runtime.GetHistoryAsync(activeInstance.WorkflowInstanceId)
            : new List<PEMS.WorkflowPlatform.Dtos.WorkflowHistoryDto>();

        return View(item);
    }

    // ==============================================================================
    // طبق ممیزی: قبلاً یک اکشن تخت (AddSubmission) همه‌چیز را با هم می‌گرفت - بدون RBAC،
    // بدون بازبینی موازی دیسیپلین، بدون تاریخچه‌ی واقعی. حالا دقیقاً مثل مدرک مهندسی چهار
    // مرحله‌ی مجزا از موتور عمومی گردش‌کار عبور می‌کند.
    // ==============================================================================


    // ================================================================== SCRUM-559: More actions menu

    private string T(string key)
    {
        var l = _localizer?[key];
        return l == null || l.ResourceNotFound ? key : l.Value;
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
