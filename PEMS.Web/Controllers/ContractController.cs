using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Web.Security;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Web.Controllers;

public class ContractController : Controller
{
    private readonly IContractService _service;
    private readonly IContractWorkflowService _workflowService;
    private readonly IWorkflowRuntime _runtime;
    private readonly IAccessControlService _accessControlService;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUser;
    private readonly PEMSDbContext _context;

    public ContractController(
        IContractService service,
        IContractWorkflowService workflowService,
        IWorkflowRuntime runtime,
        IAccessControlService accessControlService,
        IPermissionService permissionService,
        ICurrentUserService currentUser,
        PEMSDbContext context)
    {
        _service = service;
        _workflowService = workflowService;
        _runtime = runtime;
        _accessControlService = accessControlService;
        _permissionService = permissionService;
        _currentUser = currentUser;
        _context = context;
    }

    // طبق «PEMS — Global Grid Hardening» P1: این Grid فیلتری نداشت؛ فقط صفحه‌بندی واقعی اضافه شد.
    public async Task<IActionResult> Index(int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Contracts";
        ViewData["Section"] = "Contract Management";
        ViewData["HelpKey"] = "Contract.Index";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var result = await _service.GetPagedAsync(page, normalizedPageSize);

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Vendors = await _context.Vendors.Where(x => x.IsActive)
            .Select(x => new { x.VendorId, x.Name }).ToListAsync();

        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "Contract Details";
        ViewData["Section"] = "Contract Management";

        var contract = await _service.GetByIdAsync(id);
        if (contract == null) return NotFound();

        ViewBag.DisciplineReviews = await _context.ContractDisciplineReviews
            .Include(x => x.Reviewer)
            .Where(x => x.ContractId == id)
            .ToListAsync();

        ViewBag.Revisions = await _context.ContractRevisions
            .Include(x => x.RequestedBy)
            .Include(x => x.ApprovedBy)
            .Where(x => x.ContractId == id)
            .OrderByDescending(x => x.RevisionNo)
            .ToListAsync();

        ViewBag.ContractDocuments = await _context.ContractDocuments
            .Where(x => x.ContractId == id)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        var instance = await _runtime.GetInstanceAsync(Persistence.Seed.ContractWorkflowSeeder.BusinessObjectType, id);
        ViewBag.WorkflowInstance = instance;
        ViewBag.WorkflowHistory = instance != null ? await _runtime.GetHistoryAsync(instance.WorkflowInstanceId) : null;

        ViewBag.RequiredDisciplines = ContractWorkflowService.RequiredDisciplines;

        return View(contract);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "New Contract";
        ViewData["Section"] = "Contract Management";

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Vendors = await _context.Vendors.Where(x => x.IsActive)
            .Select(x => new { x.VendorId, x.Name }).ToListAsync();

        return View();
    }

