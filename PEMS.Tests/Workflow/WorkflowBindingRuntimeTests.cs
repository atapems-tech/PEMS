using Microsoft.EntityFrameworkCore;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Tests.Workflow;

public class WorkflowBindingTests : WorkflowPlatformTestBase
{
    [Fact]
    public async Task Binding_resolves_definition_and_latest_published_version_when_not_pinned()
    {
        var b = await BuildPublishedAsync();
        await Bindings.CreateAsync("Test", "Doc", "Std", b.Def.WorkflowDefinitionId, null, null, null, Actor);

        var v2 = await Defs.CloneVersionAsync(b.Version.WorkflowVersionId, Actor);
        await Defs.ValidateVersionAsync(v2.WorkflowVersionId, Actor);
        await Defs.PublishVersionAsync(v2.WorkflowVersionId, Actor);

        var res = await Bindings.ResolveAsync("Test", "Doc", "Std");
        Assert.NotNull(res);
        Assert.Equal(v2.WorkflowVersionId, res!.Version.WorkflowVersionId);
    }

    [Fact]
    public async Task Pinned_binding_keeps_using_the_pinned_version_after_a_newer_publish()
    {
        var b = await BuildPublishedAsync();
        await Bindings.CreateAsync("Test", "Doc", "Std", b.Def.WorkflowDefinitionId, b.Version.WorkflowVersionId, null, null, Actor);
        var v2 = await Defs.CloneVersionAsync(b.Version.WorkflowVersionId, Actor);
        await Defs.ValidateVersionAsync(v2.WorkflowVersionId, Actor);
        await Defs.PublishVersionAsync(v2.WorkflowVersionId, Actor);

        Assert.Equal(b.Version.WorkflowVersionId, (await Bindings.ResolveAsync("Test", "Doc", "Std"))!.Version.WorkflowVersionId);
    }

    [Fact]
    public async Task No_binding_resolves_to_null_so_legacy_path_applies()
        => Assert.Null(await Bindings.ResolveAsync("Test", "Doc", "Std"));

    [Fact]
    public async Task Overlapping_active_bindings_are_rejected_at_creation()
    {
        var b = await BuildPublishedAsync();
        await Bindings.CreateAsync("Test", "Doc", "Std", b.Def.WorkflowDefinitionId, null, null, null, Actor);
        await Assert.ThrowsAsync<WorkflowBindingAmbiguousException>(() =>
            Bindings.CreateAsync("Test", "Doc", "Std", b.Def.WorkflowDefinitionId, null, null, null, Actor));
    }

    [Fact]
    public async Task Ambiguity_present_in_data_fails_deterministically_instead_of_picking_the_first()
    {
        var b1 = await BuildPublishedAsync("One", "Test", "Std");
        var b2 = await BuildPublishedAsync("Two", "Test", "Other");
        // دو Binding فعال برای یک کلید، با دور زدن سرویس (داده‌ی خراب/دست‌کاری‌شده)
        Ctx.WorkflowBindings.Add(new WorkflowBinding { Module = "M", EntityType = "E", Context = "C", WorkflowDefinitionId = b1.Def.WorkflowDefinitionId });
        Ctx.WorkflowBindings.Add(new WorkflowBinding { Module = "M", EntityType = "E", Context = "C", WorkflowDefinitionId = b2.Def.WorkflowDefinitionId });
        await Ctx.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<WorkflowBindingAmbiguousException>(() => Bindings.ResolveAsync("M", "E", "C"));
        Assert.Contains("چند Binding", ex.Message);
    }

    [Fact]
    public async Task Non_overlapping_effective_windows_are_allowed_and_selected_by_date()
    {
        var b1 = await BuildPublishedAsync("Old", "Test", "Std");
        var b2 = await BuildPublishedAsync("New", "Test", "Other");
        await Bindings.CreateAsync("Test", "Doc", "Std", b1.Def.WorkflowDefinitionId, null, new DateTime(2026, 1, 1), new DateTime(2026, 6, 30), Actor);
        await Bindings.CreateAsync("Test", "Doc", "Std", b2.Def.WorkflowDefinitionId, null, new DateTime(2026, 7, 1), null, Actor);

        Assert.Equal(b1.Def.WorkflowDefinitionId, (await Bindings.ResolveAsync("Test", "Doc", "Std", new DateTime(2026, 3, 1)))!.Definition.WorkflowDefinitionId);
        Assert.Equal(b2.Def.WorkflowDefinitionId, (await Bindings.ResolveAsync("Test", "Doc", "Std", new DateTime(2026, 8, 1)))!.Definition.WorkflowDefinitionId);
        Assert.Null(await Bindings.ResolveAsync("Test", "Doc", "Std", new DateTime(2025, 1, 1)));
    }

