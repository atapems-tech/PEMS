using Microsoft.EntityFrameworkCore;
using PEMS.WorkflowPlatform.Dtos;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.WorkflowPlatform.Services;

public class WorkflowRuntimeService : IWorkflowRuntime
{
    private readonly IWorkflowDbContext _db;
    private readonly IWorkflowBindingService _bindings;
    private readonly IWorkflowConditionEvaluator _conditions;
    private readonly IWorkflowAuthorizationHook? _authHook;
    private readonly IWorkflowEventSink _events;
    private readonly IWorkflowActionRegistry _registry;

    // پارامترهای اختیاری: new WorkflowRuntimeService(ctx) مثل قبل کار می‌کند (Phase 1 افزایشی است).
    public WorkflowRuntimeService(
        IWorkflowDbContext db,
        IWorkflowBindingService? bindings = null,
        IWorkflowConditionEvaluator? conditions = null,
        IWorkflowAuthorizationHook? authorizationHook = null,
        IWorkflowEventSink? eventSink = null,
        IWorkflowActionRegistry? actionRegistry = null)
    {
        _db = db;
        _bindings = bindings ?? new WorkflowBindingService(db);
        _conditions = conditions ?? new WorkflowConditionEvaluator();
        _authHook = authorizationHook;
        _events = eventSink ?? new NoOpWorkflowEventSink();
        _registry = actionRegistry ?? new WorkflowActionRegistry();
    }

    internal static Dictionary<string, string?> Facts(WorkflowInstance i, WorkflowStage? stage) => new()
    {
        ["Priority"] = i.Priority, ["ProjectId"] = i.ProjectId?.ToString(), ["Module"] = i.Module,
        ["BusinessObjectType"] = i.BusinessObjectType, ["BusinessObjectId"] = i.BusinessObjectId.ToString(), ["StageName"] = stage?.Name
    };

    /// <summary>
    /// انتخاب قطعی گذار: بر اساس Priority صعودی، اولین گذاری که شرطش (در صورت وجود) برقرار است.
    /// شرط خالی = همیشه برقرار (رفتار قبلی). شرط نامعتبر = Fail-closed.
    /// </summary>
    private async Task<WorkflowTransition?> SelectTransitionAsync(WorkflowInstance instance, int? fromStageId, string actionType, WorkflowStage? stage)
    {
        var candidates = await _db.WorkflowTransitions
            .Where(t => t.WorkflowVersionId == instance.WorkflowVersionId && t.FromStageId == fromStageId && t.ActionType == actionType)
            .OrderBy(t => t.Priority).ToListAsync();
        var facts = Facts(instance, stage);
        return candidates.FirstOrDefault(t => _conditions.Evaluate(t.ConditionExpression, facts));
    }

    private async Task EmitAsync(string type, WorkflowInstance i, WorkflowStage? stage, string? action, int? actor)
    {
        try
        {
            await _events.OnEventAsync(new WorkflowRuntimeEvent(type, i.WorkflowInstanceId, i.Module, i.BusinessObjectType, i.BusinessObjectId,
                stage?.WorkflowStageId, stage?.Name, action, actor, stage?.NotificationKey));
        }
        catch { /* Extension point؛ خطای مصرف‌کننده نباید گردش‌کار را خراب کند */ }
    }

