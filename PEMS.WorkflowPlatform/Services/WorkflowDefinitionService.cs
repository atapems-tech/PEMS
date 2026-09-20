using Microsoft.EntityFrameworkCore;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.WorkflowPlatform.Services;

/// <summary>ثبت لاگ ممیزی پیکربندی در همان Unit-of-work (با همان SaveChanges تغییر اصلی ذخیره می‌شود).</summary>
internal static class WorkflowConfigAuditWriter
{
    public static void Add(IWorkflowDbContext db, string action, WorkflowActor actor, int? definitionId = null, int? versionId = null,
        int? bindingId = null, string? previous = null, string? current = null, string? details = null)
        => db.WorkflowConfigAudits.Add(new WorkflowConfigAudit
        {
            Action = action, ActorUserId = actor.UserId, ActorName = actor.Name, Timestamp = DateTime.Now,
            WorkflowDefinitionId = definitionId, WorkflowVersionId = versionId, WorkflowBindingId = bindingId,
            PreviousState = previous, CurrentState = current, Details = details
        });
}

/// <summary>
/// چرخه‌ی عمر تعریف/نسخه: Draft → Validated → Published → Retired. همه‌ی تغییرات ساختاری فقط روی
/// Draft/Validated مجازند (ویرایش Validated آن را به Draft برمی‌گرداند). نسخه‌ی Published تغییرناپذیر است؛
/// برای تغییر باید Clone/Draft جدید ساخت.
/// </summary>
public class WorkflowDefinitionService : IWorkflowDefinitionService
{
    private readonly IWorkflowDbContext _db;
    private readonly IWorkflowVersionValidator _validator;

    public WorkflowDefinitionService(IWorkflowDbContext db, IWorkflowVersionValidator? validator = null)
    {
        _db = db;
        _validator = validator ?? new WorkflowVersionValidator(db);
    }

    public async Task<WorkflowDefinition> CreateDefinitionAsync(string name, string category, string module, string? description, WorkflowActor actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(module))
            throw new ArgumentException("Name, Category و Module الزامی هستند.");
        if (await _db.WorkflowDefinitions.AnyAsync(d => d.Module == module && d.Category == category && d.Name == name, ct))
            throw new InvalidOperationException("تعریفی با همین Module/Category/Name وجود دارد.");

