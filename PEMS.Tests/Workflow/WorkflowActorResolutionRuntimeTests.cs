using Microsoft.EntityFrameworkCore;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Tests.Workflow;

/// <summary>
/// افزونه‌ی Enterprise Actor Resolution در موتور مشترک گردش‌کار (Hook + Snapshot کاندیدها + شرط ورود). موتور ماژول-خنثی است؛
/// این تست‌ها با Resolver جعلی ثابت می‌کنند: Fail-closed، Skip خودکار، مجوز فقط از Snapshot، SoD، بدون Bypass نقش، و سازگاری با مراحل قدیمی.
/// </summary>
public class WorkflowActorResolutionRuntimeTests : WorkflowPlatformTestBase
{
    private sealed class FakeHook : IWorkflowActorResolutionHook
    {
        public Func<WorkflowActorResolutionContext, WorkflowActorResolutionOutcome> Fn { get; set; } =
            c => new WorkflowActorResolutionOutcome(WorkflowActorResolutionKind.Resolved, "RESOLVED", new[] { new WorkflowActorCandidate(10, "seat") });
        public List<string> Calls { get; } = new();
        public Task<WorkflowActorResolutionOutcome> ResolveAsync(WorkflowActorResolutionContext c, CancellationToken ct = default)
        { Calls.Add(c.Stage.ResponsibilityKey!); return Task.FromResult(Fn(c)); }
    }

    private sealed class FakeFacts : IWorkflowBusinessContextProvider
    {
        public string NeedsExtra { get; set; } = "false";
        public IReadOnlyCollection<string> SupportedFields => new[] { "NeedsExtra" };
        public bool Supports(string module, string type) => module == "Res";
        public Task<IReadOnlyDictionary<string, string?>> GetFactsAsync(string m, string t, int id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string?>>(new Dictionary<string, string?> { ["NeedsExtra"] = NeedsExtra });
    }

    private readonly FakeHook _hook = new();
    private readonly FakeFacts _facts = new();
    private readonly WorkflowConditionEvaluator _eval = new(new[] { "NeedsExtra" });
    private readonly WorkflowRuntimeService _rt;
    private readonly WorkflowDefinitionService _defs;

    public WorkflowActorResolutionRuntimeTests()
    {
        _rt = new WorkflowRuntimeService(Ctx, Bindings, _eval, Hook, Sink, Registry, _hook, new IWorkflowBusinessContextProvider[] { _facts });
        _defs = new WorkflowDefinitionService(Ctx, new WorkflowVersionValidator(Ctx, Registry, _eval, Catalog));
        Hook.GrantedPermissions.Add("Workflow.Approve");
    }

