using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Workflow;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق CMMS Production Baseline v1 بند P0-05: پیاده‌سازی مرجع «اعتبارسنجی گذار وضعیت در
/// Service، نه فقط Controller» - همان الگویی که در فاز بعدی به WorkOrderLifecycleService هم
/// اضافه می‌شود.
/// </summary>
public class WorkRequestService : IWorkRequestService
{
    private readonly PEMSDbContext _context;
    private readonly INumberingService _numberingService;
    private readonly IFunctionalLocationService _functionalLocationService;

    // ------------------------------------------------------------------------------
    // LEGACY (فقط رکوردهای قبل از مهاجرت که Instance گردش‌کار ندارند). برای رکورد تحت گردش‌کار مرکزی هرگز
    // استفاده نمی‌شود: AdvanceStatusAsync آن‌ها را رد می‌کند و UI مراحل/اکشن‌ها را از مدل خواندنی می‌گیرد.
    // ------------------------------------------------------------------------------
    private static readonly Dictionary<string, string> LegacyNextStatus = new()
    {
        ["Draft"] = "Submitted",
        ["Submitted"] = "UnderReview",
        ["UnderReview"] = "Approved",
        ["Approved"] = "Planned",
    };

    private static string T(string fa, string en)
        => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa" ? fa : en;

    public string? GetLegacyNextStatus(string currentStatus)
        => LegacyNextStatus.TryGetValue(currentStatus, out var next) ? next : null;

    private static readonly Dictionary<string, string[]> LegalTransitions = new()
    {
        ["Draft"] = new[] { "Submitted", "Cancelled" },
        ["Submitted"] = new[] { "UnderReview", "Cancelled" },
        ["UnderReview"] = new[] { "Approved", "Rejected", "Cancelled" },
        ["Approved"] = new[] { "Planned", "Rejected", "Cancelled" },
        ["Planned"] = new[] { "Converted", "Cancelled" },
    };

    // --- Phase 2: اتصال به گردش‌کار مرکزی (PEMS.WorkflowPlatform). پارامترهای اختیاری تا سازنده‌ی قبلی (تست‌ها) کار کند ---
    public const string WorkflowModule = "WorkRequest";
    public const string WorkflowEntityType = "WorkRequest";
    public const string WorkflowContext = "Standard";
    private const string PermissionPrefix = "Permission:";

    private readonly IWorkflowRuntime _runtime;
    private readonly IWorkflowReadModelService _readModel;
    private readonly IWorkflowBindingService _bindings;
    private readonly IAccessControlService _access;

    public WorkRequestService(PEMSDbContext context, INumberingService numberingService, IFunctionalLocationService functionalLocationService,
        IWorkflowRuntime? runtime = null, IWorkflowReadModelService? readModel = null, IWorkflowBindingService? bindings = null,
        IAccessControlService? accessControl = null)
    {
        _context = context;
        _numberingService = numberingService;
        _functionalLocationService = functionalLocationService;

        _access = accessControl ?? new AccessControlService(context);
        var hook = new AccessControlWorkflowAuthorizationHook(_access);
        _bindings = bindings ?? new WorkflowBindingService(context);
        _runtime = runtime ?? new WorkflowRuntimeService(context, _bindings, null, hook);
        _readModel = readModel ?? new WorkflowReadModelService(context, null, hook);
    }

    // ==============================================================================
    // Phase 2 - Work Request روی گردش‌کار مرکزی. تقسیم مسئولیت:
    //   * پلتفرم: مراحل، گذارها، شرکت‌کنندگان، مجوز گذار (RequiredPermission)، تاریخچه‌ی اجرایی، نسخه‌بندی.
    //   * این سرویس (دامنه‌ی Work Request): قواعد خاص Work Request (شرط تبدیل به WorkOrder، فیلدهای
    //     Reviewed/Approved/RejectionReason، نگاشت وضعیت به ستون Status قدیمی برای داشبوردها، ممیزی کسب‌وکاری).
    // مجوز فعلی «WorkRequest.Create/Approve» عیناً حفظ شده: شرکت‌کننده‌ی هر مرحله یک نقش-مجازی
    // «Permission:WorkRequest.X» است که همین سرویس از روی مجوزهای واقعی نقش‌های کاربر می‌سازد.
    // ==============================================================================
    private async Task<List<string>> ExpandRolesAsync(IReadOnlyList<string> roles)
    {
        var expanded = new List<string>(roles ?? Array.Empty<string>());
        foreach (var action in new[] { "Create", "Approve" })
            if ((await _access.AuthorizeForRolesAsync(expanded.ToList(), "WorkRequest", action)).Allowed)
                expanded.Add($"{PermissionPrefix}WorkRequest.{action}");
        return expanded;
    }