    // طبق §J (Test Coverage Gap): این کنترلر پیش از این هیچ بررسی مجوزی روی Create نداشت
    // (اصلاً [RequirePermission] هم نبود). بررسی این‌جا به‌صورت درون‌خطی است - دقیقاً همون الگوی
    // ProcurementPackageController - چون [RequirePermission] یک IAsyncAuthorizationFilter است و
    // فقط داخل پایپ‌لاین واقعی MVC اجرا می‌شود، نه وقتی اکشن مستقیماً در تست صدا زده می‌شود.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string contractNo, int projectId, int vendorId, string? scope,
        decimal? value, DateTime? startDate, DateTime? endDate, bool requiresInspection = false)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Contracts", "Create"))
            return Forbid();

        var id = await _service.CreateAsync(contractNo, projectId, vendorId, scope, value, startDate, endDate, requiresInspection);

        // طبق §F: این فراخوان یک ردیف Allow در AuthorizationAuditLog ثبت می‌کند تا اگر همین
        // کاربر بعداً بخواهد همین قرارداد را Approve کند، بررسی SoD در ContractWorkflowService.
        // ApproveAsync آن را پیدا کند و مسدودش کند (دقیقاً همون الگوی EngineeringDocumentsController).
        if (_currentUser.UserId.HasValue)
        {
            await _accessControlService.AuthorizeAsync(new AccessControlRequest
            {
                UserId = _currentUser.UserId.Value,
                ResourceName = "Contracts",
                ActionName = "Create",
                ResourceId = id.ToString(),
                IpAddress = GetClientIp()
            });
        }

        TempData["Message"] = "Contract created successfully";
        return RedirectToAction(nameof(Details), new { id });
    }

    // طبق §D (Phase 2.2): جایگزین رشته‌ی آزاد قدیمی - فقط همین چند گذار مشخص و اعتبارسنجی‌شده
    // مجازند؛ هر ورودی دیگر رد می‌شود. ChangeStatusAsync قدیمی (رشته‌ی آزاد) دیگر از این‌جا صدا
    // زده نمی‌شود.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int contractId, string status)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Contracts", "Edit"))
            return Forbid();

        (bool Success, string? Error) result = status switch
        {
            "UnderReview" => await SubmitForReviewInternalAsync(contractId),
            _ => (false, $"گذار مستقیم به وضعیت «{status}» مجاز نیست. از اکشن‌های Approve/Reject/Sign/MarkEffective استفاده کنید.")
        };

        if (!result.Success)
            TempData["Error"] = result.Error;
        else
            TempData["Message"] = "وضعیت قرارداد به‌روزرسانی شد.";

        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitForReview(int contractId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Contracts", "Edit"))
            return Forbid();

        var result = await SubmitForReviewInternalAsync(contractId);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "قرارداد برای بازبینی چند-رشته‌ای ارسال شد.";
        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    private async Task<(bool Success, string? Error)> SubmitForReviewInternalAsync(int contractId)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        return await _workflowService.SubmitForReviewAsync(contractId, null, employeeId, _currentUser.Roles);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordDisciplineReview(int contractId, string disciplineRole, string decision, string? comment)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Contracts", "Edit"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var result = await _workflowService.RecordDisciplineReviewAsync(
            contractId, disciplineRole, decision, comment, employeeId, _currentUser.Roles);

        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "تصمیم بازبینی ثبت شد.";

        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int contractId, string? comment)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Contracts", "Approve"))
            return Forbid();
        if (!_currentUser.UserId.HasValue) return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var result = await _workflowService.ApproveAsync(
            contractId, comment, _currentUser.UserId.Value, employeeId, _currentUser.Roles, GetClientIp());

        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "قرارداد تایید شد.";

        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int contractId, string? comment)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Contracts", "Approve"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var result = await _workflowService.RejectAsync(contractId, comment, employeeId, _currentUser.Roles);

        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "قرارداد رد شد و به Draft بازگشت.";

        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sign(int contractId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Contracts", "Approve"))
            return Forbid();
        if (!_currentUser.UserId.HasValue) return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var result = await _workflowService.SignAsync(
            contractId, _currentUser.UserId.Value, employeeId, _currentUser.Roles, GetClientIp());

        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "قرارداد امضا شد.";

        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkEffective(int contractId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Contracts", "Approve"))
            return Forbid();

        var result = await _workflowService.MarkEffectiveAsync(contractId, _currentUser.Roles);

        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "قرارداد به وضعیت Effective رسید.";

        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int contractId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Contracts", "Approve"))
            return Forbid();

        var result = await _workflowService.CompleteAsync(contractId, _currentUser.Roles);

        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "قرارداد تکمیل شد.";

        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Terminate(int contractId, string? reason)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Contracts", "Approve"))
            return Forbid();

        var result = await _workflowService.TerminateAsync(contractId, reason, _currentUser.Roles);

        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "قرارداد فسخ شد.";

        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestRevision(int contractId, string changeType, decimal? newValue, string? reason, string? changeDescription)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Contracts", "Edit"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var result = await _workflowService.RequestRevisionAsync(contractId, changeType, newValue, reason, changeDescription, employeeId);

        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "درخواست تغییر ارزش قرارداد ثبت شد (در انتظار تایید).";

        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    // طبق §D/§G (Phase 4.6): ContractDocument از قبل به‌عنوان یک نوع مدرک اسکلتی وجود داشت
    // (ToTable/EF config کامل) ولی هیچ Controller/Service ای واقعاً یک ردیف در آن نمی‌ساخت -
    // این اکشن همان اتصال گمشده است، نه یک معماری موازی جدید.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDocument(int contractId, string documentNo, string title, string documentType, string? clauseReference)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Contracts", "Edit"))
            return Forbid();

        var contract = await _context.Contracts.FirstOrDefaultAsync(x => x.ContractId == contractId);
        if (contract == null) return NotFound();

        _context.ContractDocuments.Add(new PEMS.Domain.Entities.ContractDocument
        {
            DocumentKind = "Contract",
            DocumentNo = documentNo,
            Title = title,
            DocumentType = documentType,
            ProjectId = contract.ProjectId,
            ContractId = contractId,
            ClauseReference = clauseReference,
            Status = "Draft"
        });
        await _context.SaveChangesAsync();

        TempData["Message"] = "سند قرارداد ثبت شد.";
        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRevision(int contractId, int contractRevisionId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Contracts", "Approve"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var result = await _workflowService.ApproveRevisionAsync(contractRevisionId, employeeId, _currentUser.Roles);

        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "تغییر ارزش قرارداد تایید شد.";

        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IssueNtp(int contractId, string? reference, DateTime? effectiveStartDate)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Contracts", "Approve"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var result = await _workflowService.IssueNtpAsync(contractId, reference, effectiveStartDate, employeeId);

        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "NTP صادر شد.";

        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ScheduleKickoff(int contractId, DateTime meetingDate, string? location, string? agenda)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Contracts", "Edit"))
            return Forbid();

        var result = await _workflowService.ScheduleKickoffMeetingAsync(contractId, meetingDate, location, _currentUser.FullName, agenda);

        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Message"] = "جلسه‌ی Kick-off برنامه‌ریزی شد.";

        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;

        return await _context.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();
    }

    private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