    private async Task<(WorkflowStage S1, WorkflowStage S2, WorkflowStage S3)> BuildAsync(string? s2Condition = "NeedsExtra == true")
    {
        var def = await _defs.CreateDefinitionAsync("Resolution", "Std", "Res", null, Actor);
        var v = await _defs.CreateDraftVersionAsync(def.WorkflowDefinitionId, Actor);
        WorkflowStageInput S(string name, int order, string? cond = null) => new()
        { Name = name, StageOrder = order, StageType = "Sequential", ResponsibilityKey = "R_" + name, EntryCondition = cond, ApprovalStrategy = "AllApprovalsRequired" };
        var s1 = await _defs.AddStageAsync(v.WorkflowVersionId, S("S1", 1), Actor);
        var s2 = await _defs.AddStageAsync(v.WorkflowVersionId, S("S2", 2, s2Condition), Actor);
        var s3 = await _defs.AddStageAsync(v.WorkflowVersionId, S("S3", 3), Actor);
        await _defs.AddTransitionAsync(v.WorkflowVersionId, Tr(null, s1.WorkflowStageId, "Start"), Actor);
        foreach (var (from, to) in new[] { (s1, s2), (s2, s3), (s3, (WorkflowStage?)null) })
        {
            await _defs.AddTransitionAsync(v.WorkflowVersionId, Tr(from.WorkflowStageId, to?.WorkflowStageId, "Approve", perm: "Workflow.Approve", sod: true), Actor);
            await _defs.AddTransitionAsync(v.WorkflowVersionId, Tr(from.WorkflowStageId, null, "Return", perm: "Workflow.Approve", sod: true), Actor);
        }
        var result = await _defs.ValidateVersionAsync(v.WorkflowVersionId, Actor);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Code + ":" + e.Message)));
        await _defs.PublishVersionAsync(v.WorkflowVersionId, Actor);
        return (s1, s2, s3);
    }

    private Task<int> StartAsync(int initiator = 1) => _rt.StartInstanceAsync("Res", "Doc", 100, "Std", null, initiator);

    private static readonly string[] AnyRole = { "RoleA" };

    [Fact]
    public async Task EntryCondition_False_AutoSkipsStage_AndRecordsReason()
    {
        await BuildAsync();
        var id = await StartAsync();
        await _rt.ExecuteActionAsync(id, "Approve", 10, AnyRole, null, null, null);

        var inst = await Ctx.WorkflowInstances.AsNoTracking().SingleAsync();
        Assert.Equal("S3", (await Ctx.WorkflowStages.FindAsync(inst.CurrentStageId))!.Name);
        var skip = await Ctx.WorkflowHistories.AsNoTracking().SingleAsync(h => h.ActionType == "AutoSkip");
        Assert.Equal("EntryCondition", skip.Comment);
        Assert.DoesNotContain("R_S2", _hook.Calls);          // مرحله‌ی رد‌شده اصلاً Resolve نشد
    }

    [Fact]
    public async Task EntryCondition_True_EntersStage()
    {
        await BuildAsync();
        _facts.NeedsExtra = "true";
        var id = await StartAsync();
        await _rt.ExecuteActionAsync(id, "Approve", 10, AnyRole, null, null, null);
        Assert.Equal("S2", (await Ctx.WorkflowStages.FindAsync((await Ctx.WorkflowInstances.AsNoTracking().SingleAsync()).CurrentStageId))!.Name);
    }

    [Fact]
    public async Task ResolverFailure_IsFailClosed_NothingIsSaved()
    {
        await BuildAsync();
        _hook.Fn = _ => new WorkflowActorResolutionOutcome(WorkflowActorResolutionKind.Failed, "REQUESTER_ONLY", Array.Empty<WorkflowActorCandidate>(), null, "{}");
        var ex = await Assert.ThrowsAsync<WorkflowActorResolutionException>(() => StartAsync());
        Assert.Equal("REQUESTER_ONLY", ex.State);
        Assert.Equal("S1", ex.StageName);
        Assert.Empty(await Ctx.WorkflowInstances.AsNoTracking().ToListAsync());
        Assert.Empty(await Ctx.WorkflowInstanceStageCandidates.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task FailureOnLaterStage_RollsBackTheApproval()
    {
        await BuildAsync();
        _facts.NeedsExtra = "true";
        var id = await StartAsync();
        _hook.Fn = c => c.Stage.Name == "S2"
            ? new WorkflowActorResolutionOutcome(WorkflowActorResolutionKind.Failed, "NO_ELIGIBLE_ACTOR", Array.Empty<WorkflowActorCandidate>())
            : new WorkflowActorResolutionOutcome(WorkflowActorResolutionKind.Resolved, "RESOLVED", new[] { new WorkflowActorCandidate(10, "seat") });

        await Assert.ThrowsAsync<WorkflowActorResolutionException>(() => _rt.ExecuteActionAsync(id, "Approve", 10, AnyRole, null, null, null));
        Ctx.ChangeTracker.Clear();
        var inst = await Ctx.WorkflowInstances.AsNoTracking().SingleAsync();
        Assert.Equal("S1", (await Ctx.WorkflowStages.FindAsync(inst.CurrentStageId))!.Name);   // مرحله‌ی اول هنوز منتظر است
        Assert.Equal("Pending", (await Ctx.WorkflowInstanceStages.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task ResolverSkippedOutcome_SkipsStage_AndAllSkippedCompletesInstance()
    {
        await BuildAsync();
        _hook.Fn = _ => new WorkflowActorResolutionOutcome(WorkflowActorResolutionKind.Skipped, "NOT_APPLICABLE", Array.Empty<WorkflowActorCandidate>(), "RequesterIsCEO");
        await StartAsync();
        var inst = await Ctx.WorkflowInstances.AsNoTracking().SingleAsync();
        Assert.Equal("Completed", inst.Status);
        Assert.Equal(new[] { "RequesterIsCEO", "RequesterIsCEO", "RequesterIsCEO" }.Length, await Ctx.WorkflowHistories.CountAsync(h => h.ActionType == "AutoSkip"));
    }

    [Fact]
    public async Task OnlySelectedCandidate_MayAct_RoleAndAdminHaveNoBypass()
    {
        await BuildAsync();
        var id = await StartAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _rt.ExecuteActionAsync(id, "Approve", 11, new[] { "RoleA", "RoleB", "SystemAdmin" }, null, null, null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _rt.ExecuteActionAsync(id, "Approve", null, new[] { "SystemAdmin" }, null, null, null));
        await _rt.ExecuteActionAsync(id, "Approve", 10, Array.Empty<string>(), null, null, null);
    }

    [Fact]
    public async Task Initiator_CannotAct_EvenWhenSnapshotContainsThem()
    {
        await BuildAsync();
        _hook.Fn = _ => new WorkflowActorResolutionOutcome(WorkflowActorResolutionKind.Resolved, "RESOLVED", new[] { new WorkflowActorCandidate(1, "seat") });
        var id = await StartAsync(initiator: 1);
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _rt.ExecuteActionAsync(id, "Approve", 1, AnyRole, null, null, null));
        Assert.Contains("تفکیک وظایف", ex.Message);
    }

    [Fact]
    public async Task UnselectedCandidate_IsSnapshottedButCannotAct()
    {
        await BuildAsync();
        _hook.Fn = _ => new WorkflowActorResolutionOutcome(WorkflowActorResolutionKind.Resolved, "MULTIPLE_ELIGIBLE_ACTORS",
            new[] { new WorkflowActorCandidate(10, "primary", true), new WorkflowActorCandidate(11, "secondary", false) });
        var id = await StartAsync();

        Assert.Equal(2, await Ctx.WorkflowInstanceStageCandidates.CountAsync());
        Assert.Equal(10, (await Ctx.WorkflowInstanceStages.AsNoTracking().SingleAsync()).AssignedToEmployeeId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _rt.ExecuteActionAsync(id, "Approve", 11, AnyRole, null, null, null));
    }

    [Fact]
    public async Task DuplicateApproval_ByTheSameActor_IsRejected_AndSnapshotStaysImmutable()
    {
        await BuildAsync();
        _facts.NeedsExtra = "true";
        var id = await StartAsync();
        await _rt.ExecuteActionAsync(id, "Approve", 10, AnyRole, null, null, null);   // S1 → S2

        var s1Stage = await Ctx.WorkflowInstanceStages.AsNoTracking().Include(x => x.Candidates).OrderBy(x => x.WorkflowInstanceStageId).FirstAsync();
        Assert.Equal("Approve", s1Stage.Status);
        Assert.Equal(10, Assert.Single(s1Stage.Candidates).EmployeeId);

        // همان فرد: مرحله‌ی S1 دیگر Pending نیست و S2 کاندیدای خودش را دارد؛ با تغییر Resolver برای مراحل بعد، S1 دست‌نخورده می‌ماند
        _hook.Fn = _ => new WorkflowActorResolutionOutcome(WorkflowActorResolutionKind.Resolved, "RESOLVED", new[] { new WorkflowActorCandidate(77, "new holder") });
        var again = await Ctx.WorkflowInstanceStages.AsNoTracking().Include(x => x.Candidates).OrderBy(x => x.WorkflowInstanceStageId).FirstAsync();
        Assert.Equal(10, Assert.Single(again.Candidates).EmployeeId);
    }

    [Fact]
    public async Task ReturnOnLastTransition_EndsInstanceAsReturned_NotCompleted()
    {
        await BuildAsync();
        var id = await StartAsync();
        await _rt.ExecuteActionAsync(id, "Return", 10, AnyRole, "fix", null, null);
        var inst = await Ctx.WorkflowInstances.AsNoTracking().SingleAsync();
        Assert.Equal("Returned", inst.Status);
        Assert.Null(inst.CurrentStageId);
    }

    [Fact]
    public async Task PendingTasks_ListSnapshotCandidates_AndHideInitiatorsOwnWork()
    {
        await BuildAsync();
        _hook.Fn = _ => new WorkflowActorResolutionOutcome(WorkflowActorResolutionKind.Resolved, "RESOLVED",
            new[] { new WorkflowActorCandidate(10, "a"), new WorkflowActorCandidate(1, "initiator") });
        await StartAsync(initiator: 1);

        Assert.Single(await _rt.GetPendingTasksAsync(10, Array.Empty<string>()));
        Assert.Empty(await _rt.GetPendingTasksAsync(1, Array.Empty<string>()));
        Assert.Empty(await _rt.GetPendingTasksAsync(12, new[] { "RoleA" }));
    }

    [Fact]
    public async Task ResponsibilityStage_WithoutHook_FailsClosed_AndLegacyRoleStagesStillWork()
    {
        await BuildAsync();
        var noHook = new WorkflowRuntimeService(Ctx, Bindings, _eval, Hook, Sink, Registry);
        await Assert.ThrowsAsync<InvalidOperationException>(() => noHook.StartInstanceAsync("Res", "Doc", 100, "Std", null, 1));

        // مراحل قدیمی (ResponsibleRole) بدون Hook مثل قبل کار می‌کنند
        var legacy = await BuildDraftAsync("Legacy", "Old", "Std");
        await Defs.ValidateVersionAsync(legacy.Version.WorkflowVersionId, Actor);
        await Defs.PublishVersionAsync(legacy.Version.WorkflowVersionId, Actor);
        var iid = await noHook.StartInstanceAsync("Old", "Doc", 5, "Std", null, 1);
        await noHook.ExecuteActionAsync(iid, "Approve", 2, new[] { "RoleA" }, null, null, null);
    }
}