    /// <summary>
    /// ارتباط صریح Work Request ↔ Instance از طریق EntityKey. یک Work Request فقط یک‌بار وارد گردش‌کار می‌شود
    /// (Submit اگر Instance وجود داشته باشد رد می‌شود و RowVersion از Submit هم‌زمان جلوگیری می‌کند)، پس باید حداکثر
    /// یک Instance باشد. بیش از یکی = نقض ناوردا: «جدیدترین» را حدس نمی‌زنیم، خطای شفاف می‌دهیم.
    /// </summary>
    private async Task<WorkflowInstance?> CurrentInstanceAsync(int workRequestId)
    {
        var all = await _readModel.FindInstancesForEntityAsync(WorkflowModule, WorkflowEntityType, workRequestId);
        if (all.Count > 1)
            throw new InvalidOperationException(T($"برای Work Request {workRequestId} بیش از یک Instance گردش‌کار ثبت شده است ({all.Count}); رکورد ناسازگار است و نیازمند بررسی مدیر.", $"Work Request {workRequestId} has more than one workflow instance ({all.Count}); the record is inconsistent and needs administrator review."));
        return all.FirstOrDefault();
    }

    private async Task<string?> ResolveStartStatusAsync()
    {
        var res = await _bindings.ResolveAsync(WorkflowModule, WorkflowEntityType, WorkflowContext);
        if (res == null) return null;
        var start = await _context.WorkflowTransitions.Where(t => t.WorkflowVersionId == res.Version.WorkflowVersionId && t.FromStageId == null)
            .OrderBy(t => t.Priority).FirstOrDefaultAsync();
        if (start?.ToStageId == null) return null;
        return await _context.WorkflowStages.Where(s => s.WorkflowStageId == start.ToStageId).Select(s => s.BusinessStatus).FirstOrDefaultAsync();
    }

    /// <summary>وضعیت کسب‌وکاری جدید: مرحله‌ی مقصد (از داده‌ی پیکربندی) یا وضعیت پایانی از روی اکشن.</summary>
    private async Task<string?> ResolveNewStatusAsync(WorkflowAvailableAction action, string currentStatus)
    {
        if (action.ToStageId.HasValue)
        {
            var bs = await _context.WorkflowStages.Where(s => s.WorkflowStageId == action.ToStageId).Select(s => s.BusinessStatus).FirstOrDefaultAsync();
            return string.IsNullOrWhiteSpace(bs) ? currentStatus : bs;
        }
        return action.ActionType switch { "Reject" => "Rejected", "Cancel" => "Cancelled", "Complete" => "Converted", _ => currentStatus };
    }

    private async Task StageBusinessChangeAsync(WorkRequest wr, string newStatus, string? reason, int? employeeId)
    {
        var from = wr.Status;
        if (from == newStatus) return;
        if (newStatus == "UnderReview") { wr.ReviewedById = employeeId; wr.ReviewedDate = DateTime.Now; }
        if (newStatus == "Approved") { wr.ApprovedById = employeeId; wr.ApprovedDate = DateTime.Now; }
        if (newStatus == "Rejected") wr.RejectionReason = reason;
        wr.Status = newStatus;
        await _context.WorkRequestStatusHistories.AddAsync(new WorkRequestStatusHistory
        {
            WorkRequestId = wr.WorkRequestId, FromStatus = from, ToStatus = newStatus,
            ChangedById = employeeId, ChangedDate = DateTime.Now, Reason = reason
        });
    }

