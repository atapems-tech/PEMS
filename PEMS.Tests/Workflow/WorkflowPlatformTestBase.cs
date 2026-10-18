using PEMS.Persistence.Context;
using PEMS.Tests.TestSupport;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Tests.Workflow;

public sealed class FakeCatalog : IWorkflowReferenceCatalog
{
    public HashSet<string> Roles { get; } = new() { "RoleA", "RoleB", "Escalator" };
    public HashSet<int> Employees { get; } = new() { 1, 2, 3 };
    public HashSet<string> Permissions { get; } = new() { "Workflow.Approve" };
    public Task<bool> RoleExistsAsync(string role, CancellationToken ct = default) => Task.FromResult(Roles.Contains(role));
    public Task<bool> EmployeeExistsAsync(int id, CancellationToken ct = default) => Task.FromResult(Employees.Contains(id));
    public Task<bool> PermissionExistsAsync(string p, CancellationToken ct = default) => Task.FromResult(Permissions.Contains(p));
}

public sealed class FakeAuthHook : IWorkflowAuthorizationHook
{
    public HashSet<string> GrantedPermissions { get; } = new();
    public int Calls { get; private set; }
    public Task<WorkflowAuthorizationResult> AuthorizeAsync(WorkflowAuthorizationContext c, CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult(GrantedPermissions.Contains(c.Transition.RequiredPermission ?? "")
            ? WorkflowAuthorizationResult.Allow() : WorkflowAuthorizationResult.Deny("NoPermission"));
    }
}

public sealed class RecordingSink : IWorkflowEventSink
{
    public List<WorkflowRuntimeEvent> Events { get; } = new();
    public Task OnEventAsync(WorkflowRuntimeEvent evt, CancellationToken ct = default) { Events.Add(evt); return Task.CompletedTask; }
}

/// <summary>پایه‌ی تست‌های Phase 1: DbContext مجزا (InMemory) + سرویس‌ها + ساخت گردش‌کار استاندارد دو مرحله‌ای.</summary>
public abstract class WorkflowPlatformTestBase : IDisposable
{
    protected readonly PEMSDbContext Ctx = PemsDbContextFactory.CreateInMemory("wf-p1-" + Guid.NewGuid());
    protected readonly FakeCatalog Catalog = new();
    protected readonly FakeAuthHook Hook = new();
    protected readonly RecordingSink Sink = new();
    protected readonly WorkflowActionRegistry Registry = new();
    protected readonly WorkflowConditionEvaluator Conditions = new();
    protected readonly WorkflowActor Actor = new(42, "tester");
    protected readonly WorkflowVersionValidator Validator;
    protected readonly WorkflowDefinitionService Defs;
    protected readonly WorkflowBindingService Bindings;
    protected readonly WorkflowRuntimeService Runtime;
    protected readonly WorkflowReadModelService ReadModel;
    protected DateTime Now = new(2026, 9, 20, 9, 0, 0);

    protected WorkflowPlatformTestBase()
    {
        Validator = new WorkflowVersionValidator(Ctx, Registry, Conditions, Catalog);
        Defs = new WorkflowDefinitionService(Ctx, Validator);
        Bindings = new WorkflowBindingService(Ctx);
        Runtime = new WorkflowRuntimeService(Ctx, Bindings, Conditions, Hook, Sink, Registry);
        ReadModel = new WorkflowReadModelService(Ctx, Conditions, Hook, Registry, () => Now);
    }

    public void Dispose() => Ctx.Dispose();

    protected sealed record Built(WorkflowDefinition Def, WorkflowVersion Version, WorkflowStage A, WorkflowStage B);

    protected static WorkflowStageInput Stage(string name, int order, string? role, string type = "Sequential") =>
        new() { Name = name, StageOrder = order, ResponsibleRole = role, StageType = type };

    protected static WorkflowTransitionInput Tr(int? from, int? to, string action, int priority = 1, string? cond = null, string? perm = null, bool sod = false) =>
        new() { FromStageId = from, ToStageId = to, ActionType = action, Priority = priority, ConditionExpression = cond, RequiredPermission = perm, ForbidInitiator = sod };

    /// <summary>Draft با دو مرحله‌ی A→B و گذارهای کامل. Publish نمی‌کند.</summary>
    protected async Task<Built> BuildDraftAsync(string name = "Review", string module = "Test", string category = "Std")
    {
        var def = await Defs.CreateDefinitionAsync(name, category, module, null, Actor);
        var v = await Defs.CreateDraftVersionAsync(def.WorkflowDefinitionId, Actor);
        var a = await Defs.AddStageAsync(v.WorkflowVersionId, Stage("Stage A", 1, "RoleA"), Actor);
        var b = await Defs.AddStageAsync(v.WorkflowVersionId, Stage("Stage B", 2, "RoleB"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(null, a.WorkflowStageId, "Start"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(a.WorkflowStageId, b.WorkflowStageId, "Approve"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(a.WorkflowStageId, null, "Reject"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(b.WorkflowStageId, null, "Approve"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(b.WorkflowStageId, null, "Reject"), Actor);
        return new Built(def, v, a, b);
    }

    protected async Task<Built> BuildPublishedAsync(string name = "Review", string module = "Test", string category = "Std")
    {
        var b = await BuildDraftAsync(name, module, category);
        var r = await Defs.ValidateVersionAsync(b.Version.WorkflowVersionId, Actor);
        Assert.True(r.IsValid, string.Join("; ", r.Errors.Select(e => e.Code)));
        await Defs.PublishVersionAsync(b.Version.WorkflowVersionId, Actor);
        return b;
    }
}
