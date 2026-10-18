using Microsoft.EntityFrameworkCore;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.WorkflowPlatform.Services;

/// <summary>
/// مدل خواندنی مشترک برای UI: مراحل از روی «نسخه‌ی پین‌شده‌ی Instance» خوانده می‌شوند (نه ثابت‌نویسی در View)،
/// بنابراین اضافه شدن مثلاً «Reliability Review» در نسخه‌ی جدید خودکار نمایش داده می‌شود.
/// اکشن‌های در دسترس سمت سرور و از روی وضعیت جاری، گذارها، نقش/کاربر، SoD، شرط‌ها و مجوز محاسبه می‌شوند.
/// SLA فقط محاسبه/نمایش است (سررسید/عقب‌افتاده)؛ Escalation خودکار اجرا نمی‌شود.
/// </summary>
public class WorkflowReadModelService : IWorkflowReadModelService
{
    private readonly IWorkflowDbContext _db;
    private readonly IWorkflowConditionEvaluator _conditions;
    private readonly IWorkflowAuthorizationHook? _authHook;
    private readonly IWorkflowActionRegistry _registry;
    private readonly Func<DateTime> _clock;

    public WorkflowReadModelService(IWorkflowDbContext db, IWorkflowConditionEvaluator? conditions = null,
        IWorkflowAuthorizationHook? authorizationHook = null, IWorkflowActionRegistry? registry = null, Func<DateTime>? clock = null)
    {
        _db = db;
        _conditions = conditions ?? new WorkflowConditionEvaluator();
        _authHook = authorizationHook;
        _registry = registry ?? new WorkflowActionRegistry();
        _clock = clock ?? (() => DateTime.Now);
    }

    public async Task<IReadOnlyList<WorkflowInstance>> FindInstancesForEntityAsync(string module, string businessObjectType, int businessObjectId, CancellationToken ct = default)
        => await _db.WorkflowInstances.AsNoTracking()
            .Where(i => i.Module == module && i.BusinessObjectType == businessObjectType && i.BusinessObjectId == businessObjectId)
            .OrderByDescending(i => i.StartDate).ThenByDescending(i => i.WorkflowInstanceId).ToListAsync(ct);

    public async Task<WorkflowInstance?> GetCurrentInstanceForEntityAsync(string module, string businessObjectType, int businessObjectId, CancellationToken ct = default)
    {
        var all = await FindInstancesForEntityAsync(module, businessObjectType, businessObjectId, ct);
        return all.FirstOrDefault(i => i.Status == "Running") ?? all.FirstOrDefault();
    }

    public async Task<WorkflowRuntimeViewModel?> GetRuntimeViewForEntityAsync(string module, string businessObjectType, int businessObjectId, WorkflowUserContext user, CancellationToken ct = default)
    {
        var inst = await GetCurrentInstanceForEntityAsync(module, businessObjectType, businessObjectId, ct);
        return inst == null ? null : await GetRuntimeViewAsync(inst.WorkflowInstanceId, user, ct);
    }