    [Fact]
    public async Task Binding_target_must_be_valid()
    {
        var draft = await BuildDraftAsync();
        // بدون نسخه‌ی Published
        await Assert.ThrowsAsync<InvalidOperationException>(() => Bindings.CreateAsync("Test", "Doc", "Std", draft.Def.WorkflowDefinitionId, null, null, null, Actor));
        // پین به Draft
        await Assert.ThrowsAsync<InvalidOperationException>(() => Bindings.CreateAsync("Test", "Doc", "Std", draft.Def.WorkflowDefinitionId, draft.Version.WorkflowVersionId, null, null, Actor));
        // پین به نسخه‌ی تعریف دیگر
        var other = await BuildPublishedAsync("Other", "Test", "Zed");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Bindings.CreateAsync("Test", "Doc", "Std", draft.Def.WorkflowDefinitionId, other.Version.WorkflowVersionId, null, null, Actor));
        // بازه‌ی معکوس
        await Assert.ThrowsAsync<ArgumentException>(() => Bindings.CreateAsync("Test", "Doc", "Std", other.Def.WorkflowDefinitionId, null, new DateTime(2026, 5, 1), new DateTime(2026, 1, 1), Actor));
    }

    [Fact]
    public async Task Deactivate_and_Activate_toggle_resolution()
    {
        var b = await BuildPublishedAsync();
        var bind = await Bindings.CreateAsync("Test", "Doc", "Std", b.Def.WorkflowDefinitionId, null, null, null, Actor);
        await Bindings.DeactivateAsync(bind.WorkflowBindingId, Actor);
        Assert.Null(await Bindings.ResolveAsync("Test", "Doc", "Std"));
        await Bindings.ActivateAsync(bind.WorkflowBindingId, Actor);
        Assert.NotNull(await Bindings.ResolveAsync("Test", "Doc", "Std"));
    }

    [Fact]
    public async Task Runtime_starts_instance_through_binding_and_records_binding_and_entity_key()
    {
        var b = await BuildPublishedAsync("Bound", "Test", "Std");
        var bind = await Bindings.CreateAsync("Test", "Doc", "Std", b.Def.WorkflowDefinitionId, b.Version.WorkflowVersionId, null, null, Actor);

        var id = await Runtime.StartInstanceAsync("Test", "Doc", 77, "Std", null, 5);
        var inst = await Ctx.WorkflowInstances.FirstAsync(i => i.WorkflowInstanceId == id);
        Assert.Equal(bind.WorkflowBindingId, inst.WorkflowBindingId);
        Assert.Equal("Test|Doc|77", inst.EntityKey);
        Assert.Equal(b.Version.WorkflowVersionId, inst.WorkflowVersionId);
    }

    [Fact]
    public async Task Binding_path_rejects_a_second_running_instance_for_the_same_entity_but_allows_it_after_completion()
    {
        var b = await BuildPublishedAsync("Bound", "Test", "Std");
        await Bindings.CreateAsync("Test", "Doc", "Std", b.Def.WorkflowDefinitionId, null, null, null, Actor);
        var id = await Runtime.StartInstanceAsync("Test", "Doc", 1, "Std", null, 5);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Runtime.StartInstanceAsync("Test", "Doc", 1, "Std", null, 5));

        await Runtime.ExecuteActionAsync(id, "Reject", 6, new[] { "RoleA" }, null, null, null);
        var id2 = await Runtime.StartInstanceAsync("Test", "Doc", 1, "Std", null, 5);
        Assert.NotEqual(id, id2);

        // ارتباط صریح: هر دو Instance با کلید یکسان پیدا می‌شوند، جدیدترین اول
        var found = await ReadModel.FindInstancesForEntityAsync("Test", "Doc", 1);
        Assert.Equal(new[] { id2, id }, found.Select(i => i.WorkflowInstanceId).ToArray());
    }

    [Fact]
    public async Task Legacy_path_with_two_matching_active_definitions_fails_instead_of_picking_first()
    {
        await BuildPublishedAsync("First", "Test", "Std");
        await BuildPublishedAsync("Second", "Test", "Std");
        await Assert.ThrowsAsync<WorkflowBindingAmbiguousException>(() => Runtime.StartInstanceAsync("Test", "Doc", 1, "Std", null, 5));
    }

    [Fact]
    public async Task Explicit_binding_resolves_the_legacy_ambiguity()
    {
        var first = await BuildPublishedAsync("First", "Test", "Std");
        await BuildPublishedAsync("Second", "Test", "Std");
        await Bindings.CreateAsync("Test", "Doc", "Std", first.Def.WorkflowDefinitionId, null, null, null, Actor);
        var id = await Runtime.StartInstanceAsync("Test", "Doc", 1, "Std", null, 5);
        Assert.Equal(first.Version.WorkflowVersionId, (await Ctx.WorkflowInstances.FirstAsync(i => i.WorkflowInstanceId == id)).WorkflowVersionId);
    }

    [Fact]
    public async Task Legacy_path_without_binding_still_works_and_leaves_binding_null()
    {
        var b = await BuildPublishedAsync();
        var id = await Runtime.StartInstanceAsync("Test", "Doc", 1, "Std", null, 5);
        var inst = await Ctx.WorkflowInstances.FirstAsync(i => i.WorkflowInstanceId == id);
        Assert.Null(inst.WorkflowBindingId);
        Assert.Equal(b.Version.WorkflowVersionId, inst.WorkflowVersionId);
        // مسیر Legacy تکرار Instance Running را مثل قبل مجاز می‌داند (رفتار ماژول‌های موجود تغییر نکرده)
        await Runtime.StartInstanceAsync("Test", "Doc", 1, "Std", null, 5);
    }

    [Fact]
    public async Task Old_instances_stay_pinned_to_their_version_after_new_version_publishes()
    {
        var b = await BuildPublishedAsync();
        var id = await Runtime.StartInstanceAsync("Test", "Doc", 1, "Std", null, 5);
        var v2 = await Defs.CloneVersionAsync(b.Version.WorkflowVersionId, Actor);
        await Defs.AddStageAsync(v2.WorkflowVersionId, Stage("Reliability Review", 3, "RoleA"), Actor);
        var stageB2 = await Ctx.WorkflowStages.SingleAsync(s => s.WorkflowVersionId == v2.WorkflowVersionId && s.Name == "Stage B");
        var rr = await Ctx.WorkflowStages.SingleAsync(s => s.WorkflowVersionId == v2.WorkflowVersionId && s.Name == "Reliability Review");
        await Defs.AddTransitionAsync(v2.WorkflowVersionId, Tr(stageB2.WorkflowStageId, rr.WorkflowStageId, "Return", 5), Actor);
        await Defs.AddTransitionAsync(v2.WorkflowVersionId, Tr(rr.WorkflowStageId, null, "Approve"), Actor);
        await Defs.AddTransitionAsync(v2.WorkflowVersionId, Tr(rr.WorkflowStageId, null, "Reject"), Actor);
        var vr = await Defs.ValidateVersionAsync(v2.WorkflowVersionId, Actor);
        Assert.True(vr.IsValid, string.Join(",", vr.Errors.Select(e => e.Code)));
        await Defs.PublishVersionAsync(v2.WorkflowVersionId, Actor);

        Assert.Equal(b.Version.WorkflowVersionId, (await Ctx.WorkflowInstances.FirstAsync(i => i.WorkflowInstanceId == id)).WorkflowVersionId);
        var id2 = await Runtime.StartInstanceAsync("Test", "Doc", 2, "Std", null, 5);
        Assert.Equal(v2.WorkflowVersionId, (await Ctx.WorkflowInstances.FirstAsync(i => i.WorkflowInstanceId == id2)).WorkflowVersionId);
    }
}

