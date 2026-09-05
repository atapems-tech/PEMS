using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق CMMS-10a Execution Baseline: Core Permit Lifecycle. مجوزدهی از طریق
/// IRoleModuleActionService (ADR-02) انجام می‌شود، نه IPermissionService موجود - چون Permit
/// عمل‌هایی فراتر از View/Create/Edit/Delete/Approve دارد (Issue/Suspend/Resume/Revoke...).
///
/// طبق CMMS-10b: گذار Submitted→UnderReview و کل زنجیره‌ی UnderReview→Approved/Rejected/Draft
/// دیگر از طریق AdvanceStatus عمومی انجام نمی‌شود - این‌جا SubmitForApproval زنجیره‌ی تایید
/// واقعی (IWorkflowRuntime) را شروع می‌کند، و RespondApproval/ReturnForRework نتیجه‌ی هر
/// مرحله را ثبت می‌کنند. بقیه‌ی گذارها (Draft→Submitted، Approved→Issued، Suspend/Resume/
/// Close/Cancel/Revoke) دقیقاً مثل CMMS-10a دست‌نخورده مانده‌اند.
/// </summary>
public class PermitController : Controller
{
    private readonly IPermitService _service;
    private readonly IRoleModuleActionService _roleModuleActionService;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFunctionalLocationService _functionalLocationService;
    private readonly IWorkflowRuntime _runtime;
    private readonly IIsolationService _isolationService;
    private readonly IGasTestService _gasTestService;
    private readonly ISimopsService _simopsService;
    private readonly IExecutionGateService _executionGateService;
    private readonly IPermitAuthorityService _permitAuthorityService;

    private const string BusinessObjectType = PEMS.Persistence.Seed.PermitApprovalWorkflowSeeder.BusinessObjectType;

    private static readonly Dictionary<string, string> NextStatusMap = new()
    {
        ["Draft"] = "Submitted",
        ["Approved"] = "Issued",
        ["Issued"] = "Active",
        ["Suspended"] = "Active",
    };

    public PermitController(IPermitService service, IRoleModuleActionService roleModuleActionService,
        PEMSDbContext context, ICurrentUserService currentUser, IFunctionalLocationService functionalLocationService,
        IWorkflowRuntime runtime, IIsolationService isolationService, IGasTestService gasTestService,
        ISimopsService simopsService, IExecutionGateService executionGateService,
        IPermitAuthorityService permitAuthorityService)
    {
        _service = service;
        _roleModuleActionService = roleModuleActionService;
        _context = context;
        _currentUser = currentUser;
        _functionalLocationService = functionalLocationService;
        _runtime = runtime;
        _isolationService = isolationService;
        _gasTestService = gasTestService;
        _simopsService = simopsService;
        _executionGateService = executionGateService;
        _permitAuthorityService = permitAuthorityService;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): قبلاً کل جدول فیلترشده‌ی-با-
    // Status با ToListAsync (بدون Skip/Take) بارگذاری می‌شد. مسیر pendingMyApproval باید همچنان
    // ابتدا کل مجموعه را با IWorkflowRuntime فیلتر کند (این فیلتر ستونی از DB نیست) و فقط بعد از
    // آن صفحه‌بندی شود - وگرنه صفحه‌بندی قبل از فیلتر واقعی رخ می‌داد.
    public async Task<IActionResult> Index(string? status, bool pendingMyApproval = false, int page = 1, int? pageSize = null)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Permit", "View"))
            return Forbid();