    public async Task<WorkflowRuntimeViewModel?> GetRuntimeViewAsync(int workflowInstanceId, WorkflowUserContext user, CancellationToken ct = default)
    {
        var instance = await _db.WorkflowInstances.AsNoTracking().FirstOrDefaultAsync(i => i.WorkflowInstanceId == workflowInstanceId, ct);
        if (instance == null) return null;

        var version = await _db.WorkflowVersions.AsNoTracking().FirstAsync(v => v.WorkflowVersionId == instance.WorkflowVersionId, ct);
        var def = await _db.WorkflowDefinitions.AsNoTracking().FirstAsync(d => d.WorkflowDefinitionId == version.WorkflowDefinitionId, ct);
        var stages = await _db.WorkflowStages.AsNoTracking().Where(s => s.WorkflowVersionId == version.WorkflowVersionId)
            .OrderBy(s => s.StageOrder).ThenBy(s => s.WorkflowStageId).ToListAsync(ct);
        var instStages = await _db.WorkflowInstanceStages.AsNoTracking().Where(x => x.WorkflowInstanceId == workflowInstanceId).ToListAsync(ct);
        var history = await _db.WorkflowHistories.AsNoTracking().Where(h => h.WorkflowInstanceId == workflowInstanceId)
            .OrderBy(h => h.ActionDate).ThenBy(h => h.WorkflowHistoryId).ToListAsync(ct);
        var names = stages.ToDictionary(s => s.WorkflowStageId, s => s.Name);
        var now = _clock();

        var vm = new WorkflowRuntimeViewModel
        {
            WorkflowInstanceId = instance.WorkflowInstanceId, WorkflowDefinitionId = def.WorkflowDefinitionId, WorkflowName = def.Name,
            WorkflowVersionId = version.WorkflowVersionId, VersionNumber = version.VersionNumber, Status = instance.Status,
            CurrentStageId = instance.CurrentStageId, CurrentStageName = instance.CurrentStageId.HasValue ? names.GetValueOrDefault(instance.CurrentStageId.Value) : null,
            StartDate = instance.StartDate, CompletionDate = instance.CompletionDate
        };

        // مرحله‌ای که Instance در آن لغو شد (اکشن Cancel)؛ «Completed» نشان داده نمی‌شود
        var cancelledAt = instance.Status == "Cancelled" ? history.LastOrDefault(h => h.ActionType == "Cancel")?.FromStageId : null;
        var visited = history.SelectMany(h => new[] { h.FromStageId, h.ToStageId }).Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
        foreach (var s in stages)
        {
            var mine = instStages.Where(x => x.WorkflowStageId == s.WorkflowStageId).OrderBy(x => x.AssignedDate).ToList();
            var isCurrent = instance.Status == "Running" && instance.CurrentStageId == s.WorkflowStageId;
            var autoSkipped = history.Any(h => h.ActionType == "AutoSkip" && h.FromStageId == s.WorkflowStageId);
            var rejected = mine.Any(x => x.Status == "Reject");

            string status;
            if (cancelledAt == s.WorkflowStageId) status = "Cancelled";
            else if (isCurrent) status = "Current";
            else if (autoSkipped) status = "Skipped";
            else if (rejected) status = "Rejected";
            else if (visited.Contains(s.WorkflowStageId)) status = "Completed";
            else status = "Upcoming";

            var dueBase = mine.Where(x => x.Status == "Pending").Select(x => (DateTime?)x.AssignedDate).Min();
            var due = s.TimeoutHours.HasValue && dueBase.HasValue ? dueBase.Value.AddHours(s.TimeoutHours.Value) : (DateTime?)null;

            vm.Stages.Add(new WorkflowStageView
            {
                StageId = s.WorkflowStageId, Name = s.Name, NameFa = s.NameFa, BusinessStatus = s.BusinessStatus, Order = s.StageOrder, StageType = s.StageType, Status = status, IsMandatory = s.IsMandatory,
                ResponsibleRole = s.ResponsibleRole, ResponsibleEmployeeId = s.ResponsibleEmployeeId, ApprovalStrategy = s.ApprovalStrategy,
                MinimumApprovalCount = s.MinimumApprovalCount,
                ApprovedCount = mine.Count(x => x.Status is "Approve" or "Comment"), PendingCount = mine.Count(x => x.Status == "Pending"),
                Participants = mine.Select(x => new WorkflowParticipantView
                {
                    Role = x.AssignedToRole, EmployeeId = x.AssignedToEmployeeId, Status = x.Status, Decision = x.Decision,
                    AssignedDate = x.AssignedDate, ActionDate = x.ActionDate
                }).ToList(),
                Sla = new WorkflowSlaView
                {
                    TimeoutHours = s.TimeoutHours, DueDate = due, IsOverdue = isCurrent && due.HasValue && due.Value < now,
                    IsEscalatable = s.IsEscalatable, EscalationRole = s.EscalationRole, EscalationExecuted = false
                }
            });
        }

        vm.HistorySummary = history.Select(h => new WorkflowHistorySummaryItem
        {
            ActionDate = h.ActionDate, ActionType = h.ActionType, UserId = h.UserId, Comment = h.Comment,
            FromStageName = h.FromStageId.HasValue ? names.GetValueOrDefault(h.FromStageId.Value) : null,
            ToStageName = h.ToStageId.HasValue ? names.GetValueOrDefault(h.ToStageId.Value) : null
        }).ToList();

        if (instance.Status == "Running" && vm.Stages.Any(s => s.Status == "Current" && s.PendingCount == 0))
            vm.Warnings.Add("مرحله‌ی جاری هیچ شرکت‌کننده‌ی در انتظاری ندارد.");
        if (vm.Stages.Any(s => s.Sla.IsOverdue))
            vm.Warnings.Add("SLA مرحله‌ی جاری از مهلت گذشته است (Escalation خودکار اجرا نمی‌شود).");
        if (WorkflowVersionStatus.IsImmutable(version.Status) == false)
            vm.Warnings.Add($"Instance به نسخه‌ای با وضعیت {version.Status} پین شده است.");
        if (instance.WorkflowBindingId == null)
            vm.Warnings.Add("این Instance از مسیر Legacy (بدون Binding صریح) شروع شده است.");

        vm.AvailableActions = (await GetAvailableActionsAsync(instance, version, stages, instStages, user, ct)).ToList();
        return vm;
    }