public class WorkflowRuntimeHardeningTests : WorkflowPlatformTestBase
{
    private async Task<int> StartBoundAsync(Built b, int entityId = 1, int? initiator = 5)
    {
        await Bindings.CreateAsync("Test", "Doc", "Std", b.Def.WorkflowDefinitionId, null, null, null, Actor);
        return await Runtime.StartInstanceAsync("Test", "Doc", entityId, "Std", null, initiator);
    }

    [Fact]
    public async Task Conditional_transitions_are_chosen_deterministically_by_priority_and_condition()
    {
        var def = await Defs.CreateDefinitionAsync("Cond", "Std", "Test", null, Actor);
        var v = await Defs.CreateDraftVersionAsync(def.WorkflowDefinitionId, Actor);
        var a = await Defs.AddStageAsync(v.WorkflowVersionId, Stage("A", 1, "RoleA"), Actor);
        var fast = await Defs.AddStageAsync(v.WorkflowVersionId, Stage("Fast", 2, "RoleB"), Actor);
        var slow = await Defs.AddStageAsync(v.WorkflowVersionId, Stage("Slow", 3, "RoleB"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(null, a.WorkflowStageId, "Start"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(a.WorkflowStageId, fast.WorkflowStageId, "Approve", 1, "ProjectId == 9"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(a.WorkflowStageId, slow.WorkflowStageId, "Approve", 2), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(a.WorkflowStageId, null, "Reject"), Actor);
        foreach (var s in new[] { fast, slow })
        {
            await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(s.WorkflowStageId, null, "Approve"), Actor);
            await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(s.WorkflowStageId, null, "Reject"), Actor);
        }
        Assert.True((await Defs.ValidateVersionAsync(v.WorkflowVersionId, Actor)).IsValid);
        await Defs.PublishVersionAsync(v.WorkflowVersionId, Actor);

        var i9 = await Runtime.StartInstanceAsync("Test", "Doc", 1, "Std", 9, 5);
        await Runtime.ExecuteActionAsync(i9, "Approve", 6, new[] { "RoleA" }, null, null, null);
        Assert.Equal(fast.WorkflowStageId, (await Ctx.WorkflowInstances.FirstAsync(i => i.WorkflowInstanceId == i9)).CurrentStageId);

        var i1 = await Runtime.StartInstanceAsync("Test", "Doc", 2, "Std", 1, 5);
        await Runtime.ExecuteActionAsync(i1, "Approve", 6, new[] { "RoleA" }, null, null, null);
        Assert.Equal(slow.WorkflowStageId, (await Ctx.WorkflowInstances.FirstAsync(i => i.WorkflowInstanceId == i1)).CurrentStageId);
    }

    [Fact]
    public async Task Separation_of_duties_blocks_the_initiator()
    {
        var b = await BuildDraftAsync();
        // جایگزین گذار Approve مرحله‌ی A با نسخه‌ی SoD
        var def2 = await Defs.CreateDefinitionAsync("Sod", "Sod", "Test", null, Actor);
        var v = await Defs.CreateDraftVersionAsync(def2.WorkflowDefinitionId, Actor);
        var a = await Defs.AddStageAsync(v.WorkflowVersionId, Stage("A", 1, "RoleA"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(null, a.WorkflowStageId, "Start"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(a.WorkflowStageId, null, "Approve", 1, null, null, true), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(a.WorkflowStageId, null, "Reject"), Actor);
        Assert.True((await Defs.ValidateVersionAsync(v.WorkflowVersionId, Actor)).IsValid);
        await Defs.PublishVersionAsync(v.WorkflowVersionId, Actor);

        var id = await Runtime.StartInstanceAsync("Test", "Doc", 1, "Sod", null, initiatedByEmployeeId: 5);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Runtime.ExecuteActionAsync(id, "Approve", 5, new[] { "RoleA" }, null, null, null));
        Assert.Equal("Running", (await Ctx.WorkflowInstances.FirstAsync(i => i.WorkflowInstanceId == id)).Status);
        await Runtime.ExecuteActionAsync(id, "Approve", 6, new[] { "RoleA" }, null, null, null);
        Assert.Equal("Completed", (await Ctx.WorkflowInstances.FirstAsync(i => i.WorkflowInstanceId == id)).Status);
        _ = b;
    }

    private async Task<int> StartPermissionWorkflowAsync(IWorkflowAuthorizationHook? hook)
    {
        var def = await Defs.CreateDefinitionAsync("Perm", "Perm", "Test", null, Actor);
        var v = await Defs.CreateDraftVersionAsync(def.WorkflowDefinitionId, Actor);
        var a = await Defs.AddStageAsync(v.WorkflowVersionId, Stage("A", 1, "RoleA"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(null, a.WorkflowStageId, "Start"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(a.WorkflowStageId, null, "Approve", 1, null, "Workflow.Approve"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(a.WorkflowStageId, null, "Reject"), Actor);
        Assert.True((await Defs.ValidateVersionAsync(v.WorkflowVersionId, Actor)).IsValid);
        await Defs.PublishVersionAsync(v.WorkflowVersionId, Actor);
        var rt = new WorkflowRuntimeService(Ctx, Bindings, Conditions, hook, Sink, Registry);
        var id = await rt.StartInstanceAsync("Test", "Doc", 1, "Perm", null, 5);
        _hookRuntime = rt;
        return id;
    }
    private WorkflowRuntimeService _hookRuntime = null!;

    [Fact]
    public async Task Permission_hook_denies_and_allows_the_transition()
    {
        var id = await StartPermissionWorkflowAsync(Hook);
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _hookRuntime.ExecuteActionAsync(id, "Approve", 6, new[] { "RoleA" }, null, null, null));
        Assert.Contains("NoPermission", ex.Message);
        Assert.Equal("Running", (await Ctx.WorkflowInstances.FirstAsync(i => i.WorkflowInstanceId == id)).Status);
        Assert.Equal("Pending", (await Ctx.WorkflowInstanceStages.FirstAsync(s => s.WorkflowInstanceId == id)).Status);

        Hook.GrantedPermissions.Add("Workflow.Approve");
        await _hookRuntime.ExecuteActionAsync(id, "Approve", 6, new[] { "RoleA" }, null, null, null);
        Assert.Equal("Completed", (await Ctx.WorkflowInstances.FirstAsync(i => i.WorkflowInstanceId == id)).Status);
    }

    [Fact]
    public async Task Permission_transition_fails_closed_when_no_hook_is_configured()
    {
        var id = await StartPermissionWorkflowAsync(null);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _hookRuntime.ExecuteActionAsync(id, "Approve", 6, new[] { "RoleA" }, null, null, null));
    }

    [Fact]
    public async Task Role_check_still_applies_before_anything_else()
    {
        var b = await BuildPublishedAsync();
        var id = await StartBoundAsync(b);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Runtime.ExecuteActionAsync(id, "Approve", 6, new[] { "SomebodyElse" }, null, null, null));
    }

    [Fact]
    public async Task Binding_path_rejects_unregistered_actions_but_legacy_path_does_not()
    {
        var b = await BuildPublishedAsync();
        var id = await StartBoundAsync(b);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Runtime.ExecuteActionAsync(id, "RunScript", 6, new[] { "RoleA" }, null, null, null));
        Assert.Contains("رجیستری", ex.Message);
    }

    [Fact]
    public async Task Registry_only_stores_names_and_validates_them()
    {
        Assert.Throws<ArgumentException>(() => Registry.RegisterModuleAction("M", "bad name; DROP TABLE", WorkflowActionKind.Outcome, ""));
        Assert.Throws<ArgumentException>(() => Registry.RegisterModuleAction("", "Ok", WorkflowActionKind.Outcome, ""));
        Assert.Throws<InvalidOperationException>(() => Registry.RegisterModuleAction("M", "Approve", WorkflowActionKind.Outcome, ""));
        Assert.All(new[] { "Approve", "Reject", "Return", "Assign", "Notify", "Escalate", "Complete" }, n => Assert.NotNull(Registry.Find(n, null)));
        Assert.Equal(WorkflowActionKind.SideEffect, Registry.Find("Notify", null)!.Kind);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Event_sink_receives_events_and_a_failing_sink_does_not_break_the_workflow()
    {
        var b = await BuildPublishedAsync();
        var id = await StartBoundAsync(b);
        await Runtime.ExecuteActionAsync(id, "Approve", 6, new[] { "RoleA" }, null, null, null);
        Assert.Contains(Sink.Events, e => e.EventType == "StageEntered" && e.StageName == "Stage B");
        Assert.Contains(Sink.Events, e => e.EventType == "ActionExecuted");

        var throwing = new WorkflowRuntimeService(Ctx, Bindings, Conditions, Hook, new ThrowingSink(), Registry);
        await throwing.ExecuteActionAsync(id, "Approve", 7, new[] { "RoleB" }, null, null, null);
        Assert.Equal("Completed", (await Ctx.WorkflowInstances.FirstAsync(i => i.WorkflowInstanceId == id)).Status);
        Assert.Contains(Sink.Events, e => e.EventType == "ActionExecuted"); // (نمونه‌ی Sink اول)
    }

    private sealed class ThrowingSink : IWorkflowEventSink
    {
        public Task OnEventAsync(WorkflowRuntimeEvent evt, CancellationToken ct = default) => throw new InvalidOperationException("boom");
    }

    [Fact]
    public async Task Runtime_default_constructor_is_still_supported()
    {
        var b = await BuildPublishedAsync();
        var plain = new WorkflowRuntimeService(Ctx);
        var id = await plain.StartInstanceAsync("Test", "Doc", 1, "Std", null, 5);
        await plain.ExecuteActionAsync(id, "Approve", 6, new[] { "RoleA" }, null, null, null);
        await plain.ExecuteActionAsync(id, "Approve", 7, new[] { "RoleB" }, null, null, null);
        Assert.Equal("Completed", (await Ctx.WorkflowInstances.FirstAsync(i => i.WorkflowInstanceId == id)).Status);
        _ = b;
    }
}

public class WorkflowConditionEvaluatorTests
{
    private readonly WorkflowConditionEvaluator _e = new();
    private static IReadOnlyDictionary<string, string?> Facts(string? priority = "High", string? project = "9") =>
        new Dictionary<string, string?> { ["Priority"] = priority, ["ProjectId"] = project, ["Module"] = "M", ["StageName"] = "Review" };

    [Theory]
    [InlineData("Priority == 'High'", true)]
    [InlineData("Priority == 'high'", true)]
    [InlineData("Priority != 'High'", false)]
    [InlineData("ProjectId > 5", true)]
    [InlineData("ProjectId >= 9 AND Priority == 'High'", true)]
    [InlineData("ProjectId < 5 OR Priority == 'High'", true)]
    [InlineData("ProjectId < 5 AND Priority == 'High'", false)]
    [InlineData("Priority IN ('Low', 'High')", true)]
    [InlineData("Priority IN ('Low', 'Medium')", false)]
    [InlineData("StageName == 'it''s'", false)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void Evaluates(string? expr, bool expected) => Assert.Equal(expected, _e.Evaluate(expr, Facts()));

    [Fact]
    public void Missing_fact_is_false_except_for_not_equal()
    {
        Assert.False(_e.Evaluate("Priority == 'High'", Facts(priority: null)));
        Assert.True(_e.Evaluate("Priority != 'High'", Facts(priority: null)));
    }

    [Theory]
    [InlineData("System.IO.File.Delete('x')")]
    [InlineData("Unknown == 1")]
    [InlineData("Priority == ")]
    [InlineData("Priority > 'High'")]
    [InlineData("Priority == 'High' AND")]
    [InlineData("Priority ~= 3")]
    [InlineData("Priority == 'a' Priority == 'b'")]
    [InlineData("(Priority == 'a')")]
    [InlineData("Priority IN 'a'")]
    [InlineData("1 == 1")]
    public void Rejects_anything_outside_the_closed_grammar(string expr)
    {
        Assert.NotEmpty(_e.Validate(expr));
        Assert.Throws<InvalidOperationException>(() => _e.Evaluate(expr, Facts()));
    }

    [Fact]
    public void Supported_fields_are_a_closed_list() =>
        Assert.Equal(new[] { "BusinessObjectId", "BusinessObjectType", "Module", "Priority", "ProjectId", "StageName" }, _e.SupportedFields.OrderBy(x => x).ToArray());
}

public class WorkflowConfigAuditTests : WorkflowPlatformTestBase
{
    [Fact]
    public async Task Full_lifecycle_is_audited_with_actor_state_and_ids_separately_from_runtime_history()
    {
        var b = await BuildDraftAsync();
        await Defs.AddStageAsync(b.Version.WorkflowVersionId, Stage("Late", 3, "RoleA"), Actor);
        await Defs.ValidateVersionAsync(b.Version.WorkflowVersionId, Actor);          // فاقد گذار برای Late → خطا؟ (DEAD_END/UNREACHABLE)
        var late = await Ctx.WorkflowStages.SingleAsync(s => s.Name == "Late");
        await Defs.RemoveStageAsync(late.WorkflowStageId, Actor);
        await Defs.ValidateVersionAsync(b.Version.WorkflowVersionId, Actor);
        await Defs.PublishVersionAsync(b.Version.WorkflowVersionId, Actor);
        var bind = await Bindings.CreateAsync("Test", "Doc", "Std", b.Def.WorkflowDefinitionId, null, null, null, Actor);
        await Bindings.ChangeAsync(bind.WorkflowBindingId, b.Version.WorkflowVersionId, null, null, Actor);
        await Bindings.DeactivateAsync(bind.WorkflowBindingId, Actor);
        var clone = await Defs.CloneVersionAsync(b.Version.WorkflowVersionId, Actor, "c");
        await Defs.RetireVersionAsync(b.Version.WorkflowVersionId, Actor);
        await Defs.CloneDefinitionAsync(b.Def.WorkflowDefinitionId, "Review copy", Actor);
        await Defs.ArchiveDefinitionAsync(b.Def.WorkflowDefinitionId, Actor);

        var actions = (await Defs.GetAuditAsync()).Select(a => a.Action).ToHashSet();
        foreach (var expected in new[]
        {
            "DefinitionCreated", "VersionCreated", "DraftEdited", "ValidationFailed", "Validated", "Published", "BindingCreated", "BindingChanged",
            "Activated", "Deactivated", "VersionCloned", "Retired", "DefinitionCloned", "DefinitionArchived"
        })
            Assert.Contains(expected, actions);

        var published = (await Defs.GetAuditAsync(b.Def.WorkflowDefinitionId, b.Version.WorkflowVersionId)).Single(a => a.Action == "Published");
        Assert.Equal(42, published.ActorUserId);
        Assert.Equal("tester", published.ActorName);
        Assert.Equal("Validated", published.PreviousState);
        Assert.Equal("Published", published.CurrentState);
        Assert.Equal(b.Def.WorkflowDefinitionId, published.WorkflowDefinitionId);
        Assert.Equal(b.Version.WorkflowVersionId, published.WorkflowVersionId);
        Assert.True(published.Timestamp > DateTime.Now.AddMinutes(-5));

        var bindingAudit = (await Defs.GetAuditAsync(b.Def.WorkflowDefinitionId)).Where(a => a.WorkflowBindingId == bind.WorkflowBindingId).ToList();
        Assert.True(bindingAudit.Count >= 3);
        var changed = bindingAudit.Single(a => a.Action == "BindingChanged");
        Assert.NotEqual(changed.PreviousState, changed.CurrentState);

        // جدا از تاریخچه‌ی اجرایی
        Assert.Equal(0, await Ctx.WorkflowHistories.CountAsync());
        _ = clone;
    }

    [Fact]
    public async Task Runtime_actions_do_not_write_config_audit_and_config_does_not_write_history()
    {
        var b = await BuildPublishedAsync();
        var before = await Ctx.WorkflowConfigAudits.CountAsync();
        var id = await Runtime.StartInstanceAsync("Test", "Doc", 1, "Std", null, 5);
        await Runtime.ExecuteActionAsync(id, "Approve", 6, new[] { "RoleA" }, null, null, null);
        Assert.Equal(before, await Ctx.WorkflowConfigAudits.CountAsync());
        Assert.True(await Ctx.WorkflowHistories.CountAsync() >= 2);
        _ = b;
    }

    [Fact]
    public async Task Draft_edit_audit_records_previous_and_current_status()
    {
        var b = await BuildDraftAsync();
        await Defs.ValidateVersionAsync(b.Version.WorkflowVersionId, Actor);
        await Defs.AddStageAsync(b.Version.WorkflowVersionId, Stage("Extra", 3, "RoleA"), Actor);
        var last = (await Defs.GetAuditAsync(versionId: b.Version.WorkflowVersionId)).Last(a => a.Action == "DraftEdited");
        Assert.Equal("Validated", last.PreviousState);
        Assert.Equal("Draft", last.CurrentState);
        Assert.Contains("AddStage", last.Details);
    }
}

public class WorkflowReadModelTests : WorkflowPlatformTestBase
{
    private static readonly string[] RoleA = { "RoleA" };
    private static readonly string[] RoleB = { "RoleB" };

    [Fact]
    public async Task Read_model_lists_ordered_stages_with_status_and_current_stage()
    {
        var b = await BuildPublishedAsync();
        var id = await Runtime.StartInstanceAsync("Test", "Doc", 1, "Std", null, 5);
        var vm = (await ReadModel.GetRuntimeViewAsync(id, new WorkflowUserContext(6, RoleA)))!;

        Assert.Equal("Review", vm.WorkflowName);
        Assert.Equal(1, vm.VersionNumber);
        Assert.Equal("Running", vm.Status);
        Assert.Equal("Stage A", vm.CurrentStageName);
        Assert.Equal(new[] { "Stage A", "Stage B" }, vm.Stages.Select(s => s.Name).ToArray());
        Assert.Equal(new[] { "Current", "Upcoming" }, vm.Stages.Select(s => s.Status).ToArray());
        Assert.Equal(1, vm.Stages[0].PendingCount);
        Assert.Contains(vm.Stages[0].Participants, p => p.Role == "RoleA" && p.Status == "Pending");
        Assert.Single(vm.HistorySummary);
        _ = b;
    }

    [Fact]
    public async Task Stage_status_progresses_to_completed_rejected_and_finished()
    {
        var b = await BuildPublishedAsync();
        var id = await Runtime.StartInstanceAsync("Test", "Doc", 1, "Std", null, 5);
        await Runtime.ExecuteActionAsync(id, "Approve", 6, RoleA, "ok", null, null);
        var vm = (await ReadModel.GetRuntimeViewAsync(id, new WorkflowUserContext(7, RoleB)))!;
        Assert.Equal(new[] { "Completed", "Current" }, vm.Stages.Select(s => s.Status).ToArray());
        Assert.Equal(1, vm.Stages[0].ApprovedCount);

        await Runtime.ExecuteActionAsync(id, "Reject", 7, RoleB, "no", null, null);
        vm = (await ReadModel.GetRuntimeViewAsync(id, new WorkflowUserContext(7, RoleB)))!;
        Assert.Equal("Rejected", vm.Status);
        Assert.Equal("Rejected", vm.Stages[1].Status);
        Assert.Empty(vm.AvailableActions);
        Assert.Equal(3, vm.HistorySummary.Count);
        _ = b;
    }

    [Fact]
    public async Task Inserting_a_new_stage_in_a_new_version_appears_automatically_in_the_read_model()
    {
        var b = await BuildPublishedAsync();
        var v2 = await Defs.CloneVersionAsync(b.Version.WorkflowVersionId, Actor);
        var rr = await Defs.AddStageAsync(v2.WorkflowVersionId, Stage("Reliability Review", 15, "RoleA"), Actor);
        var b2 = await Ctx.WorkflowStages.SingleAsync(s => s.WorkflowVersionId == v2.WorkflowVersionId && s.Name == "Stage B");
        await Defs.AddTransitionAsync(v2.WorkflowVersionId, Tr(b2.WorkflowStageId, rr.WorkflowStageId, "Return", 5), Actor);
        await Defs.AddTransitionAsync(v2.WorkflowVersionId, Tr(rr.WorkflowStageId, null, "Approve"), Actor);
        await Defs.AddTransitionAsync(v2.WorkflowVersionId, Tr(rr.WorkflowStageId, null, "Reject"), Actor);
        Assert.True((await Defs.ValidateVersionAsync(v2.WorkflowVersionId, Actor)).IsValid);
        await Defs.PublishVersionAsync(v2.WorkflowVersionId, Actor);

        var oldId = await Runtime.StartInstanceAsync("Test", "Doc", 1, "Std", null, 5); // نسخه‌ی 2 چون آخرین Published
        var vm = (await ReadModel.GetRuntimeViewAsync(oldId, new WorkflowUserContext(6, RoleA)))!;
        Assert.Equal(2, vm.VersionNumber);
        Assert.Contains("Reliability Review", vm.Stages.Select(s => s.Name));
        Assert.Equal(new[] { "Stage A", "Stage B", "Reliability Review" }, vm.Stages.Select(s => s.Name).ToArray());
    }

    [Fact]
    public async Task Available_actions_follow_role_state_transitions()
    {
        var b = await BuildPublishedAsync();
        var id = await Runtime.StartInstanceAsync("Test", "Doc", 1, "Std", null, 5);

        var forA = await ReadModel.GetAvailableActionsAsync(id, new WorkflowUserContext(6, RoleA));
        Assert.Equal(new[] { "Approve", "Reject" }, forA.Select(a => a.ActionType).OrderBy(x => x).ToArray());
        Assert.All(forA, a => Assert.True(a.IsAllowed));
        Assert.Equal("Stage B", forA.Single(a => a.ActionType == "Approve").ToStageName);

        Assert.Empty(await ReadModel.GetAvailableActionsAsync(id, new WorkflowUserContext(7, RoleB)));      // نقش دیگر
        Assert.Empty(await ReadModel.GetAvailableActionsAsync(id, new WorkflowUserContext(9, Array.Empty<string>())));
        Assert.Empty(await ReadModel.GetAvailableActionsAsync(9999, new WorkflowUserContext(6, RoleA)));       // Instance نامعتبر

        await Runtime.ExecuteActionAsync(id, "Approve", 6, RoleA, null, null, null);
        Assert.Empty(await ReadModel.GetAvailableActionsAsync(id, new WorkflowUserContext(6, RoleA)));       // دیگر مرحله‌ی A نیست
        Assert.NotEmpty(await ReadModel.GetAvailableActionsAsync(id, new WorkflowUserContext(7, RoleB)));
        _ = b;
    }

    [Fact]
    public async Task Available_actions_honor_conditions_sod_and_permission()
    {
        var def = await Defs.CreateDefinitionAsync("Rich", "Rich", "Test", null, Actor);
        var v = await Defs.CreateDraftVersionAsync(def.WorkflowDefinitionId, Actor);
        var a = await Defs.AddStageAsync(v.WorkflowVersionId, Stage("A", 1, "RoleA"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(null, a.WorkflowStageId, "Start"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(a.WorkflowStageId, null, "Approve", 1, null, "Workflow.Approve"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(a.WorkflowStageId, null, "Reject", 1, null, null, true), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(a.WorkflowStageId, null, "Return", 1, "Priority == 'Critical'"), Actor);
        Assert.True((await Defs.ValidateVersionAsync(v.WorkflowVersionId, Actor)).IsValid);
        await Defs.PublishVersionAsync(v.WorkflowVersionId, Actor);

        var id = await Runtime.StartInstanceAsync("Test", "Doc", 1, "Rich", null, initiatedByEmployeeId: 5);
        var actions = await ReadModel.GetAvailableActionsAsync(id, new WorkflowUserContext(5, RoleA));

        Assert.DoesNotContain(actions, x => x.ActionType == "Return");                       // شرط برقرار نیست → اصلاً نمایش داده نمی‌شود
        Assert.False(actions.Single(x => x.ActionType == "Approve").IsAllowed);
        Assert.Equal("NoPermission", actions.Single(x => x.ActionType == "Approve").DeniedReason);
        Assert.Equal("SeparationOfDuties", actions.Single(x => x.ActionType == "Reject").DeniedReason);

        Hook.GrantedPermissions.Add("Workflow.Approve");
        var other = await ReadModel.GetAvailableActionsAsync(id, new WorkflowUserContext(6, RoleA));
        Assert.True(other.Single(x => x.ActionType == "Approve").IsAllowed);
        Assert.True(other.Single(x => x.ActionType == "Reject").IsAllowed);
    }

    [Fact]
    public async Task Sla_due_and_overdue_are_computed_and_escalation_is_never_reported_as_executed()
    {
        var def = await Defs.CreateDefinitionAsync("Sla", "Sla", "Test", null, Actor);
        var v = await Defs.CreateDraftVersionAsync(def.WorkflowDefinitionId, Actor);
        var a = await Defs.AddStageAsync(v.WorkflowVersionId, new WorkflowStageInput
        { Name = "A", StageOrder = 1, ResponsibleRole = "RoleA", TimeoutHours = 4, IsEscalatable = true, EscalationRole = "Escalator" }, Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(null, a.WorkflowStageId, "Start"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(a.WorkflowStageId, null, "Approve"), Actor);
        await Defs.AddTransitionAsync(v.WorkflowVersionId, Tr(a.WorkflowStageId, null, "Reject"), Actor);
        Assert.True((await Defs.ValidateVersionAsync(v.WorkflowVersionId, Actor)).IsValid);
        await Defs.PublishVersionAsync(v.WorkflowVersionId, Actor);

        var id = await Runtime.StartInstanceAsync("Test", "Doc", 1, "Sla", null, 5);
        var assigned = (await Ctx.WorkflowInstanceStages.FirstAsync(x => x.WorkflowInstanceId == id)).AssignedDate;

        Now = assigned.AddHours(1);
        var vm = (await ReadModel.GetRuntimeViewAsync(id, new WorkflowUserContext(6, RoleA)))!;
        Assert.Equal(assigned.AddHours(4), vm.Stages[0].Sla.DueDate);
        Assert.False(vm.Stages[0].Sla.IsOverdue);

        Now = assigned.AddHours(5);
        vm = (await ReadModel.GetRuntimeViewAsync(id, new WorkflowUserContext(6, RoleA)))!;
        Assert.True(vm.Stages[0].Sla.IsOverdue);
        Assert.False(vm.Stages[0].Sla.EscalationExecuted);
        Assert.Equal("Escalator", vm.Stages[0].Sla.EscalationRole);
        Assert.Contains(vm.Warnings, w => w.Contains("SLA"));
        Assert.Equal("Running", vm.Status); // Escalation/تغییر وضعیتی اتفاق نیفتاده
    }

    [Fact]
    public async Task Legacy_instances_get_a_warning_and_bound_instances_do_not()
    {
        var b = await BuildPublishedAsync();
        var legacy = await Runtime.StartInstanceAsync("Test", "Doc", 1, "Std", null, 5);
        Assert.Contains((await ReadModel.GetRuntimeViewAsync(legacy, new WorkflowUserContext(6, RoleA)))!.Warnings, w => w.Contains("Legacy"));

        await Bindings.CreateAsync("Test", "Doc", "Std", b.Def.WorkflowDefinitionId, null, null, null, Actor);
        var bound = await Runtime.StartInstanceAsync("Test", "Doc", 2, "Std", null, 5);
        Assert.DoesNotContain((await ReadModel.GetRuntimeViewAsync(bound, new WorkflowUserContext(6, RoleA)))!.Warnings, w => w.Contains("Legacy"));
    }

    [Fact]
    public async Task Unknown_instance_returns_null()
        => Assert.Null(await ReadModel.GetRuntimeViewAsync(12345, new WorkflowUserContext(1, RoleA)));
}