        var def = new WorkflowDefinition { Name = name.Trim(), Category = category.Trim(), Module = module.Trim(), Description = description, IsActive = true, CreatedById = actor.UserId };
        _db.WorkflowDefinitions.Add(def);
        await _db.SaveChangesAsync(ct);
        WorkflowConfigAuditWriter.Add(_db, WorkflowConfigAuditActions.DefinitionCreated, actor, def.WorkflowDefinitionId, current: $"{def.Module}/{def.Category}/{def.Name}");
        await _db.SaveChangesAsync(ct);
        return def;
    }

    private async Task<int> NextVersionNumberAsync(int definitionId, CancellationToken ct)
        => (await _db.WorkflowVersions.Where(v => v.WorkflowDefinitionId == definitionId).MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0) + 1;

    private async Task EnsureNoOpenDraftAsync(int definitionId, CancellationToken ct)
    {
        if (await _db.WorkflowVersions.AnyAsync(v => v.WorkflowDefinitionId == definitionId
                && (v.Status == WorkflowVersionStatus.Draft || v.Status == WorkflowVersionStatus.Validated), ct))
            throw new InvalidOperationException("این تعریف از قبل یک نسخه‌ی Draft/Validated باز دارد؛ ابتدا آن را منتشر یا رها کنید.");
    }

    public async Task<WorkflowVersion> CreateDraftVersionAsync(int definitionId, WorkflowActor actor, string? changeSummary = null, CancellationToken ct = default)
    {
        var def = await _db.WorkflowDefinitions.FirstOrDefaultAsync(d => d.WorkflowDefinitionId == definitionId, ct)
                  ?? throw new InvalidOperationException("تعریف یافت نشد.");
        if (!def.IsActive) throw new InvalidOperationException("تعریف بایگانی شده است.");
        await EnsureNoOpenDraftAsync(definitionId, ct);

        var v = new WorkflowVersion { WorkflowDefinitionId = definitionId, VersionNumber = await NextVersionNumberAsync(definitionId, ct), Status = WorkflowVersionStatus.Draft, ChangeSummary = changeSummary };
        _db.WorkflowVersions.Add(v);
        await _db.SaveChangesAsync(ct);
        WorkflowConfigAuditWriter.Add(_db, WorkflowConfigAuditActions.VersionCreated, actor, definitionId, v.WorkflowVersionId, current: $"v{v.VersionNumber} Draft", details: changeSummary);
        await _db.SaveChangesAsync(ct);
        return v;
    }

    public async Task<WorkflowVersion> CloneVersionAsync(int sourceVersionId, WorkflowActor actor, string? changeSummary = null, CancellationToken ct = default)
    {
        var src = await _db.WorkflowVersions.AsNoTracking().FirstOrDefaultAsync(v => v.WorkflowVersionId == sourceVersionId, ct)
                  ?? throw new InvalidOperationException("نسخه‌ی مبدأ یافت نشد.");
        var def = await _db.WorkflowDefinitions.FirstAsync(d => d.WorkflowDefinitionId == src.WorkflowDefinitionId, ct);
        if (!def.IsActive) throw new InvalidOperationException("تعریف بایگانی شده است.");
        await EnsureNoOpenDraftAsync(src.WorkflowDefinitionId, ct);

        var nv = await CopyStructureAsync(src, src.WorkflowDefinitionId, await NextVersionNumberAsync(src.WorkflowDefinitionId, ct), changeSummary, ct);
        WorkflowConfigAuditWriter.Add(_db, WorkflowConfigAuditActions.VersionCloned, actor, src.WorkflowDefinitionId, nv.WorkflowVersionId,
            previous: $"v{src.VersionNumber} {src.Status}", current: $"v{nv.VersionNumber} Draft", details: changeSummary);
        await _db.SaveChangesAsync(ct);
        return nv;
    }

    public async Task<WorkflowDefinition> CloneDefinitionAsync(int sourceDefinitionId, string newName, WorkflowActor actor, CancellationToken ct = default)
    {
        var src = await _db.WorkflowDefinitions.AsNoTracking().FirstOrDefaultAsync(d => d.WorkflowDefinitionId == sourceDefinitionId, ct)
                  ?? throw new InvalidOperationException("تعریف مبدأ یافت نشد.");
        var srcVersion = await _db.WorkflowVersions.AsNoTracking().Where(v => v.WorkflowDefinitionId == sourceDefinitionId)
                             .OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync(ct)
                         ?? throw new InvalidOperationException("تعریف مبدأ هیچ نسخه‌ای ندارد.");

        var def = await CreateDefinitionAsync(newName, src.Category, src.Module, src.Description, actor, ct);
        var nv = await CopyStructureAsync(srcVersion, def.WorkflowDefinitionId, 1, $"Cloned from definition {src.WorkflowDefinitionId} v{srcVersion.VersionNumber}", ct);
        WorkflowConfigAuditWriter.Add(_db, WorkflowConfigAuditActions.DefinitionCloned, actor, def.WorkflowDefinitionId, nv.WorkflowVersionId,
            previous: $"{src.WorkflowDefinitionId}", current: $"{def.WorkflowDefinitionId}");
        await _db.SaveChangesAsync(ct);
        return def;
    }

    /// <summary>کپی ساختار با نگاشت شناسه‌ها. فقط Stage/Transition (شامل تنظیمات تایید، نقش/کاربر، SLA، Escalation، اعلان، شرط، مجوز).</summary>
    private async Task<WorkflowVersion> CopyStructureAsync(WorkflowVersion src, int targetDefinitionId, int number, string? summary, CancellationToken ct)
    {
        var stages = await _db.WorkflowStages.AsNoTracking().Where(s => s.WorkflowVersionId == src.WorkflowVersionId).ToListAsync(ct);
        var transitions = await _db.WorkflowTransitions.AsNoTracking().Where(t => t.WorkflowVersionId == src.WorkflowVersionId).ToListAsync(ct);

        var nv = new WorkflowVersion
        {
            WorkflowDefinitionId = targetDefinitionId, VersionNumber = number, Status = WorkflowVersionStatus.Draft,
            ClonedFromVersionId = src.WorkflowVersionId, ChangeSummary = summary
        };
        var map = new Dictionary<int, WorkflowStage>();
        foreach (var s in stages)
        {
            var ns = new WorkflowStage
            {
                Version = nv, Name = s.Name, StageOrder = s.StageOrder, StageType = s.StageType, ResponsibleRole = s.ResponsibleRole,
                ApprovalStrategy = s.ApprovalStrategy, MinimumApprovalCount = s.MinimumApprovalCount, IsMandatory = s.IsMandatory,
                IsSkippable = s.IsSkippable, IsDelegatable = s.IsDelegatable, IsEscalatable = s.IsEscalatable, TimeoutHours = s.TimeoutHours,
                Color = s.Color, Icon = s.Icon, Description = s.Description, ResponsibleEmployeeId = s.ResponsibleEmployeeId,
                EscalationRole = s.EscalationRole, NotificationKey = s.NotificationKey, NameFa = s.NameFa, BusinessStatus = s.BusinessStatus
            };
            map[s.WorkflowStageId] = ns;
            _db.WorkflowStages.Add(ns);
        }
        foreach (var t in transitions)
        {
            _db.WorkflowTransitions.Add(new WorkflowTransition
            {
                Version = nv,
                FromStage = t.FromStageId.HasValue && map.TryGetValue(t.FromStageId.Value, out var f) ? f : null,
                ToStage = t.ToStageId.HasValue && map.TryGetValue(t.ToStageId.Value, out var to) ? to : null,
                ActionType = t.ActionType, ConditionExpression = t.ConditionExpression, Priority = t.Priority,
                RequiredPermission = t.RequiredPermission, ForbidInitiator = t.ForbidInitiator, Label = t.Label, LabelFa = t.LabelFa
            });
        }
        _db.WorkflowVersions.Add(nv);
        await _db.SaveChangesAsync(ct);
        return nv;
    }

    // ---------------- Draft editing ----------------
    private async Task<WorkflowVersion> EditableVersionAsync(int versionId, CancellationToken ct)
    {
        var v = await _db.WorkflowVersions.FirstOrDefaultAsync(x => x.WorkflowVersionId == versionId, ct)
                ?? throw new InvalidOperationException("نسخه یافت نشد.");
        if (WorkflowVersionStatus.IsImmutable(v.Status))
            throw new WorkflowImmutableVersionException($"نسخه‌ی {v.Status} تغییرناپذیر است. یک Draft جدید بسازید (CloneVersion).");
        return v;
    }

    private void ReopenIfValidated(WorkflowVersion v, WorkflowActor actor, string what)
    {
        var prev = v.Status;
        if (prev == WorkflowVersionStatus.Validated) { v.Status = WorkflowVersionStatus.Draft; v.ValidatedDate = null; v.ValidatedById = null; }
        WorkflowConfigAuditWriter.Add(_db, WorkflowConfigAuditActions.DraftEdited, actor, v.WorkflowDefinitionId, v.WorkflowVersionId,
            previous: prev, current: v.Status, details: what);
    }

    private static void Apply(WorkflowStage s, WorkflowStageInput i)
    {
        s.Name = i.Name?.Trim() ?? ""; s.StageOrder = i.StageOrder; s.StageType = i.StageType; s.ResponsibleRole = i.ResponsibleRole;
        s.ResponsibleEmployeeId = i.ResponsibleEmployeeId; s.ApprovalStrategy = i.ApprovalStrategy; s.MinimumApprovalCount = i.MinimumApprovalCount;
        s.IsMandatory = i.IsMandatory; s.IsSkippable = i.IsSkippable; s.IsDelegatable = i.IsDelegatable; s.IsEscalatable = i.IsEscalatable;
        s.TimeoutHours = i.TimeoutHours; s.EscalationRole = i.EscalationRole; s.NotificationKey = i.NotificationKey; s.Description = i.Description; s.NameFa = i.NameFa; s.BusinessStatus = i.BusinessStatus;
    }

    public async Task<WorkflowStage> AddStageAsync(int versionId, WorkflowStageInput input, WorkflowActor actor, CancellationToken ct = default)
    {
        var v = await EditableVersionAsync(versionId, ct);
        var s = new WorkflowStage { WorkflowVersionId = versionId };
        Apply(s, input);
        _db.WorkflowStages.Add(s);
        ReopenIfValidated(v, actor, $"AddStage:{s.Name}");
        await _db.SaveChangesAsync(ct);
        return s;
    }

    public async Task<WorkflowStage> UpdateStageAsync(int stageId, WorkflowStageInput input, WorkflowActor actor, CancellationToken ct = default)
    {
        var s = await _db.WorkflowStages.FirstOrDefaultAsync(x => x.WorkflowStageId == stageId, ct) ?? throw new InvalidOperationException("مرحله یافت نشد.");
        var v = await EditableVersionAsync(s.WorkflowVersionId, ct);
        Apply(s, input);
        ReopenIfValidated(v, actor, $"UpdateStage:{s.Name}");
        await _db.SaveChangesAsync(ct);
        return s;
    }

    public async Task RemoveStageAsync(int stageId, WorkflowActor actor, CancellationToken ct = default)
    {
        var s = await _db.WorkflowStages.FirstOrDefaultAsync(x => x.WorkflowStageId == stageId, ct) ?? throw new InvalidOperationException("مرحله یافت نشد.");
        var v = await EditableVersionAsync(s.WorkflowVersionId, ct);
        var trs = await _db.WorkflowTransitions.Where(t => t.FromStageId == stageId || t.ToStageId == stageId).ToListAsync(ct);
        _db.WorkflowTransitions.RemoveRange(trs);
        _db.WorkflowStages.Remove(s);
        ReopenIfValidated(v, actor, $"RemoveStage:{s.Name} (+{trs.Count} transitions)");
        await _db.SaveChangesAsync(ct);
    }

    public async Task<WorkflowTransition> AddTransitionAsync(int versionId, WorkflowTransitionInput input, WorkflowActor actor, CancellationToken ct = default)
    {
        var v = await EditableVersionAsync(versionId, ct);
        var ids = new[] { input.FromStageId, input.ToStageId }.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var ok = await _db.WorkflowStages.CountAsync(s => s.WorkflowVersionId == versionId && ids.Contains(s.WorkflowStageId), ct);
        if (ok != ids.Count) throw new InvalidOperationException("مرحله‌ی مبدأ/مقصد گذار متعلق به این نسخه نیست.");

        var t = new WorkflowTransition
        {
            WorkflowVersionId = versionId, FromStageId = input.FromStageId, ToStageId = input.ToStageId, ActionType = input.ActionType,
            ConditionExpression = input.ConditionExpression, Priority = input.Priority, RequiredPermission = input.RequiredPermission,
            ForbidInitiator = input.ForbidInitiator, Label = input.Label, LabelFa = input.LabelFa
        };
        _db.WorkflowTransitions.Add(t);
        ReopenIfValidated(v, actor, $"AddTransition:{input.ActionType}");
        await _db.SaveChangesAsync(ct);
        return t;
    }

    public async Task RemoveTransitionAsync(int transitionId, WorkflowActor actor, CancellationToken ct = default)
    {
        var t = await _db.WorkflowTransitions.FirstOrDefaultAsync(x => x.WorkflowTransitionId == transitionId, ct) ?? throw new InvalidOperationException("گذار یافت نشد.");
        var v = await EditableVersionAsync(t.WorkflowVersionId, ct);
        _db.WorkflowTransitions.Remove(t);
        ReopenIfValidated(v, actor, $"RemoveTransition:{t.ActionType}");
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- Lifecycle ----------------
    public async Task<WorkflowValidationResult> ValidateVersionAsync(int versionId, WorkflowActor actor, CancellationToken ct = default)
    {
        var v = await EditableVersionAsync(versionId, ct);
        var result = await _validator.ValidateAsync(versionId, ct);
        if (result.IsValid)
        {
            var prev = v.Status;
            v.Status = WorkflowVersionStatus.Validated; v.ValidatedDate = DateTime.Now; v.ValidatedById = actor.UserId;
            WorkflowConfigAuditWriter.Add(_db, WorkflowConfigAuditActions.Validated, actor, v.WorkflowDefinitionId, versionId, previous: prev, current: v.Status,
                details: $"warnings={result.Warnings.Count()}");
        }
        else
        {
            WorkflowConfigAuditWriter.Add(_db, WorkflowConfigAuditActions.ValidationFailed, actor, v.WorkflowDefinitionId, versionId, previous: v.Status, current: v.Status,
                details: string.Join("; ", result.Errors.Take(10).Select(e => e.Code)));
        }
        await _db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<WorkflowVersion> PublishVersionAsync(int versionId, WorkflowActor actor, DateTime? effectiveDate = null, CancellationToken ct = default)
    {
        var v = await _db.WorkflowVersions.FirstOrDefaultAsync(x => x.WorkflowVersionId == versionId, ct) ?? throw new InvalidOperationException("نسخه یافت نشد.");
        if (v.Status != WorkflowVersionStatus.Validated)
            throw new InvalidOperationException($"فقط نسخه‌ی Validated قابل انتشار است (وضعیت فعلی: {v.Status}).");

        var result = await _validator.ValidateAsync(versionId, ct);
        if (!result.IsValid)
            throw new InvalidOperationException("اعتبارسنجی در لحظه‌ی انتشار ناموفق بود: " + string.Join("; ", result.Errors.Select(e => e.Code + ": " + e.Message)));

        v.Status = WorkflowVersionStatus.Published; v.PublishedDate = DateTime.Now; v.PublishedById = actor.UserId; v.EffectiveDate = effectiveDate ?? DateTime.Now;
        WorkflowConfigAuditWriter.Add(_db, WorkflowConfigAuditActions.Published, actor, v.WorkflowDefinitionId, versionId, previous: WorkflowVersionStatus.Validated, current: v.Status, details: $"v{v.VersionNumber}");
        await _db.SaveChangesAsync(ct);
        return v;
    }

    public async Task<WorkflowVersion> RetireVersionAsync(int versionId, WorkflowActor actor, CancellationToken ct = default)
    {
        var v = await _db.WorkflowVersions.FirstOrDefaultAsync(x => x.WorkflowVersionId == versionId, ct) ?? throw new InvalidOperationException("نسخه یافت نشد.");
        if (v.Status != WorkflowVersionStatus.Published) throw new InvalidOperationException("فقط نسخه‌ی Published قابل Retire است.");
        if (await _db.WorkflowBindings.AnyAsync(b => b.IsActive && b.PinnedVersionId == versionId, ct))
            throw new InvalidOperationException("Binding فعالی به این نسخه پین شده است؛ ابتدا Binding را تغییر دهید.");

        v.Status = WorkflowVersionStatus.Retired; v.RetiredDate = DateTime.Now;
        WorkflowConfigAuditWriter.Add(_db, WorkflowConfigAuditActions.Retired, actor, v.WorkflowDefinitionId, versionId, previous: WorkflowVersionStatus.Published, current: v.Status);
        await _db.SaveChangesAsync(ct);
        return v;
    }

    public async Task ArchiveDefinitionAsync(int definitionId, WorkflowActor actor, CancellationToken ct = default)
    {
        var d = await _db.WorkflowDefinitions.FirstOrDefaultAsync(x => x.WorkflowDefinitionId == definitionId, ct) ?? throw new InvalidOperationException("تعریف یافت نشد.");
        if (await _db.WorkflowBindings.AnyAsync(b => b.IsActive && b.WorkflowDefinitionId == definitionId, ct))
            throw new InvalidOperationException("Binding فعالی به این تعریف وصل است؛ ابتدا آن را غیرفعال کنید.");
        var prev = d.IsActive ? "Active" : "Archived";
        d.IsActive = false;
        WorkflowConfigAuditWriter.Add(_db, WorkflowConfigAuditActions.DefinitionArchived, actor, definitionId, previous: prev, current: "Archived");
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowConfigAudit>> GetAuditAsync(int? definitionId = null, int? versionId = null, CancellationToken ct = default)
    {
        var q = _db.WorkflowConfigAudits.AsNoTracking().AsQueryable();
        if (definitionId.HasValue) q = q.Where(a => a.WorkflowDefinitionId == definitionId);
        if (versionId.HasValue) q = q.Where(a => a.WorkflowVersionId == versionId);
        return await q.OrderBy(a => a.WorkflowConfigAuditId).ToListAsync(ct);
    }
}