    private static IEnumerable<string> SplitRoles(string? responsibleRole)
    {
        return string.IsNullOrWhiteSpace(responsibleRole)
            ? Array.Empty<string>()
            : responsibleRole.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    // ==============================================================================
    // باگ کشف‌شده حین طراحی: قبلاً برای مراحل Sequential هم به ازای هر نقش داخل
    // ResponsibleRole یک WorkflowInstanceStage جدا ساخته می‌شد - یعنی اگر ResponsibleRole
    // یک مرحله‌ی Sequential برابر "EngineeringSupervisor,EngineeringManager,SystemAdmin" بود
    // (یعنی "هرکدوم از این‌ها مجازند")، موتور اشتباهاً منتظر تایید هر سه نفر می‌ماند، چون
    // DetermineStageOutcome صرفاً از روی تعداد رکورد Parallel/Sequential را تشخیص می‌داد.
    // رفع شد: Sequential = فقط یک رکورد با لیست نقش‌های مجاز (رابطه‌ی OR)، Parallel = یک
    // رکورد جدا به ازای هر نقش (رابطه‌ی AND/طبق ApprovalStrategy).
    // ==============================================================================
    private static void CreateInstanceStages(
        IWorkflowDbContext db, WorkflowInstance instance, WorkflowStage stage, IReadOnlyList<string>? explicitParticipants = null)
    {
        // اگر لیست صریح اعضا داده شده (مثلاً IDC که دیسیپلین‌هایش پویاست)، همان استفاده
        // می‌شود - صرف‌نظر از StageType یا ResponsibleRole ایستای خود مرحله. هر عضو رکورد
        // مستقل خودش را می‌گیرد (رفتار Parallel واقعی).
        if (explicitParticipants != null)
        {
            foreach (var role in explicitParticipants.Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                db.WorkflowInstanceStages.Add(new WorkflowInstanceStage
                {
                    Instance = instance,
                    WorkflowStageId = stage.WorkflowStageId,
                    AssignedToRole = role,
                    Status = "Pending",
                    AssignedDate = DateTime.Now
                });
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(stage.ResponsibleRole)) return;

        if (stage.StageType == "Parallel")
        {
            foreach (var role in SplitRoles(stage.ResponsibleRole))
            {
                db.WorkflowInstanceStages.Add(new WorkflowInstanceStage
                {
                    Instance = instance,
                    WorkflowStageId = stage.WorkflowStageId,
                    AssignedToRole = role,
                    Status = "Pending",
                    AssignedDate = DateTime.Now
                });
            }
        }
        else
        {
            db.WorkflowInstanceStages.Add(new WorkflowInstanceStage
            {
                Instance = instance,
                WorkflowStageId = stage.WorkflowStageId,
                AssignedToRole = stage.ResponsibleRole, // رشته‌ی کاما-جدا؛ هرکدام از این نقش‌ها مجاز به اقدام است
                Status = "Pending",
                AssignedDate = DateTime.Now
            });
        }
    }

    private static bool RoleMatches(string? assignedToRole, IReadOnlyList<string> actorRoles)
        => assignedToRole != null && SplitRoles(assignedToRole).Any(actorRoles.Contains);

    public async Task<int> StartInstanceAsync(
        string module,
        string businessObjectType,
        int businessObjectId,
        string workflowCategory,
        int? projectId,
        int? initiatedByEmployeeId,
        IReadOnlyList<string>? firstStageParticipantRoles = null)
    {
        // 1) Binding صریح (اگر تعریف شده باشد)؛ ابهام = خطای قطعی.
        // 2) وگرنه مسیر Legacy (Module + Category)؛ اگر بیش از یک تعریف فعال منطبق باشد «اولین مورد» انتخاب
        //    نمی‌شود و خطای قطعی می‌دهد.
        var resolution = await _bindings.ResolveAsync(module, businessObjectType, workflowCategory);
        WorkflowDefinition definition;
        WorkflowVersion version;

        if (resolution != null)
        {
            definition = resolution.Definition;
            version = resolution.Version;

            if (await _db.WorkflowInstances.AnyAsync(i => i.Module == module && i.BusinessObjectType == businessObjectType
                                                          && i.BusinessObjectId == businessObjectId && i.Status == "Running"))
                throw new InvalidOperationException(
                    $"برای «{businessObjectType}» شماره‌ی {businessObjectId} از قبل یک گردش‌کار در حال اجرا وجود دارد.");
        }
        else
        {
            var candidates = await _db.WorkflowDefinitions
                .Where(d => d.Module == module && d.Category == workflowCategory && d.IsActive)
                .OrderBy(d => d.WorkflowDefinitionId).ToListAsync();

            if (candidates.Count == 0)
                throw new InvalidOperationException(
                    $"هیچ تعریف گردش‌کار فعالی برای ماژول «{module}» و دسته‌ی «{workflowCategory}» یافت نشد.");
            if (candidates.Count > 1)
                throw new WorkflowBindingAmbiguousException(
                    $"بیش از یک تعریف فعال برای ماژول «{module}» و دسته‌ی «{workflowCategory}» وجود دارد؛ یک Binding صریح تعریف کنید.");

            definition = candidates[0];
            var legacyVersion = await _db.WorkflowVersions
                .Where(v => v.WorkflowDefinitionId == definition.WorkflowDefinitionId && v.Status == "Published")
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync();

            if (legacyVersion == null)
                throw new InvalidOperationException($"هیچ نسخه‌ی منتشرشده‌ای برای گردش‌کار «{definition.Name}» یافت نشد.");
            version = legacyVersion;
        }

        // گذار شروع: FromStageId == null
        var startTransition = await _db.WorkflowTransitions
            .Where(t => t.WorkflowVersionId == version.WorkflowVersionId && t.FromStageId == null)
            .OrderBy(t => t.Priority)
            .FirstOrDefaultAsync();

        if (resolution != null
            && await _db.WorkflowTransitions.CountAsync(t => t.WorkflowVersionId == version.WorkflowVersionId && t.FromStageId == null) > 1)
            throw new InvalidOperationException("این نسخه بیش از یک گذار شروع دارد (نقطه‌ی شروع مبهم).");

        if (startTransition?.ToStageId == null)
            throw new InvalidOperationException("گذار شروع (Start Transition) برای این نسخه‌ی گردش‌کار تعریف نشده است.");

        var firstStage = await _db.WorkflowStages
            .FirstOrDefaultAsync(s => s.WorkflowStageId == startTransition.ToStageId);

        if (firstStage == null)
            throw new InvalidOperationException("مرحله‌ی اول گردش‌کار یافت نشد.");

        var instance = new WorkflowInstance
        {
            WorkflowVersionId = version.WorkflowVersionId,
            Module = module,
            BusinessObjectType = businessObjectType,
            BusinessObjectId = businessObjectId,
            ProjectId = projectId,
            CurrentStageId = firstStage.WorkflowStageId,
            Status = "Running",
            StartDate = DateTime.Now,
            InitiatedById = initiatedByEmployeeId,
            WorkflowBindingId = resolution?.Binding.WorkflowBindingId,
            EntityKey = $"{module}|{businessObjectType}|{businessObjectId}"
        };

        _db.WorkflowInstances.Add(instance);

        CreateInstanceStages(_db, instance, firstStage, firstStageParticipantRoles);

        _db.WorkflowHistories.Add(new WorkflowHistory
        {
            Instance = instance,
            FromStageId = null,
            ToStageId = firstStage.WorkflowStageId,
            ActionType = "Start",
            UserId = initiatedByEmployeeId,
            ActionDate = DateTime.Now
        });

        await TryAutoSkipEmptyStageAsync(instance, firstStage, firstStageParticipantRoles);

        await _db.SaveChangesAsync();

        await EmitAsync("StageEntered", instance, firstStage, "Start", initiatedByEmployeeId);
        return instance.WorkflowInstanceId;
    }


    public async Task ExecuteActionAsync(
        int workflowInstanceId,
        string actionType,
        int? actorEmployeeId,
        IReadOnlyList<string> actorRoles,
        string? comment,
        string? ipAddress,
        string? userAgent,
        IReadOnlyList<string>? nextStageParticipantRoles = null)
    {
        var instance = await _db.WorkflowInstances
            .FirstOrDefaultAsync(x => x.WorkflowInstanceId == workflowInstanceId);

        if (instance == null)
            throw new InvalidOperationException("Workflow instance یافت نشد.");

        if (instance.Status != "Running")
            throw new InvalidOperationException($"این گردش‌کار در وضعیت «{instance.Status}» است و اقدام جدید نمی‌پذیرد.");

        if (instance.CurrentStageId == null)
            throw new InvalidOperationException("این گردش‌کار مرحله‌ی جاری معتبری ندارد.");

        var currentStage = await _db.WorkflowStages
            .FirstOrDefaultAsync(s => s.WorkflowStageId == instance.CurrentStageId);

        if (currentStage == null)
            throw new InvalidOperationException("مرحله‌ی جاری یافت نشد.");

        // ==============================================================================
        // پیدا کردن دقیق رکورد InstanceStage متعلق به این کاربر برای این مرحله - این همان
        // مکانیزمی است که از Race Condition در بازبینی موازی جلوگیری می‌کند: هر نقش رکورد
        // مستقل خودش را دارد و فقط همان رکورد به‌روزرسانی می‌شود.
        // ==============================================================================
        var myInstanceStage = (await _db.WorkflowInstanceStages
            .Where(x => x.WorkflowInstanceId == workflowInstanceId
                        && x.WorkflowStageId == currentStage.WorkflowStageId
                        && x.Status == "Pending")
            .ToListAsync())
            .FirstOrDefault(x => RoleMatches(x.AssignedToRole, actorRoles));

        if (myInstanceStage == null)
            throw new UnauthorizedAccessException(
                "شما نقش لازم برای اقدام روی مرحله‌ی جاری این گردش‌کار را ندارید، یا قبلاً اقدام کرده‌اید.");

        // --- Phase 1: مسیر Binding فقط اکشن‌های ثبت‌شده را می‌پذیرد؛ Legacy بدون تغییر ---
        if (instance.WorkflowBindingId != null && _registry.Find(actionType, instance.Module) == null)
            throw new InvalidOperationException($"اکشن «{actionType}» در رجیستری اکشن‌ها ثبت نشده است.");

        // --- Phase 1: SoD و مجوز PEMS روی گذارهایی که RequiredPermission/ForbidInitiator دارند ---
        var guardTransition = await SelectTransitionAsync(instance, currentStage.WorkflowStageId, actionType, currentStage);
        if (guardTransition != null)
        {
            if (guardTransition.ForbidInitiator && actorEmployeeId.HasValue && actorEmployeeId == instance.InitiatedById)
                throw new UnauthorizedAccessException("تفکیک وظایف: آغازکننده‌ی گردش‌کار نمی‌تواند این اقدام را انجام دهد.");

            if (!string.IsNullOrWhiteSpace(guardTransition.RequiredPermission))
            {
                if (_authHook == null)
                    throw new UnauthorizedAccessException("این گذار مجوز می‌خواهد ولی هوک مجوز پیکربندی نشده است (Fail-closed).");
                var decision = await _authHook.AuthorizeAsync(new WorkflowAuthorizationContext(
                    instance, currentStage, guardTransition, actionType, actorEmployeeId, actorRoles));
                if (!decision.Allowed)
                    throw new UnauthorizedAccessException(decision.Reason ?? "مجوز اجرای این اقدام وجود ندارد.");
            }
        }

        myInstanceStage.Status = actionType;
        myInstanceStage.Decision = actionType;
        myInstanceStage.Comment = comment;
        myInstanceStage.ActionDate = DateTime.Now;

        var stageOutcome = DetermineStageOutcome(currentStage, await _db.WorkflowInstanceStages
            .Where(x => x.WorkflowInstanceId == workflowInstanceId && x.WorkflowStageId == currentStage.WorkflowStageId)
            .ToListAsync());

        // اگر مرحله هنوز کامل نشده (منتظر بقیه‌ی بازبینان موازی) - فقط اقدام همین کاربر ثبت
        // می‌شود، بدون گذار به مرحله‌ی بعد
        if (stageOutcome == null)
        {
            _db.WorkflowHistories.Add(new WorkflowHistory
            {
                Instance = instance,
                FromStageId = currentStage.WorkflowStageId,
                ToStageId = currentStage.WorkflowStageId,
                ActionType = actionType,
                Decision = actionType,
                Comment = comment,
                UserId = actorEmployeeId,
                Role = string.Join(",", actorRoles),
                ActionDate = DateTime.Now,
                IPAddress = ipAddress,
                Browser = userAgent
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException(
                    "این آیتم هم‌زمان توسط کاربر دیگری تغییر داده شد. صفحه را دوباره بارگذاری کن و دوباره امتحان کن.");
            }
            return;
        }

        // مرحله کامل شده - گذار بعدی را طبق WorkflowTransition پیدا کن
        var transition = await SelectTransitionAsync(instance, currentStage.WorkflowStageId, stageOutcome, currentStage);

        if (transition == null)
            throw new InvalidOperationException(
                $"برای مرحله‌ی «{currentStage.Name}» با نتیجه‌ی «{stageOutcome}» هیچ گذاری تعریف نشده است.");

        var previousStageId = currentStage.WorkflowStageId;

        if (transition.ToStageId == null)
        {
            // پایان گردش‌کار
            instance.CurrentStageId = null;
            instance.Status = stageOutcome == "Reject" ? "Rejected" : stageOutcome == "Cancel" ? "Cancelled" : "Completed";
            instance.CompletionDate = DateTime.Now;
        }
        else
        {
            var nextStage = await _db.WorkflowStages
                .FirstOrDefaultAsync(s => s.WorkflowStageId == transition.ToStageId);

            if (nextStage == null)
                throw new InvalidOperationException("مرحله‌ی بعدی گردش‌کار یافت نشد.");

            instance.CurrentStageId = nextStage.WorkflowStageId;

            CreateInstanceStages(_db, instance, nextStage, nextStageParticipantRoles);

            await TryAutoSkipEmptyStageAsync(instance, nextStage, nextStageParticipantRoles);
        }

        _db.WorkflowHistories.Add(new WorkflowHistory
        {
            Instance = instance,
            FromStageId = previousStageId,
            ToStageId = instance.CurrentStageId,
            ActionType = actionType,
            Decision = stageOutcome,
            Comment = comment,
            UserId = actorEmployeeId,
            Role = string.Join(",", actorRoles),
            ActionDate = DateTime.Now,
            IPAddress = ipAddress,
            Browser = userAgent
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "این آیتم هم‌زمان توسط کاربر دیگری تغییر داده شد. صفحه را دوباره بارگذاری کن و دوباره امتحان کن.");
        }

        await EmitAsync("ActionExecuted", instance, currentStage, actionType, actorEmployeeId);
        if (instance.Status == "Completed") await EmitAsync("InstanceCompleted", instance, currentStage, actionType, actorEmployeeId);
        else if (instance.Status == "Rejected") await EmitAsync("InstanceRejected", instance, currentStage, actionType, actorEmployeeId);
        else if (instance.CurrentStageId != previousStageId)
            await EmitAsync("StageEntered", instance, await _db.WorkflowStages.FirstOrDefaultAsync(x => x.WorkflowStageId == instance.CurrentStageId), actionType, actorEmployeeId);
    }


    // ==============================================================================
    // اگر یک مرحله (چه اولین مرحله‌ی گردش‌کار، چه مرحله‌ای که از یک گذار به آن رسیده‌ایم)
    // قرار بود اعضای پویا داشته باشد ولی هیچ عضوی تعیین نشد (یعنی تشخیص داده شد نیازی به
    // بازبینی نیست)، هیچ‌کس هرگز نمی‌تواند روی آن اقدام کند و Instance برای همیشه گیر
    // می‌کند. این متد فوری و خودکار از چنین مرحله‌ای عبور می‌کند (طبق گذار با ActionType
    // "Approve" از همان مرحله).
    // ==============================================================================
    private async Task TryAutoSkipEmptyStageAsync(
        WorkflowInstance instance, WorkflowStage stage, IReadOnlyList<string>? explicitParticipants)
    {
        var hasAnyParticipant = explicitParticipants != null
            ? explicitParticipants.Any(r => !string.IsNullOrWhiteSpace(r))
            : !string.IsNullOrWhiteSpace(stage.ResponsibleRole);

        if (hasAnyParticipant) return;

        var skipTransition = await _db.WorkflowTransitions
            .Where(t => t.WorkflowVersionId == instance.WorkflowVersionId
                        && t.FromStageId == stage.WorkflowStageId
                        && t.ActionType == "Approve")
            .OrderBy(t => t.Priority)
            .FirstOrDefaultAsync();

        _db.WorkflowHistories.Add(new WorkflowHistory
        {
            Instance = instance,
            FromStageId = stage.WorkflowStageId,
            ToStageId = skipTransition?.ToStageId,
            ActionType = "AutoSkip",
            Comment = "این مرحله نیازی به بازبینی تشخیص داده نشد و به‌صورت خودکار رد شد.",
            ActionDate = DateTime.Now
        });

        if (skipTransition?.ToStageId == null)
        {
            instance.CurrentStageId = null;
            instance.Status = "Completed";
            instance.CompletionDate = DateTime.Now;
        }
        else
        {
            var afterSkipStage = await _db.WorkflowStages
                .FirstOrDefaultAsync(s => s.WorkflowStageId == skipTransition.ToStageId);

            if (afterSkipStage != null)
            {
                instance.CurrentStageId = afterSkipStage.WorkflowStageId;
                CreateInstanceStages(_db, instance, afterSkipStage);

                // ==============================================================================
                // اگر بعد از Skip، مرحله‌ی بعدی هم بدون عضو باشد (زنجیره‌ی چندتایی) - عمداً اینجا
                // متوقف می‌شویم (فقط یک سطح Skip خودکار). طبق محدودیت شناخته‌شده‌ی فعلی، فقط یک
                // مرحله‌ی Parallel پویا در هر گردش‌کار پشتیبانی می‌شود (IDC یا DisciplineReview) -
                // زنجیره‌ی چندسطحی نیاز به بازطراحی این متد به شکل بازگشتی دارد که فعلاً لازم نیست.
                // ==============================================================================
            }
        }
    }


    /// <summary>
    /// طبق ApprovalStrategy مرحله، مشخص می‌کند آیا مرحله کامل شده یا نه، و اگر شده، نتیجه‌ی
    /// نهایی‌اش (Approve/Reject) چیست. اگر هنوز کامل نشده null برمی‌گرداند.
    /// </summary>
    private static string? DetermineStageOutcome(WorkflowStage stage, List<WorkflowInstanceStage> allInstanceStages)
    {
        var pendingCount = allInstanceStages.Count(x => x.Status == "Pending");

        if (stage.StageType != "Parallel" || allInstanceStages.Count <= 1)
        {
            if (pendingCount > 0) return null;

            // فقط یک بازبین دارد - نتیجه‌ی مرحله دقیقاً همان اقدام واقعی اوست (Approve/Reject/
            // Skip/Return/...)، نه صرفاً فرض دوتایی Approve-یا-Reject
            return allInstanceStages.OrderByDescending(x => x.ActionDate).First().Status;
        }

        // رفع باگ بحرانی: قبلاً Comment هم مثل Reject حساب می‌شد و کل مرحله‌ی موازی رو (حتی
        // با یک نظر غیرمسدودکننده از یک عضو) رد می‌کرد. حالا فقط Reject واقعی مسدودکننده‌ست؛
        // Comment مثل تایید نرم عمل می‌کنه (پیشروی می‌ده) و خودِ متن نظر در WorkflowHistory
        // برای تصمیم‌گیرنده‌ی بعدی حفظ می‌شه.
        var rejectedCount = allInstanceStages.Count(x => x.Status == "Reject");
        var approvedCount = allInstanceStages.Count(x => x.Status == "Approve" || x.Status == "Comment");

        switch (stage.ApprovalStrategy)
        {
            case "FirstResponse":
                return rejectedCount > 0 ? "Reject" : (approvedCount > 0 ? "Approve" : null);

            case "MinimumCount":
                var minRequired = stage.MinimumApprovalCount ?? allInstanceStages.Count;
                if (rejectedCount > 0) return "Reject";
                return approvedCount >= minRequired ? "Approve" : (pendingCount == 0 ? "Reject" : null);

            case "AllApprovalsRequired":
            case "MandatoryRolesOnly": // ساده‌سازی فاز A: فعلاً معادل AllApprovalsRequired
            default:
                if (pendingCount > 0) return null;
                return rejectedCount > 0 ? "Reject" : "Approve";
        }
    }


    public async Task<WorkflowInstanceDto?> GetInstanceAsync(string businessObjectType, int businessObjectId)
    {
        var instance = await _db.WorkflowInstances
            .Where(x => x.BusinessObjectType == businessObjectType && x.BusinessObjectId == businessObjectId)
            .OrderByDescending(x => x.StartDate)
            .FirstOrDefaultAsync();

        return instance == null ? null : await MapInstanceAsync(instance);
    }

    public async Task<WorkflowInstanceDto?> GetInstanceByIdAsync(int workflowInstanceId)
    {
        var instance = await _db.WorkflowInstances
            .FirstOrDefaultAsync(x => x.WorkflowInstanceId == workflowInstanceId);

        return instance == null ? null : await MapInstanceAsync(instance);
    }

    private async Task<WorkflowInstanceDto> MapInstanceAsync(WorkflowInstance instance)
    {
        var currentStageName = instance.CurrentStageId == null
            ? null
            : await _db.WorkflowStages
                .Where(s => s.WorkflowStageId == instance.CurrentStageId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync();

        var pendingStages = await _db.WorkflowInstanceStages
            .Where(x => x.WorkflowInstanceId == instance.WorkflowInstanceId && x.Status == "Pending")
            .Select(x => new WorkflowInstanceStageDto
            {
                WorkflowInstanceStageId = x.WorkflowInstanceStageId,
                AssignedToEmployeeId = x.AssignedToEmployeeId,
                AssignedToRole = x.AssignedToRole,
                Status = x.Status,
                StageName = currentStageName ?? "",
                AssignedDate = x.AssignedDate
            })
            .ToListAsync();

        return new WorkflowInstanceDto
        {
            WorkflowInstanceId = instance.WorkflowInstanceId,
            Module = instance.Module,
            BusinessObjectType = instance.BusinessObjectType,
            BusinessObjectId = instance.BusinessObjectId,
            Status = instance.Status,
            CurrentStageId = instance.CurrentStageId,
            CurrentStageName = currentStageName,
            StartDate = instance.StartDate,
            CompletionDate = instance.CompletionDate,
            PendingStages = pendingStages
        };
    }


    public async Task<IReadOnlyList<WorkflowHistoryDto>> GetHistoryAsync(int workflowInstanceId)
    {
        var stageNames = await _db.WorkflowStages.ToDictionaryAsync(s => s.WorkflowStageId, s => s.Name);

        var history = await _db.WorkflowHistories
            .Where(x => x.WorkflowInstanceId == workflowInstanceId)
            .OrderBy(x => x.ActionDate)
            .ToListAsync();

        return history.Select(h => new WorkflowHistoryDto
        {
            FromStageName = h.FromStageId.HasValue ? stageNames.GetValueOrDefault(h.FromStageId.Value) : null,
            ToStageName = h.ToStageId.HasValue ? stageNames.GetValueOrDefault(h.ToStageId.Value) : null,
            ActionType = h.ActionType,
            Decision = h.Decision,
            Comment = h.Comment,
            UserId = h.UserId,
            Role = h.Role,
            ActionDate = h.ActionDate,
            IPAddress = h.IPAddress
        }).ToList();
    }


    public async Task<IReadOnlyList<PendingTaskDto>> GetPendingTasksAsync(int employeeId, IReadOnlyList<string> actorRoles)
    {
        var candidates = await (
            from instStage in _db.WorkflowInstanceStages
            join instance in _db.WorkflowInstances on instStage.WorkflowInstanceId equals instance.WorkflowInstanceId
            join stage in _db.WorkflowStages on instStage.WorkflowStageId equals stage.WorkflowStageId
            where instStage.Status == "Pending" && instance.Status == "Running"
            select new
            {
                instStage.AssignedToRole,
                Task = new PendingTaskDto
                {
                    WorkflowInstanceId = instance.WorkflowInstanceId,
                    WorkflowInstanceStageId = instStage.WorkflowInstanceStageId,
                    Module = instance.Module,
                    BusinessObjectType = instance.BusinessObjectType,
                    BusinessObjectId = instance.BusinessObjectId,
                    StageName = stage.Name,
                    AssignedDate = instStage.AssignedDate
                }
            }
        ).ToListAsync();

        return candidates
            .Where(x => RoleMatches(x.AssignedToRole, actorRoles))
            .Select(x => x.Task)
            .ToList();
    }
}