    public async Task<IReadOnlyList<WorkflowAvailableAction>> GetAvailableActionsAsync(int workflowInstanceId, WorkflowUserContext user, CancellationToken ct = default)
    {
        var instance = await _db.WorkflowInstances.AsNoTracking().FirstOrDefaultAsync(i => i.WorkflowInstanceId == workflowInstanceId, ct);
        if (instance == null) return Array.Empty<WorkflowAvailableAction>();
        var version = await _db.WorkflowVersions.AsNoTracking().FirstAsync(v => v.WorkflowVersionId == instance.WorkflowVersionId, ct);
        var stages = await _db.WorkflowStages.AsNoTracking().Where(s => s.WorkflowVersionId == version.WorkflowVersionId).ToListAsync(ct);
        var instStages = await _db.WorkflowInstanceStages.AsNoTracking().Where(x => x.WorkflowInstanceId == workflowInstanceId).ToListAsync(ct);
        return await GetAvailableActionsAsync(instance, version, stages, instStages, user, ct);
    }

    private async Task<IReadOnlyList<WorkflowAvailableAction>> GetAvailableActionsAsync(WorkflowInstance instance, WorkflowVersion version,
        List<WorkflowStage> stages, List<WorkflowInstanceStage> instStages, WorkflowUserContext user, CancellationToken ct)
    {
        var result = new List<WorkflowAvailableAction>();
        if (instance.Status != "Running" || instance.CurrentStageId == null) return result;
        var stage = stages.FirstOrDefault(s => s.WorkflowStageId == instance.CurrentStageId);
        if (stage == null) return result;

        var mine = instStages.Any(x => x.WorkflowStageId == stage.WorkflowStageId && x.Status == "Pending"
                                       && x.AssignedToRole != null
                                       && x.AssignedToRole.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(user.Roles.Contains));
        if (!mine) return result; // فقط شرکت‌کننده‌ی در انتظار اکشن می‌بیند - همان قاعده‌ی اجرای Runtime

        var transitions = await _db.WorkflowTransitions.AsNoTracking()
            .Where(t => t.WorkflowVersionId == version.WorkflowVersionId && t.FromStageId == stage.WorkflowStageId)
            .OrderBy(t => t.Priority).ToListAsync(ct);
        var facts = WorkflowRuntimeService.Facts(instance, stage);
        var seen = new HashSet<string>();

        foreach (var t in transitions)
        {
            if (!seen.Add(t.ActionType)) continue;               // فقط گذار برنده‌ی هر اکشن (همان انتخاب Runtime)
            var desc = _registry.Find(t.ActionType, instance.Module);
            if (instance.WorkflowBindingId != null && (desc == null || desc.Kind != WorkflowActionKind.Outcome)) continue;

            var winner = transitions.Where(x => x.ActionType == t.ActionType).FirstOrDefault(x => _conditions.Evaluate(x.ConditionExpression, facts));
            if (winner == null) continue;                        // هیچ شرطی برقرار نیست

            var action = new WorkflowAvailableAction
            {
                ActionType = winner.ActionType, Label = winner.Label, LabelFa = winner.LabelFa, TransitionId = winner.WorkflowTransitionId, ToStageId = winner.ToStageId,
                ToStageName = winner.ToStageId.HasValue ? stages.FirstOrDefault(s => s.WorkflowStageId == winner.ToStageId)?.Name : null, IsAllowed = true
            };

            if (winner.ForbidInitiator && user.EmployeeId.HasValue && user.EmployeeId == instance.InitiatedById)
            { action.IsAllowed = false; action.DeniedReason = "SeparationOfDuties"; }
            else if (!string.IsNullOrWhiteSpace(winner.RequiredPermission))
            {
                if (_authHook == null) { action.IsAllowed = false; action.DeniedReason = "AuthorizationHookMissing"; }
                else
                {
                    var d = await _authHook.AuthorizeAsync(new WorkflowAuthorizationContext(instance, stage, winner, winner.ActionType, user.EmployeeId, user.Roles), ct);
                    if (!d.Allowed) { action.IsAllowed = false; action.DeniedReason = d.Reason ?? "PermissionDenied"; }
                }
            }
            result.Add(action);
        }
        return result;
    }
}
