using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق درخواست: تکمیل بخش‌های CMMS - Work Order قلب واقعی CMMS است (کار نگهداری واقعی که
/// انجام می‌شود). گردش‌کار: Open → Assigned → InProgress → Completed → Closed. طبق درخواست
/// بازبینی: یک WorkOrder می‌تواند نیاز به ثبت نظر چند دیسیپلین تعمیراتی داشته باشد
/// (Multi-Craft Coordination، EN 13460) قبل از این‌که Completed بشه.
/// </summary>
public class WorkOrderController : Controller
{
    private static readonly string[] StatusFlow = { "Open", "Assigned", "InProgress", "Completed", "Closed" };

    // Canonical CMMS values. These values must match the Create/Edit UI and WorkOrder domain contract.
    private static readonly string[] ValidWorkOrderTypes = { "Preventive", "Corrective", "Emergency", "Predictive" };
    private static readonly string[] ValidPriorities = { "Low", "Medium", "High", "Critical" };

    private readonly PEMSDbContext _context;
    private readonly IWorkOrderReviewService _reviewService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly IWorkOrderLaborService _laborService;
    private readonly IWorkOrderLifecycleService _lifecycleService;
    private readonly ICrewService _crewService;
    private readonly INumberingService _numberingService;
    private readonly IMaterialStockService _materialStockService;

    public WorkOrderController(PEMSDbContext context, IWorkOrderReviewService reviewService,
        ICurrentUserService currentUser, IPermissionService permissionService, IWorkOrderLaborService laborService,
        IWorkOrderLifecycleService lifecycleService, ICrewService crewService, INumberingService numberingService,
        IMaterialStockService materialStockService)
    {
        _context = context;
        _reviewService = reviewService;
        _currentUser = currentUser;
        _permissionService = permissionService;
        _laborService = laborService;
        _lifecycleService = lifecycleService;
        _crewService = crewService;
        _numberingService = numberingService;
        _materialStockService = materialStockService;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): قبلاً کل جدول فیلترشده
    // (با ۳ Include) با ToListAsync (بدون Skip/Take) بارگذاری می‌شد؛ شمارنده‌های Dashboard هم
    // با یک بارگذاری کامل دومِ کل جدول (allActive) محاسبه می‌شدند. حالا Grid با SQL واقعی
    // صفحه‌بندی می‌شود و شمارنده‌ها CountAsync هستند - با همون Scope سراسری قبلی (مستقل از
    // فیلتر status/equipmentId/assignedToMe).
    public async Task<IActionResult> Index(string? status, int? equipmentId, bool assignedToMe = false, int page = 1, int? pageSize = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "View"))
            return Forbid();

        ViewData["Title"] = "Work Orders";
        ViewData["HelpKey"] = "WorkOrders.Index";
        ViewData["Section"] = "CMMS Integration";

        var query = _context.WorkOrders.Where(x => x.IsActive).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (equipmentId.HasValue) query = query.Where(x => x.EquipmentId == equipmentId.Value);

        // طبق PEMS-UI-04: از همون رابطه‌ی موجود و قابل‌اتکای AssignedEmployeeId استفاده می‌شود -
        // نه یک معماری انتساب جدید.
        if (assignedToMe)
        {
            var employeeId = await GetCurrentEmployeeIdAsync();
            query = query.Where(x => x.AssignedEmployeeId == employeeId);
        }

        var totalCount = await query.CountAsync();

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var normalizedPage = page < 1 ? 1 : page;

        // طبق A-3: سه Include قبلی (Equipment/AssignedTeam/AssignedEmployee) حذف شدند - فقط
        // یک فیلد اسکالر از هرکدام لازم بود که مستقیم در Select قابل‌دریافت است.
        var workOrders = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.WorkOrderId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new WorkOrderListDto
            {
                WorkOrderId = x.WorkOrderId,
                WorkOrderNumber = x.WorkOrderNumber,
                EquipmentId = x.EquipmentId,
                EquipmentTagNumber = x.Equipment != null ? x.Equipment.TagNumber : null,
                WorkOrderType = x.WorkOrderType,
                Priority = x.Priority,
                AssignedTeamName = x.AssignedTeam != null ? x.AssignedTeam.Name : null,
                AssignedEmployeeName = x.AssignedEmployee != null ? x.AssignedEmployee.FirstName + " " + x.AssignedEmployee.LastName : null,
                AssignedTo = x.AssignedTo,
                Status = x.Status,
                CreatedDate = x.CreatedDate
            })
            .ToListAsync();

        ViewBag.Equipments = await _context.Equipments.Where(x => x.IsActive)
            .Select(x => new { x.EquipmentId, x.TagNumber, x.Name }).ToListAsync();

        ViewBag.SelectedStatus = status;
        ViewBag.SelectedEquipmentId = equipmentId;

        if (equipmentId.HasValue)
        {
            ViewBag.SelectedEquipmentLabel = await _context.Equipments
                .Where(x => x.EquipmentId == equipmentId.Value)
                .Select(x => x.TagNumber + " - " + x.Name)
                .FirstOrDefaultAsync();
        }

        var activeQuery = _context.WorkOrders.Where(x => x.IsActive);
        ViewBag.OpenCount = await activeQuery.CountAsync(x => x.Status == "Open");
        ViewBag.InProgressCount = await activeQuery.CountAsync(x => x.Status == "InProgress" || x.Status == "Assigned");
        ViewBag.CompletedCount = await activeQuery.CountAsync(x => x.Status == "Completed" || x.Status == "Closed");
        ViewBag.CriticalOpenCount = await activeQuery.CountAsync(x => x.Priority == "Critical" && x.Status != "Completed" && x.Status != "Closed");
        ViewBag.AssignedToMe = assignedToMe;

        return View(new PagedResult<WorkOrderListDto>
        {
            Items = workOrders,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? equipmentId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Create"))
            return Forbid();

        ViewData["Title"] = "New Work Order";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.PreselectedEquipmentId = equipmentId;

        if (equipmentId.HasValue)
        {
            ViewBag.PreselectedEquipmentLabel = await _context.Equipments
                .Where(x => x.EquipmentId == equipmentId.Value)
                .Select(x => x.TagNumber + " - " + x.Name)
                .FirstOrDefaultAsync();
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int equipmentId, string workOrderType, string priority,
        string? description)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Create"))
            return Forbid();

        // Server-side validation must use exactly the same canonical values exposed by the UI.
        if (equipmentId <= 0 || !await _context.Equipments.AnyAsync(x => x.EquipmentId == equipmentId && x.IsActive))
        {
            TempData["Error"] = "تجهیز انتخاب‌شده نامعتبره.";
            return RedirectToAction(nameof(Create));
        }

        workOrderType = workOrderType?.Trim() ?? string.Empty;
        priority = priority?.Trim() ?? string.Empty;

        if (!ValidWorkOrderTypes.Contains(workOrderType, StringComparer.Ordinal))
        {
            TempData["Error"] = "نوع WorkOrder نامعتبره.";
            return RedirectToAction(nameof(Create));
        }

        if (!ValidPriorities.Contains(priority, StringComparer.Ordinal))
        {
            TempData["Error"] = "اولویت نامعتبره.";
            return RedirectToAction(nameof(Create));
        }

        // Phase 3B: Work Order جدید همیشه حاکمیتی است (Snapshot دیسیپلین/دامنه‌ی مبدأ + Instance چرخه‌عمر با Binding صریح، یک تراکنش).
        // شماره‌گذاری از NumberingService (اتمیک، P0-04).
        var workOrderNumber = await _numberingService.GetNextNumberAsync("WorkOrder");
        var (created, createError, newId) = await _lifecycleService.CreateGovernedAsync(new WorkOrderCreateRequest
        {
            WorkOrderNumber = workOrderNumber, EquipmentId = equipmentId, WorkOrderType = workOrderType, Priority = priority, Description = description
        }, await BuildActorAsync());

        if (!created)
        {
            TempData["Error"] = createError;
            return RedirectToAction(nameof(Create));
        }

        TempData["Message"] = L($"Work Order {workOrderNumber} ایجاد شد", $"Work order {workOrderNumber} created");
        return RedirectToAction(nameof(Details), new { id = newId });
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "View"))
            return Forbid();

        ViewData["Title"] = "Work Order Details";
        ViewData["Section"] = "CMMS Integration";
        ViewData["HelpKey"] = "WorkOrders.Details";

        var workOrder = await _context.WorkOrders
            .Include(x => x.Equipment)
            .Include(x => x.MaintenancePlan)
            .Include(x => x.AssignedTeam)
            .Include(x => x.AssignedEmployee)
            .FirstOrDefaultAsync(x => x.WorkOrderId == id);

        if (workOrder == null) return NotFound();

        // طبق قاعده‌ی دیسیپلین در AssignExecutorAsync/AddLaborEntryAsync (Phase 6D): وقتی دیسیپلین مسئول
        // مشخص است، فقط تیم/کارمند همان دیسیپلین نمایش داده می‌شود تا انتخابگر با قاعده‌ی واقعی سرور
        // هم‌خوان باشد (پیش از این هر تیم/کارمند فعالی نمایش داده می‌شد و فقط بعد از Submit رد می‌شد).
        var teamsQuery = _context.Teams.Where(x => x.IsActive);
        var employeesQuery = _context.Employees.Where(x => x.IsActive);
        if (workOrder.ResponsibleDisciplineId.HasValue)
        {
            teamsQuery = teamsQuery.Where(x => x.MaintenanceGroup!.DisciplineId == workOrder.ResponsibleDisciplineId);
            employeesQuery = employeesQuery.Where(x => x.DisciplineId == workOrder.ResponsibleDisciplineId);
        }
        ViewBag.Teams = await teamsQuery.Select(x => new { x.TeamId, x.Name }).ToListAsync();
        ViewBag.SparesUsed = await _context.MaintenanceSpareUsages.Include(x => x.MaterialItem)
            .Where(x => x.WorkOrderId == id && x.IsActive).ToListAsync();
        ViewBag.MaterialItems = await _context.MaterialItems.Where(x => x.IsActive && x.IsStockItem)
            .Select(x => new { x.MaterialItemId, x.MaterialCode, x.Description }).ToListAsync();
        // Phase 3B: ساختار گردش‌کار/اقدام‌ها/شرکت‌کنندگان/تاریخچه از مدل خواندنی مرکزی؛ فقط Work Order Legacy از مسیر قدیمی نمایش داده می‌شود
        var actor = await BuildActorAsync();
        ViewBag.Workflow = await _lifecycleService.GetWorkflowViewAsync(id, actor);
        ViewBag.IsLegacy = ViewBag.Workflow == null;
        var historyUserIds = ((PEMS.WorkflowPlatform.Interfaces.WorkflowRuntimeViewModel?)ViewBag.Workflow)?.HistorySummary.Where(h => h.UserId.HasValue).Select(h => h.UserId!.Value).Distinct().ToList() ?? new List<int>();
        ViewBag.EmployeeNames = historyUserIds.Count == 0 ? new Dictionary<int, string>()
            : await _context.Employees.Where(e => historyUserIds.Contains(e.EmployeeId)).ToDictionaryAsync(e => e.EmployeeId, e => e.FirstName + " " + e.LastName);
        ViewBag.OriginatingDisciplineName = workOrder.OriginatingDisciplineId.HasValue
            ? await _context.Disciplines.Where(d => d.DisciplineId == workOrder.OriginatingDisciplineId).Select(d => d.Name).FirstOrDefaultAsync() : null;
        ViewBag.ResponsibleDisciplineName = workOrder.ResponsibleDisciplineId.HasValue
            ? await _context.Disciplines.Where(d => d.DisciplineId == workOrder.ResponsibleDisciplineId).Select(d => d.Name).FirstOrDefaultAsync() : null;
        ViewBag.NextStatus = ViewBag.IsLegacy ? GetNextStatus(workOrder.Status) : null;
        ViewBag.LaborEntries = await _context.WorkOrderLabors.Include(x => x.Employee).Include(x => x.Team)
            .Where(x => x.WorkOrderId == id && x.IsActive).OrderByDescending(x => x.WorkDate).ToListAsync();
        ViewBag.TotalLaborCost = await _laborService.GetTotalLaborCostAsync(id);
        ViewBag.SpareCost = (await _context.MaintenanceSpareUsages.Where(x => x.WorkOrderId == id && x.IsActive)
            .ToListAsync()).Sum(x => x.TotalCost ?? 0);
        ViewBag.Employees = await employeesQuery
            .Select(x => new { x.EmployeeId, FullName = x.FirstName + " " + x.LastName }).ToListAsync();
        ViewBag.ReviewStatus = await _reviewService.GetDisciplineReviewStatusAsync(id);
        ViewBag.HasPendingReview = await _reviewService.HasPendingDisciplineReviewAsync(id);
        ViewBag.MyDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();
        ViewBag.Findings = await _context.WorkOrderDisciplineFindings.Include(x => x.Discipline)
            .Where(x => x.WorkOrderId == id).OrderByDescending(x => x.RespondedDate).ToListAsync();
        ViewBag.StatusHistory = await _context.WorkOrderStatusHistories.Include(x => x.ChangedBy)
            .Where(x => x.WorkOrderId == id).OrderByDescending(x => x.ChangedDate).ToListAsync();
        ViewBag.RequiredSkills = await _context.RequiredSkills.Include(x => x.Skill)
            .Where(x => x.WorkOrderId == id).ToListAsync();
        ViewBag.AllSkills = await _context.Skills.Where(x => x.IsActive)
            .OrderBy(x => x.Name).Select(x => new { x.SkillId, x.Name }).ToListAsync();
        ViewBag.Crews = await _context.Crews.Include(x => x.Members).ThenInclude(m => m.Employee)
            .Where(x => x.WorkOrderId == id && x.IsActive).ToListAsync();

        ViewBag.RequiredMaterials = await _context.WorkOrderMaterials.Include(x => x.MaterialItem)
            .Where(x => x.WorkOrderId == id).ToListAsync();

        // طبق CMMS Production Baseline v1 بند ۱۰.۴: چک‌لیست اجرای واقعی - وقتی این WorkOrder از
        // یک Plan دارای Job Plan ساخته شده باشد
        ViewBag.Tasks = await _context.WorkOrderTasks
            .Where(x => x.WorkOrderId == id).OrderBy(x => x.TaskNumber).ToListAsync();

        return View(workOrder);
    }

    // طبق ممیزی مستقل فاز ۶B Wave A (P0): CompleteTask/ReopenTask هیچ بررسی مجوزی نداشتند -
    // مثل بقیه‌ی جهش‌های این کنترلر (Hold/Resume/AddLaborEntry/...) نیازمند "WorkOrder"/"Edit"
    // هستند، نه یک مجوز جدید.
    // طبق ممیزی مستقل (P1-01): قبلاً فقط با WorkOrderTaskId پیدا می‌شد - بدون اثبات اینکه واقعاً
    // به workOrderId درخواستی تعلق دارد (امکان دستکاری Task یک WorkOrder دیگر با فرستادن
    // workOrderId دلخواه در فرم). حالا هر دو شناسه با هم Query می‌شوند.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteTask(int workOrderTaskId, int workOrderId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Edit"))
            return Forbid();

        var task = await GetOwnedWorkOrderTaskAsync(workOrderTaskId, workOrderId);
        if (task == null) return NotFound();

        task.IsCompleted = true;
        task.CompletedBy = _currentUser.UserName;
        task.CompletedDate = DateTime.Now;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = workOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReopenTask(int workOrderTaskId, int workOrderId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Edit"))
            return Forbid();

        var task = await GetOwnedWorkOrderTaskAsync(workOrderTaskId, workOrderId);
        if (task == null) return NotFound();

        task.IsCompleted = false;
        task.CompletedBy = null;
        task.CompletedDate = null;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = workOrderId });
    }

    // طبق ممیزی مستقل (P1-01): هر دو شناسه با هم Query می‌شوند - نه بارگذاری با TaskId و بررسی
    // بعدی - تا Task یک WorkOrder دیگر هرگز حتی برای خواندن هم بازگردانده نشود.
    private Task<WorkOrderTask?> GetOwnedWorkOrderTaskAsync(int workOrderTaskId, int workOrderId) =>
        _context.WorkOrderTasks.FirstOrDefaultAsync(x => x.WorkOrderTaskId == workOrderTaskId && x.WorkOrderId == workOrderId);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdvanceStatus(int id, int? assignedTeamId, int? assignedEmployeeId,
        string? failureModeCode, string? failureCauseCode, string? rootCause, decimal? downtimeHours, string? reason,
        string? failureEffect, string? correctiveAction, decimal? contractorCost, string? contractorName)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Edit")) return Forbid();
        var workOrder = await _context.WorkOrders.FirstOrDefaultAsync(x => x.WorkOrderId == id);
        if (workOrder == null) return NotFound();
        var next = GetNextStatus(workOrder.Status);
        if (next == null) { TempData["Error"] = "This work order is already at its final status"; return RedirectToAction(nameof(Details), new { id }); }
        if (next == "Closed" && !await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Approve"))
        { TempData["Error"] = "برای بستن نهایی WorkOrder مجوز کافی نداری."; return RedirectToAction(nameof(Details), new { id }); }
        if (next == "Completed" && await _reviewService.HasPendingDisciplineReviewAsync(id))
        { TempData["Error"] = "این WorkOrder یک بازبینی چند-دیسیپلینه‌ی باز داره - قبل از تکمیل، همه‌ی دیسیپلین‌های ارجاع‌شده باید نظرشون رو ثبت کنن."; return RedirectToAction(nameof(Details), new { id }); }

        var (success, error) = await _lifecycleService.AdvanceStatusAsync(
            id, next, reason, await GetCurrentEmployeeIdAsync(), assignedTeamId, assignedEmployeeId,
            failureModeCode, failureCauseCode, rootCause, downtimeHours, failureEffect, correctiveAction,
            contractorCost, contractorName);

        if (!success) { TempData["Error"] = error; return RedirectToAction(nameof(Details), new { id }); }

        TempData["Message"] = $"Work order advanced to {next}";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ==============================================================================
    // طبق CMMS Production Baseline v1 بند ۹.۲ ("optional controlled states"): مسیرهای انشعابی
    // که قبلاً اصلاً در دسترس نبودند - AdvanceStatus فقط مسیر خطی رو به جلو رو پوشش می‌ده.
    // اعتبارسنجی واقعی گذار مجاز/غیرمجاز داخل WorkOrderLifecycleService انجام می‌شه.
    // ==============================================================================

    /// <summary>
    /// اجرای اکشن گردش‌کار (مرحله‌ی جاری از مدل مرکزی). actionType از کاربر می‌آید ولی «مجاز بودن» را سرور (Runtime + هوک Work Order:
    /// مجوز، سازمان، دیسیپلین، SoD) تعیین می‌کند؛ دکمه‌ی مخفی/غیرفعال هیچ‌گاه مرجع امنیت نیست.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WorkflowAction(int id, string actionType, string? comment,
        string? failureModeCode, string? failureCauseCode, string? rootCause, decimal? downtimeHours,
        string? failureEffect, string? correctiveAction, decimal? contractorCost, string? contractorName)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "View")) return Forbid();

        var (success, error) = await _lifecycleService.ExecuteWorkflowActionAsync(id, actionType, comment, await BuildActorAsync(),
            new WorkOrderCompletionData
            {
                FailureModeCode = failureModeCode, FailureCauseCode = failureCauseCode, RootCause = rootCause, DowntimeHours = downtimeHours,
                FailureEffect = failureEffect, CorrectiveAction = correctiveAction, ContractorCost = contractorCost, ContractorName = contractorName
            });
        TempData[success ? "Message" : "Error"] = success ? L("اقدام با موفقیت انجام شد.", "The action was completed.") : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPlanning(int id, int responsibleDisciplineId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "View")) return Forbid();
        var (success, error) = await _lifecycleService.SetPlanningAsync(id, responsibleDisciplineId, await BuildActorAsync());
        TempData[success ? "Message" : "Error"] = success ? L("دیسیپلین مسئول نگهداری ثبت شد.", "Responsible maintenance discipline saved.") : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignExecutor(int id, int? teamId, int? employeeId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "View")) return Forbid();
        var (success, error) = await _lifecycleService.AssignExecutorAsync(id, teamId, employeeId, await BuildActorAsync());
        TempData[success ? "Message" : "Error"] = success ? L("مجری تخصیص داده شد.", "Executor assigned.") : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetSchedule(int id, DateTime plannedStart, DateTime plannedFinish)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "View")) return Forbid();
        var (success, error) = await _lifecycleService.ScheduleWorkOrderAsync(id, plannedStart, plannedFinish, await BuildActorAsync());
        TempData[success ? "Message" : "Error"] = success ? L("زمان‌بندی ثبت شد.", "Schedule saved.") : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hold(int id, string? reason)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Edit")) return Forbid();

        var (success, error) = await _lifecycleService.AdvanceStatusAsync(
            id, "OnHold", reason, await GetCurrentEmployeeIdAsync(), null, null, null, null, null, null);

        TempData[success ? "Message" : "Error"] = success ? "Work order put on hold" : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resume(int id, string? reason)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Edit")) return Forbid();

        var (success, error) = await _lifecycleService.AdvanceStatusAsync(
            id, "InProgress", reason, await GetCurrentEmployeeIdAsync(), null, null, null, null, null, null);

        TempData[success ? "Message" : "Error"] = success ? "Work order resumed" : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Approve")) return Forbid();

        var (success, error) = await _lifecycleService.AdvanceStatusAsync(
            id, "Cancelled", reason, await GetCurrentEmployeeIdAsync(), null, null, null, null, null, null);

        TempData[success ? "Message" : "Error"] = success ? "Work order cancelled" : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reason)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Approve")) return Forbid();

        var (success, error) = await _lifecycleService.AdvanceStatusAsync(
            id, "Rejected", reason, await GetCurrentEmployeeIdAsync(), null, null, null, null, null, null);

        TempData[success ? "Message" : "Error"] = success ? "Work order rejected" : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLaborEntry(int workOrderId, int employeeId, int? teamId, DateTime workDate,
        decimal regularHours, decimal overtimeHours, string? notes)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Edit")) return Forbid();
        try { await _laborService.AddLaborEntryAsync(workOrderId, employeeId, teamId, workDate, regularHours, overtimeHours, notes, await BuildActorAsync()); TempData["Message"] = "ساعت کار ثبت شد."; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id = workOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSpareUsage(int workOrderId, int materialItemId, decimal quantity, decimal? unitCost)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Edit")) return Forbid();
        if (quantity <= 0) { TempData["Error"] = "تعداد مصرف باید بزرگ‌تر از صفر باشه."; return RedirectToAction(nameof(Details), new { id = workOrderId }); }
        if (unitCost.HasValue && unitCost.Value < 0) { TempData["Error"] = "قیمت واحد نمی‌تونه منفی باشه."; return RedirectToAction(nameof(Details), new { id = workOrderId }); }

        var materialItem = await _context.MaterialItems.FirstOrDefaultAsync(x => x.MaterialItemId == materialItemId);
        if (materialItem == null) return NotFound();

        // طبق سند Inventory بخش ۹-۱۰: بدون انتخاب انبار در فرم فعلی، موجودترین انبار برای
        // این متریال انتخاب می‌شه - Transaction است که رکورد واقعی رو نگه می‌داره، نه این انتخاب.
        var stock = await _context.MaterialStocks
            .Where(x => x.MaterialItemId == materialItemId && x.IsActive)
            .OrderByDescending(x => x.OnHandQuantity - x.ReservedQuantity)
            .FirstOrDefaultAsync();

        if (stock == null || stock.AvailableQuantity < quantity)
        {
            var available = stock?.AvailableQuantity ?? 0;
            TempData["Error"] = $"موجودی کافی نیست - موجودی قابل‌مصرف «{materialItem.Description}»: {available}.";
            return RedirectToAction(nameof(Details), new { id = workOrderId });
        }

        var employeeId = await GetCurrentEmployeeIdAsync();

        await _context.MaintenanceSpareUsages.AddAsync(new MaintenanceSpareUsage
        {
            WorkOrderId = workOrderId, MaterialItemId = materialItemId, Quantity = quantity, UnitCost = unitCost,
            TotalCost = unitCost.HasValue ? unitCost.Value * quantity : null, UsageDate = DateTime.Now,
            IsActive = true, CreatedDate = DateTime.Now
        });

        await _context.InventoryTransactions.AddAsync(new InventoryTransaction
        {
            MaterialItemId = materialItemId, WarehouseId = stock.WarehouseId, StorageLocationId = stock.StorageLocationId,
            TransactionType = "Issue", Quantity = quantity, ReferenceType = "WorkOrder", ReferenceId = workOrderId,
            WorkOrderId = workOrderId, EmployeeId = employeeId, TransactionDate = DateTime.Now,
            Reason = $"مصرف در WorkOrder #{workOrderId}"
        });

        stock.OnHandQuantity -= quantity;
        stock.LastUpdated = DateTime.Now;

        await _context.SaveChangesAsync();
        TempData["Message"] = "مصرف قطعه ثبت شد" + (stock.IsLowStock ? " - هشدار: موجودی این متریال کمه." : "");
        return RedirectToAction(nameof(Details), new { id = workOrderId });
    }

    // طبق CMMS Production Baseline v1 بند ۱۲.۱ (Return): برگرداندن بخشی از یک مصرف قبلاً
    // ثبت‌شده - Quantity اصلی (واقعیت تاریخی) دست‌نخورده می‌ماند، فقط ReturnedQuantity بالا
    // می‌رود و TotalCost به مصرف خالص بازمحاسبه می‌شود.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnSpareUsage(int maintenanceSpareUsageId, int workOrderId, decimal returnQuantity, string? reason)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Edit")) return Forbid();

        if (returnQuantity <= 0)
        {
            TempData["Error"] = "مقدار برگشتی باید بزرگ‌تر از صفر باشه.";
            return RedirectToAction(nameof(Details), new { id = workOrderId });
        }

        var usage = await _context.MaintenanceSpareUsages
            .Include(x => x.MaterialItem)
            .FirstOrDefaultAsync(x => x.MaintenanceSpareUsageId == maintenanceSpareUsageId);

        if (usage == null) return NotFound();

        var alreadyReturned = usage.ReturnedQuantity ?? 0;
        var outstanding = usage.Quantity - alreadyReturned;

        if (returnQuantity > outstanding)
        {
            TempData["Error"] = $"حداکثر مقدار قابل‌برگشت {outstanding} است.";
            return RedirectToAction(nameof(Details), new { id = workOrderId });
        }

        var stock = await _context.MaterialStocks
            .Where(x => x.MaterialItemId == usage.MaterialItemId && x.IsActive)
            .OrderByDescending(x => x.OnHandQuantity)
            .FirstOrDefaultAsync();

        if (stock == null)
        {
            TempData["Error"] = "رکورد موجودی این متریال پیدا نشد.";
            return RedirectToAction(nameof(Details), new { id = workOrderId });
        }

        var employeeId = await GetCurrentEmployeeIdAsync();

        var (success, error) = await _materialStockService.ReturnAsync(
            usage.MaterialItemId, stock.WarehouseId, stock.StorageLocationId, returnQuantity,
            reason ?? $"برگشت از WorkOrder #{workOrderId}", employeeId);

        if (!success)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Details), new { id = workOrderId });
        }

        usage.ReturnedQuantity = alreadyReturned + returnQuantity;
        if (usage.UnitCost.HasValue)
            usage.TotalCost = usage.UnitCost.Value * (usage.Quantity - usage.ReturnedQuantity.Value);

        await _context.SaveChangesAsync();

        TempData["Message"] = "برگشت قطعه ثبت شد.";
        return RedirectToAction(nameof(Details), new { id = workOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRequiredSkill(int workOrderId, int skillId, string? minimumProficiencyLevel, decimal? plannedHours)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Edit")) return Forbid();

        if (await _context.RequiredSkills.AnyAsync(x => x.WorkOrderId == workOrderId && x.SkillId == skillId))
        {
            TempData["Error"] = "این مهارت از قبل به‌عنوان پیش‌نیاز این WorkOrder ثبت شده.";
            return RedirectToAction(nameof(Details), new { id = workOrderId });
        }

        await _context.RequiredSkills.AddAsync(new RequiredSkill
        {
            WorkOrderId = workOrderId,
            SkillId = skillId,
            MinimumProficiencyLevel = minimumProficiencyLevel,
            PlannedHours = plannedHours,
            CreatedDate = DateTime.Now
        });
        await _context.SaveChangesAsync();

        TempData["Message"] = "مهارت موردنیاز اضافه شد.";
        return RedirectToAction(nameof(Details), new { id = workOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRequiredMaterial(int workOrderId, int materialItemId, decimal requiredQuantity)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Edit")) return Forbid();

        if (await _context.WorkOrderMaterials.AnyAsync(x => x.WorkOrderId == workOrderId && x.MaterialItemId == materialItemId))
        {
            TempData["Error"] = "این متریال از قبل به‌عنوان نیاز این WorkOrder ثبت شده.";
            return RedirectToAction(nameof(Details), new { id = workOrderId });
        }

        await _context.WorkOrderMaterials.AddAsync(new WorkOrderMaterial
        {
            WorkOrderId = workOrderId,
            MaterialItemId = materialItemId,
            RequiredQuantity = requiredQuantity,
            CreatedDate = DateTime.Now
        });
        await _context.SaveChangesAsync();

        TempData["Message"] = "متریال موردنیاز اضافه شد.";
        return RedirectToAction(nameof(Details), new { id = workOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRequiredMaterial(int workOrderMaterialId, int workOrderId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Edit")) return Forbid();

        var requiredMaterial = await _context.WorkOrderMaterials.FirstOrDefaultAsync(x => x.WorkOrderMaterialId == workOrderMaterialId);
        if (requiredMaterial != null)
        {
            _context.WorkOrderMaterials.Remove(requiredMaterial);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Details), new { id = workOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRequiredSkill(int requiredSkillId, int workOrderId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Edit")) return Forbid();

        var requiredSkill = await _context.RequiredSkills.FirstOrDefaultAsync(x => x.RequiredSkillId == requiredSkillId);
        if (requiredSkill != null)
        {
            _context.RequiredSkills.Remove(requiredSkill);
            await _context.SaveChangesAsync();
        }

        TempData["Message"] = "مهارت موردنیاز حذف شد.";
        return RedirectToAction(nameof(Details), new { id = workOrderId });
    }

    // طبق بخش ۱۴ سند: ساخت سریع یک Crew اختصاصی برای همین WorkOrder، بدون خروج از صفحه
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCrewForWorkOrder(int workOrderId, string code, string name)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Edit")) return Forbid();
        var (success, error, _) = await _crewService.CreateAsync(code, name, workOrderId);
        TempData[success ? "Message" : "Error"] = success ? "Crew ساخته شد." : error;
        return RedirectToAction(nameof(Details), new { id = workOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestDisciplineReview(int workOrderId, List<int> disciplineIds)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "Edit")) return Forbid();
        try { await _reviewService.RequestDisciplineReviewAsync(workOrderId, disciplineIds, await GetCurrentEmployeeIdAsync(), GetClientIp(), GetUserAgent()); TempData["Message"] = "بازبینی چند-دیسیپلینه شروع شد."; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id = workOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RespondDisciplineReview(int workOrderId, string decision, string? findings, string? failureModeCode, string? failureCauseCode)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "WorkOrder", "View")) return Forbid();
        var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
        var myEmployeeId = await GetCurrentEmployeeIdAsync();
        if (!myDisciplineId.HasValue || !myEmployeeId.HasValue) { TempData["Error"] = "کارمند مرتبط با این کاربر یا دیسیپلینش مشخص نیست."; return RedirectToAction(nameof(Details), new { id = workOrderId }); }
        try { await _reviewService.RespondAsync(workOrderId, decision, findings, failureModeCode, failureCauseCode, myEmployeeId.Value, myDisciplineId.Value, GetClientIp(), GetUserAgent()); TempData["Message"] = "نظر شما ثبت شد."; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id = workOrderId });
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }

    private async Task<int?> GetCurrentEmployeeDisciplineIdAsync()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (!employeeId.HasValue) return null;
        return await _context.Employees.Where(e => e.EmployeeId == employeeId.Value).Select(e => (int?)e.DisciplineId).FirstOrDefaultAsync();
    }

    private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? GetUserAgent() => Request.Headers["User-Agent"].ToString();

    private static string L(string fa, string en)
        => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa" ? fa : en;

    private async Task<WorkOrderActor> BuildActorAsync() => new(await GetCurrentEmployeeIdAsync(), _currentUser.Roles);

    private static string? GetNextStatus(string current)
    {
        var index = Array.IndexOf(StatusFlow, current);
        if (index < 0 || index >= StatusFlow.Length - 1) return null;
        return StatusFlow[index + 1];
    }
}