        ViewData["Title"] = "Permits";
        ViewData["Section"] = "CMMS Integration";

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);

        PagedResult<PermitListDto> result;

        if (pendingMyApproval)
        {
            var items = await _service.GetListAsync(status);

            // طبق PEMS-UI-04: از همون IWorkflowRuntime.GetPendingTasksAsync موجود (بدون
            // مصرف‌کننده‌ی قبلی) برای "Pending My Approval" استفاده می‌شود - نه یک موتور تطبیق
            // نقش موازی جدید.
            var employeeId = await GetCurrentEmployeeIdAsync();
            var pendingByPermitId = employeeId.HasValue
                ? (await _runtime.GetPendingTasksAsync(employeeId.Value, _currentUser.Roles))
                    .Where(t => t.BusinessObjectType == "Permit")
                    .GroupBy(t => t.BusinessObjectId)
                    .ToDictionary(g => g.Key, g => g.First())
                : new Dictionary<int, PEMS.WorkflowPlatform.Dtos.PendingTaskDto>();

            // طولانی‌ترین‌منتظرها اول - تا Approver اول به Permit ای رسیدگی کند که بیشترین زمان
            // در صف مانده.
            var filtered = items.Where(p => pendingByPermitId.ContainsKey(p.PermitId))
                .OrderBy(p => pendingByPermitId[p.PermitId].AssignedDate)
                .ThenBy(p => p.PermitId)
                .ToList();
            ViewBag.PendingTasksByPermitId = pendingByPermitId;

            var totalCount = filtered.Count;
            var pageItems = filtered.Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize).ToList();
            result = new PagedResult<PermitListDto> { Items = pageItems, Page = normalizedPage, PageSize = normalizedPageSize, TotalCount = totalCount };
        }
        else
        {
            result = await _service.GetPagedListAsync(status, normalizedPage, normalizedPageSize);
        }

        ViewBag.SelectedStatus = status;
        ViewBag.PendingMyApproval = pendingMyApproval;

        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Permit", "Create"))
            return Forbid();

        ViewData["Title"] = "New Permit";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.PermitTypes = PEMS.Persistence.Services.PermitService.ValidPermitTypes;
        ViewBag.FunctionalLocations = await _functionalLocationService.GetPickerListAsync();

        // پرسنل دریافت‌کننده‌ی Permit می‌تواند Internal یا Contractor باشد - طبق ADR-01
        ViewBag.Receivers = await _context.Employees.Where(x => x.IsActive)
            .OrderBy(x => x.FirstName)
            .Select(x => new { x.EmployeeId, Label = x.FirstName + " " + x.LastName + " (" + x.PersonnelType + ")" })
            .ToListAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string permitType, string title, string? description, int? equipmentId,
        int? functionalLocationId, int? workOrderId, int? permitReceiverEmployeeId,
        string priority, DateTime? validFrom, DateTime? validTo)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Permit", "Create"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        if (!employeeId.HasValue)
        {
            TempData["Error"] = "کارمند مرتبط با این کاربر پیدا نشد.";
            return RedirectToAction(nameof(Create));
        }

        var (success, error, id) = await _service.CreateAsync(
            permitType, title, description, employeeId.Value, equipmentId, functionalLocationId,
            workOrderId, permitReceiverEmployeeId, priority, validFrom, validTo);

        if (!success)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Create));
        }

        TempData["Message"] = "Permit ساخته شد.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Permit", "View"))
            return Forbid();

        var permit = await _service.GetDetailsAsync(id);
        if (permit == null) return NotFound();

        ViewData["Title"] = "Permit Details";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.NextStatus = NextStatusMap.TryGetValue(permit.Status, out var next) ? next : null;

        // طبق CMMS-10b: نمایش پیشرفت زنجیره‌ی تایید واقعی (اگر شروع شده باشد)
        var instance = await _runtime.GetInstanceAsync(BusinessObjectType, id);
        ViewBag.ApprovalInstance = instance;
        ViewBag.ApprovalHistory = instance != null ? await _runtime.GetHistoryAsync(instance.WorkflowInstanceId) : null;
        ViewBag.CurrentUserRoles = _currentUser.Roles;

        // طبق CMMS-10c: نمایش نقاط ایزوله‌سازی این Permit
        ViewBag.IsolationPoints = await _isolationService.GetForPermitAsync(id);

        // طبق CMMS-10d: نمایش تست‌های گاز این Permit
        ViewBag.GasTests = await _gasTestService.GetForPermitAsync(id);

        // طبق CMMS-10e: نمایش Candidate Overlap و تعارض‌های ثبت‌شده‌ی SIMOPS این Permit
        ViewBag.SimopsCandidates = await _simopsService.GetCandidatesForPermitAsync(id);
        ViewBag.SimopsCandidateWorkOrders = await _simopsService.GetCandidateWorkOrdersForPermitAsync(id);
        ViewBag.SimopsConflicts = await _simopsService.GetConflictsForPermitAsync(id);

        // طبق CMMS-10e: نمایش Backlink اطلاعاتی به هر تحویل شیفتی که این Permit در آن Snapshot شده
        ViewBag.HandoverBacklinks = await _context.ShiftHandoverItems
            .Include(x => x.ShiftHandover)
            .Where(x => x.ReferenceType == "Permit" && x.ReferenceId == id)
            .Select(x => new { x.ShiftHandoverId, x.ShiftHandover!.Status, x.ShiftHandover.PreparedDate })
            .Distinct()
            .ToListAsync();

        // طبق CMMS-10f: ارزیابی Execution Gate فقط وقتی Status مربوطه است محاسبه می‌شود
        if (permit.Status is "Issued" or "Suspended")
        {
            ViewBag.ExecutionGate = await _executionGateService.EvaluateAsync(id);
        }
        ViewBag.ExecutionGateDecisions = await _executionGateService.GetDecisionsForPermitAsync(id);
        ViewBag.CanEmergencyOverride = await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "ExecutionGate", "EmergencyOverride");

        // طبق PEMS-UI-01 (UI01-002): موتور Authority/Competency (PEMS-SEC-PERMIT-AUTH-001) از قبل
        // برای هر اقدام حساس محاسبه می‌شود، اما فقط Execution Gate آن را قبل از کلیک نمایش
        // می‌داد. این‌جا همون EvaluateApproverEligibilityAsync موجود - بدون هیچ تغییری در خودش -
        // فقط برای نمایش، و فقط وقتی دکمه‌ی مربوطه واقعاً رندر می‌شود، صدا زده می‌شود.
        var userId = _currentUser.UserId ?? 0;
        var employeeIdForEligibility = await GetCurrentEmployeeIdAsync();
        var rolesList = _currentUser.Roles.ToList();

        if (instance is { Status: "Running" } && instance.PendingStages.Any(p =>
                p.AssignedToRole != null && p.AssignedToRole.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(rolesList.Contains)))
        {
            ViewBag.ApproveEligibility = await _permitAuthorityService.EvaluateApproverEligibilityAsync(userId, employeeIdForEligibility, rolesList, id, "Permit", "Approve");
            ViewBag.RejectEligibility = await _permitAuthorityService.EvaluateApproverEligibilityAsync(userId, employeeIdForEligibility, rolesList, id, "Permit", "Reject");
        }

        if (permit.Status == "Active")
        {
            ViewBag.CloseEligibility = await _permitAuthorityService.EvaluateApproverEligibilityAsync(userId, employeeIdForEligibility, rolesList, id, "Permit", "Close");
        }

        var isolationPointsForEligibility = (List<PEMS.Application.DTOs.IsolationPointListDto>)ViewBag.IsolationPoints;
        if (isolationPointsForEligibility.Any(p => p.Status == "Isolated"))
        {
            ViewBag.LotoVerifyEligibility = await _permitAuthorityService.EvaluateApproverEligibilityAsync(userId, employeeIdForEligibility, rolesList, id, "LOTO", "Verify");
        }

        var simopsConflictsForEligibility = (List<PEMS.Application.DTOs.SimopsConflictListDto>)ViewBag.SimopsConflicts;
        if (simopsConflictsForEligibility.Any(c => c.Status == "Potential"))
        {
            ViewBag.SimopsConfirmEligibility = await _permitAuthorityService.EvaluateApproverEligibilityAsync(userId, employeeIdForEligibility, rolesList, id, "Simops", "Confirm");
        }
        if (simopsConflictsForEligibility.Any(c => c.Status == "Confirmed"))
        {
            ViewBag.SimopsResolveEligibility = await _permitAuthorityService.EvaluateApproverEligibilityAsync(userId, employeeIdForEligibility, rolesList, id, "Simops", "Resolve");
        }
        if (simopsConflictsForEligibility.Any(c => c.Status is "Potential" or "Confirmed"))
        {
            ViewBag.SimopsAcceptRiskEligibility = await _permitAuthorityService.EvaluateApproverEligibilityAsync(userId, employeeIdForEligibility, rolesList, id, "Simops", "AcceptRisk");
        }

        return View(permit);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdvanceStatus(int id, string? reason)
    {
        var permit = await _service.GetDetailsAsync(id);
        if (permit == null) return NotFound();

        if (!NextStatusMap.TryGetValue(permit.Status, out var next))
        {
            TempData["Error"] = "این Permit در وضعیت نهایی است.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // طبق CMMS-10f: Issued/Suspended→Active دیگر از این مسیر بدون‌گیت عبور نمی‌کند - باید از
        // ExecutionGateController.StartWork/EmergencyOverride انجام شود (بند ۷ دستورالعمل: عدم
        // اتکا به مخفی‌کردن دکمه در UI، رد سمت سرور).
        if (next == "Active")
        {
            TempData["Error"] = "گذار به Active باید از طریق Execution Gate (Start Work) انجام شود.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // طبق CMMS-10b: UnderReview→Approved دیگر از این‌جا عبور نمی‌کند (SubmitForApproval/
        // RespondApproval را ببین) - این سوییچ فقط برای Draft→Submitted و Approved→Issued
        // باقی مانده.
        var requiredAction = next switch
        {
            "Issued" => "Issue",
            _ => "Submit"
        };

        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Permit", requiredAction))
            return Forbid();

        var (success, error) = await _service.AdvanceStatusAsync(id, next, reason, await GetCurrentEmployeeIdAsync());

        TempData[success ? "Message" : "Error"] = success ? $"وضعیت به {next} تغییر کرد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    // ==============================================================================
    // طبق CMMS-10b: شروع زنجیره‌ی تایید واقعی. Submitted→UnderReview هم همین‌جا انجام می‌شود
    // (PermitService.SubmitForApprovalAsync) - این جایگزین دکمه‌ی عمومی «Advance to
    // UnderReview» است.
    // ==============================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitForApproval(int id)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Permit", "Submit"))
            return Forbid();

        var (success, error) = await _service.SubmitForApprovalAsync(id, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "زنجیره‌ی تایید شروع شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    // طبق CMMS-10b بند ۶: مجوز دو لایه - RoleModuleAction (لایه‌ی درشت: آیا این نقش اصلاً
    // مجاز به Approve/Reject روی Permit است) + IWorkflowRuntime.ExecuteActionAsync (لایه‌ی
    // ریز: آیا این نقش دقیقاً مسئول همین مرحله‌ی جاری است) - هر دو باید عبور کنند.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RespondApproval(int id, string decision, string? comment, string? webAuthnAssertionJson, string? webAuthnOptionsJson)
    {
        var requiredAction = decision == "Approve" ? "Approve" : "Reject";
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Permit", requiredAction))
            return Forbid();

        // طبق سند PEMS-SEC-PERMIT-AUTH-001: تایید/رد Permit جزو اقدامات حساسِ نیازمند Step-up
        // Authentication است - این بررسی علاوه بر گیت درشت RoleModuleAction بالا انجام می‌شود.
        var employeeId = await GetCurrentEmployeeIdAsync();
        var (authOk, authError) = await _permitAuthorityService.AuthorizeSensitiveActionAsync(
            _currentUser.UserId ?? 0, employeeId, _currentUser.Roles.ToList(), id, "Permit", requiredAction, "Approval",
            webAuthnAssertionJson, webAuthnOptionsJson);
        if (!authOk)
        {
            TempData["Error"] = authError;
            TempData["ErrorArea"] = "Approval";
            return RedirectToAction(nameof(Details), new { id });
        }

        var (success, error) = await _service.RespondApprovalAsync(
            id, decision, comment, employeeId, _currentUser.Roles,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers["User-Agent"].ToString());

        TempData[success ? "Message" : "Error"] = success ? "نظر شما ثبت شد." : error;
        if (!success) TempData["ErrorArea"] = "Approval";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnForRework(int id, string? comment)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Permit", "Reject"))
            return Forbid();

        var (success, error) = await _service.ReturnForReworkAsync(
            id, comment, await GetCurrentEmployeeIdAsync(), _currentUser.Roles,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers["User-Agent"].ToString());

        TempData[success ? "Message" : "Error"] = success ? "Permit برای اصلاح بازگردانده شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Permit", "Cancel"))
            return Forbid();

        var (success, error) = await _service.AdvanceStatusAsync(id, "Cancelled", reason, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "Permit لغو شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int id, string? reason)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Permit", "Revoke"))
            return Forbid();

        var (success, error) = await _service.AdvanceStatusAsync(id, "Revoked", reason, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "Permit ابطال شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(int id, string? reason)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Permit", "Suspend"))
            return Forbid();

        var (success, error) = await _service.AdvanceStatusAsync(id, "Suspended", reason, await GetCurrentEmployeeIdAsync());
        TempData[success ? "Message" : "Error"] = success ? "Permit معلق شد." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id, string? reason, string? webAuthnAssertionJson, string? webAuthnOptionsJson)
    {
        if (!await _roleModuleActionService.HasActionAsync(_currentUser.Roles, "Permit", "Close"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var (authOk, authError) = await _permitAuthorityService.AuthorizeSensitiveActionAsync(
            _currentUser.UserId ?? 0, employeeId, _currentUser.Roles.ToList(), id, "Permit", "Close", null,
            webAuthnAssertionJson, webAuthnOptionsJson);
        if (!authOk)
        {
            TempData["Error"] = authError;
            TempData["ErrorArea"] = "Actions";
            return RedirectToAction(nameof(Details), new { id });
        }

        var (success, error) = await _service.AdvanceStatusAsync(id, "Closed", reason, employeeId);
        TempData[success ? "Message" : "Error"] = success ? "Permit بسته شد." : error;
        if (!success) TempData["ErrorArea"] = "Actions";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }
}