    public async Task<(bool Success, string? Error)> SubmitAsync(int workRequestId, WorkRequestActor actor)
    {
        var wr = await _context.WorkRequests.FirstOrDefaultAsync(x => x.WorkRequestId == workRequestId);
        if (wr == null) return (false, "Work Request پیدا نشد.");
        if (wr.Status != "Draft") return (false, T("فقط پیش‌نویس قابل ارسال به گردش‌کار است.", "Only a draft can be submitted to the workflow."));
        try { if (await CurrentInstanceAsync(workRequestId) != null) return (false, T("این درخواست قبلاً وارد گردش‌کار شده است.", "This request has already entered the workflow.")); }
        catch (InvalidOperationException ex) { return (false, ex.Message); }

        var roles = await ExpandRolesAsync(actor.Roles);
        if (!roles.Contains($"{PermissionPrefix}WorkRequest.Create"))
            return (false, T("شما مجوز ارسال Work Request را ندارید.", "You do not have permission to submit a Work Request."));

        string? startStatus;
        try { startStatus = await ResolveStartStatusAsync(); }
        catch (WorkflowBindingAmbiguousException ex) { return (false, ex.Message); }   // ابهام Binding = خطای قطعی، نه انتخاب یکی
        if (startStatus == null)
            return (false, T("گردش‌کار Work Request پیکربندی نشده است (Binding فعال یا مرحله‌ی شروع یافت نشد).", "The Work Request workflow is not configured (no active binding or start stage found)."));

        try
        {
            await StageBusinessChangeAsync(wr, startStatus, null, actor.EmployeeId);
            // Instance + History + تغییر Status/ممیزی کسب‌وکاری در یک SaveChanges اتمیک ذخیره می‌شوند
            await _runtime.StartInstanceAsync(WorkflowModule, WorkflowEntityType, workRequestId, WorkflowContext, null, actor.EmployeeId);
            return (true, null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            return (false, ex is DbUpdateConcurrencyException ? T("این درخواست هم‌زمان توسط کاربر دیگری تغییر کرد؛ صفحه را دوباره بارگذاری کنید.", "This request was changed by another user at the same time; reload the page.") : ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> ExecuteWorkflowActionAsync(int workRequestId, string actionType, string? comment, WorkRequestActor actor)
    {
        var wr = await _context.WorkRequests.FirstOrDefaultAsync(x => x.WorkRequestId == workRequestId);
        if (wr == null) return (false, "Work Request پیدا نشد.");

        WorkflowInstance? instance;
        try { instance = await CurrentInstanceAsync(workRequestId); }
        catch (InvalidOperationException ex) { return (false, ex.Message); }
        if (instance == null) return (false, T("این درخواست Instance گردش‌کار ندارد (رکورد Legacy یا پیش‌نویس).", "This request has no workflow instance (legacy record or draft)."));
        if (instance.Status != "Running") return (false, T($"گردش‌کار در وضعیت «{instance.Status}» است.", $"The workflow is {instance.Status}."));

        // «Complete» = تبدیل به WorkOrder؛ اکشن کسب‌وکاری است و قواعد خودش (Planned، تجهیز، یک‌بار) را دارد
        if (actionType == "Complete")
            return (false, T("این اقدام از مسیر «تبدیل به WorkOrder» انجام می‌شود.", "This action is performed through \"Convert to Work Order\"."));

        var roles = await ExpandRolesAsync(actor.Roles);
        var actions = await _readModel.GetAvailableActionsAsync(instance.WorkflowInstanceId, new WorkflowUserContext(actor.EmployeeId, roles));
        var action = actions.FirstOrDefault(a => a.ActionType == actionType);
        if (action == null) return (false, T($"اقدام «{actionType}» در مرحله‌ی جاری برای شما در دسترس نیست.", $"Action \"{actionType}\" is not available for you at the current stage."));
        if (!action.IsAllowed) return (false, T($"اقدام «{actionType}» مجاز نیست ({action.DeniedReason}).", $"Action \"{actionType}\" is not allowed ({action.DeniedReason})."));

        var newStatus = await ResolveNewStatusAsync(action, wr.Status);
        try
        {
            await StageBusinessChangeAsync(wr, newStatus ?? wr.Status, comment, actor.EmployeeId);
            // پلتفرم مجدداً و به‌صورت مستقل مجوز/SoD/شرط را اعمال می‌کند؛ سپس همه‌ی تغییرات را اتمیک ذخیره می‌کند
            await _runtime.ExecuteActionAsync(instance.WorkflowInstanceId, actionType, actor.EmployeeId, roles, comment, null, null);
            return (true, null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            return (false, ex is DbUpdateConcurrencyException ? T("این درخواست هم‌زمان توسط کاربر دیگری تغییر کرد؛ صفحه را دوباره بارگذاری کنید.", "This request was changed by another user at the same time; reload the page.") : ex.Message);
        }
    }

    public async Task<WorkflowRuntimeViewModel?> GetWorkflowViewAsync(int workRequestId, WorkRequestActor actor)
    {
        var instance = await CurrentInstanceAsync(workRequestId);   // ارتباط صریح و سخت‌گیرانه؛ نه «جدیدترین»
        if (instance == null) return null;
        var roles = await ExpandRolesAsync(actor.Roles);
        return await _readModel.GetRuntimeViewAsync(instance.WorkflowInstanceId, new WorkflowUserContext(actor.EmployeeId, roles));
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): قبلاً کل جدول فیلترشده
    // با ToListAsync (بدون Skip/Take) بارگذاری می‌شد. دو Include قبلی هم غیرضروری بودند - چون
    // Select پایین‌تر مستقیم فیلد اسکالر می‌گیرد، نه خودِ Entity ناوبری‌شده را.
    public async Task<PagedResult<WorkRequestListDto>> GetListAsync(int page = 1, int pageSize = 50, string? status = null)
    {
        var query = _context.WorkRequests.Where(x => x.IsActive).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

        var totalCount = await query.CountAsync();

        page = page < 1 ? 1 : page;
        pageSize = DataGridPageSize.Normalize(pageSize);

        var items = await query
            .OrderByDescending(x => x.RequestedDate)
            .ThenByDescending(x => x.WorkRequestId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new WorkRequestListDto
            {
                WorkRequestId = x.WorkRequestId,
                RequestNumber = x.RequestNumber,
                Title = x.Title,
                Status = x.Status,
                Priority = x.Priority,
                RequestedByName = x.RequestedBy != null ? x.RequestedBy.FirstName + " " + x.RequestedBy.LastName : "",
                RequestedDate = x.RequestedDate,
                EquipmentTagNumber = x.Equipment != null ? x.Equipment.TagNumber : null
            })
            .ToListAsync();

        return new PagedResult<WorkRequestListDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<WorkRequestDetailDto?> GetDetailsAsync(int workRequestId)
    {
        var wr = await _context.WorkRequests
            .Include(x => x.RequestedBy)
            .Include(x => x.ReviewedBy)
            .Include(x => x.ApprovedBy)
            .Include(x => x.Equipment)
            .Include(x => x.Asset)
            .Include(x => x.SuggestedDiscipline)
            .Include(x => x.LinkedWorkOrder)
            .FirstOrDefaultAsync(x => x.WorkRequestId == workRequestId);

        if (wr == null) return null;

        var history = await _context.WorkRequestStatusHistories
            .Include(x => x.ChangedBy)
            .Where(x => x.WorkRequestId == workRequestId)
            .OrderByDescending(x => x.ChangedDate)
            .Select(x => new WorkRequestStatusHistoryDto
            {
                FromStatus = x.FromStatus,
                ToStatus = x.ToStatus,
                ChangedByName = x.ChangedBy != null ? x.ChangedBy.FirstName + " " + x.ChangedBy.LastName : null,
                ChangedDate = x.ChangedDate,
                Reason = x.Reason
            })
            .ToListAsync();

        string? criticalityClass = null;
        if (wr.EquipmentId.HasValue)
        {
            criticalityClass = await _context.EquipmentCriticalities
                .Where(x => x.EquipmentId == wr.EquipmentId.Value && x.IsActive)
                .Select(x => x.CriticalityClass)
                .FirstOrDefaultAsync();
        }

        return new WorkRequestDetailDto
        {
            WorkRequestId = wr.WorkRequestId,
            RequestNumber = wr.RequestNumber,
            Title = wr.Title,
            Description = wr.Description,
            RequestedByName = wr.RequestedBy != null ? $"{wr.RequestedBy.FirstName} {wr.RequestedBy.LastName}" : "",
            RequestedDate = wr.RequestedDate,
            EquipmentId = wr.EquipmentId,
            EquipmentTagNumber = wr.Equipment?.TagNumber,
            AssetId = wr.AssetId,
            AssetNumber = wr.Asset?.AssetNumber,
            FunctionalLocationId = wr.FunctionalLocationId,
            FunctionalLocationBreadcrumb = await _functionalLocationService.GetBreadcrumbAsync(wr.FunctionalLocationId),
            ReportedCondition = wr.ReportedCondition,
            Priority = wr.Priority,
            EquipmentCriticalityClass = criticalityClass,
            SuggestedDisciplineId = wr.SuggestedDisciplineId,
            SuggestedDisciplineName = wr.SuggestedDiscipline?.Name,
            Status = wr.Status,
            ReviewedByName = wr.ReviewedBy != null ? $"{wr.ReviewedBy.FirstName} {wr.ReviewedBy.LastName}" : null,
            ReviewedDate = wr.ReviewedDate,
            RejectionReason = wr.RejectionReason,
            ApprovedByName = wr.ApprovedBy != null ? $"{wr.ApprovedBy.FirstName} {wr.ApprovedBy.LastName}" : null,
            ApprovedDate = wr.ApprovedDate,
            LinkedWorkOrderId = wr.LinkedWorkOrderId,
            LinkedWorkOrderNumber = wr.LinkedWorkOrder?.WorkOrderNumber,
            StatusHistory = history
        };
    }

    public async Task<(bool Success, string? Error, int? Id)> CreateAsync(
        string title, string? description, int requestedById, int? equipmentId, int? assetId,
        int? functionalLocationId, string? reportedCondition, string priority, int? suggestedDisciplineId)
    {
        title = title?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(title)) return (false, "عنوان درخواست الزامی است.", null);

        if (!await _context.Employees.AnyAsync(x => x.EmployeeId == requestedById && x.IsActive))
            return (false, "کارمند درخواست‌دهنده نامعتبره.", null);

        var workRequest = new WorkRequest
        {
            RequestNumber = await _numberingService.GetNextNumberAsync("WorkRequest"),
            Title = title,
            Description = description,
            RequestedById = requestedById,
            RequestedDate = DateTime.Now,
            EquipmentId = equipmentId,
            AssetId = assetId,
            FunctionalLocationId = functionalLocationId,
            ReportedCondition = reportedCondition,
            Priority = string.IsNullOrWhiteSpace(priority) ? "Medium" : priority,
            SuggestedDisciplineId = suggestedDisciplineId,
            Status = "Draft",
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.WorkRequests.AddAsync(workRequest);
        await _context.SaveChangesAsync();

        return (true, null, workRequest.WorkRequestId);
    }

    public async Task<(bool Success, string? Error)> AdvanceStatusAsync(
        int workRequestId, string nextStatus, string? reason, int? changedByEmployeeId)
    {
        var wr = await _context.WorkRequests.FirstOrDefaultAsync(x => x.WorkRequestId == workRequestId);
        if (wr == null) return (false, "Work Request پیدا نشد.");

        // Phase 2: مسیر Legacy فقط برای رکوردهای بدون Instance گردش‌کار است. اگر Instance دارد، دور زدن گردش‌کار مرکزی ممنوع است.
        try
        {
            if (await CurrentInstanceAsync(workRequestId) != null)
                return (false, T("این درخواست تحت گردش‌کار مرکزی است؛ تغییر وضعیت فقط از مسیر اقدامات گردش‌کار مجاز است.", "This request is governed by the central workflow; its status can only change through workflow actions."));
        }
        catch (InvalidOperationException ex) { return (false, ex.Message); }
        // پیش‌نویس جدید باید با SubmitAsync وارد گردش‌کار شود، نه با تغییر مستقیم وضعیت
        if (wr.Status == "Draft" && nextStatus == "Submitted")
        {
            bool bound;
            try { bound = await _bindings.ResolveAsync(WorkflowModule, WorkflowEntityType, WorkflowContext) != null; }
            catch (WorkflowBindingAmbiguousException) { bound = true; }   // Binding مبهم هم یعنی مسیر Legacy مجاز نیست
            if (bound) return (false, T("ارسال Work Request باید از مسیر گردش‌کار مرکزی (Submit) انجام شود.", "A Work Request must be submitted through the central workflow (Submit)."));
        }

        if (!LegalTransitions.TryGetValue(wr.Status, out var allowed) || !allowed.Contains(nextStatus))
            return (false, $"گذار از وضعیت «{wr.Status}» به «{nextStatus}» مجاز نیست.");

        var fromStatus = wr.Status;

        if (nextStatus == "UnderReview")
        {
            wr.ReviewedById = changedByEmployeeId;
            wr.ReviewedDate = DateTime.Now;
        }

        if (nextStatus == "Approved")
        {
            wr.ApprovedById = changedByEmployeeId;
            wr.ApprovedDate = DateTime.Now;
        }

        if (nextStatus == "Rejected")
        {
            wr.RejectionReason = reason;
        }

        wr.Status = nextStatus;

        await _context.WorkRequestStatusHistories.AddAsync(new WorkRequestStatusHistory
        {
            WorkRequestId = wr.WorkRequestId,
            FromStatus = fromStatus,
            ToStatus = nextStatus,
            ChangedById = changedByEmployeeId,
            ChangedDate = DateTime.Now,
            Reason = reason
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error, int? WorkOrderId)> ConvertToWorkOrderAsync(
        int workRequestId, string workOrderType, string? currentUserName, WorkRequestActor? actor = null)
    {
        var wr = await _context.WorkRequests.Include(x => x.Equipment).FirstOrDefaultAsync(x => x.WorkRequestId == workRequestId);
        if (wr == null) return (false, "Work Request پیدا نشد.", null);

        if (wr.Status != "Planned")
            return (false, "فقط درخواست‌های در وضعیت Planned قابل تبدیل به WorkOrder هستند.", null);

        if (wr.LinkedWorkOrderId.HasValue)
            return (false, "این درخواست قبلاً به یک WorkOrder تبدیل شده.", null);

        if (!wr.EquipmentId.HasValue)
            return (false, "برای ساخت WorkOrder، این درخواست باید به یک تجهیز مشخص وصل باشد.", null);

        // Phase 2: اگر تحت گردش‌کار مرکزی است، اکشن «Complete» باید برای این کاربر مجاز باشد (سمت‌سرور، قبل از هر تغییر)
        WorkflowInstance? instance;
        try { instance = await CurrentInstanceAsync(workRequestId); }
        catch (InvalidOperationException ex) { return (false, ex.Message, null); }
        List<string>? actorRoles = null;
        if (instance != null)
        {
            if (actor == null) return (false, T("برای Work Request تحت گردش‌کار، اطلاعات کاربر اجراکننده الزامی است.", "The acting user is required for a workflow-governed Work Request."), null);
            if (instance.Status != "Running") return (false, T($"گردش‌کار در وضعیت «{instance.Status}» است.", $"The workflow is {instance.Status}."), null);
            actorRoles = await ExpandRolesAsync(actor.Roles);
            var actions = await _readModel.GetAvailableActionsAsync(instance.WorkflowInstanceId, new WorkflowUserContext(actor.EmployeeId, actorRoles));
            var complete = actions.FirstOrDefault(a => a.ActionType == "Complete");
            if (complete == null) return (false, T("اقدام تبدیل در مرحله‌ی جاری گردش‌کار در دسترس نیست.", "The conversion action is not available at the current workflow stage."), null);
            if (!complete.IsAllowed) return (false, T($"اقدام تبدیل مجاز نیست ({complete.DeniedReason}).", $"The conversion action is not allowed ({complete.DeniedReason})."), null);
        }

        var workOrderNumber = await _numberingService.GetNextNumberAsync("WorkOrder");

        var workOrder = new WorkOrder
        {
            EquipmentId = wr.EquipmentId.Value,
            SourceWorkRequestId = wr.WorkRequestId,
            WorkOrderNumber = workOrderNumber,
            WorkOrderType = workOrderType,
            Priority = wr.Priority,
            Status = "Open",
            Description = wr.Title + (string.IsNullOrEmpty(wr.Description) ? "" : " - " + wr.Description),
            IsActive = true,
            CreatedBy = currentUserName,
            CreatedDate = DateTime.Now
        };

        var fromStatus = wr.Status;
        // از navigation property به جای FK خام استفاده شد چون workOrder هنوز Id واقعی ندارد -
        // EF Core موقع SaveChanges به‌صورت خودکار FK را از روی object reference پر می‌کند (fixup).
        wr.LinkedWorkOrder = workOrder;
        wr.Status = "Converted";
        wr.ModifiedBy = currentUserName;

        // هر دو تغییر (ساخت WorkOrder و به‌روزرسانی WorkRequest) باید یک‌جا و اتمیک ذخیره شوند
        // تا در صورت خطا بین این دو مرحله، WorkOrder یتیم بدون به‌روزرسانی WorkRequest باقی نماند.
        await _context.WorkOrders.AddAsync(workOrder);
        await _context.WorkRequestStatusHistories.AddAsync(new WorkRequestStatusHistory
        {
            WorkRequestId = wr.WorkRequestId,
            FromStatus = fromStatus,
            ToStatus = "Converted",
            Reason = $"تبدیل به {workOrderNumber}",
            ChangedDate = DateTime.Now
        });

        try
        {
            if (instance != null)
                // WorkOrder + Status + ممیزی + اجرای «Complete» گردش‌کار: یک SaveChanges اتمیک در Runtime
                await _runtime.ExecuteActionAsync(instance.WorkflowInstanceId, "Complete", actor!.EmployeeId, actorRoles!, workOrderNumber, null, null);
            else
                await _context.SaveChangesAsync();
        }
        catch (Exception ex) when (instance != null && ex is InvalidOperationException or UnauthorizedAccessException)
        {
            _context.ChangeTracker.Clear();
            return (false, ex.Message, null);
        }

        return (true, null, workOrder.WorkOrderId);
    }
}
