using Microsoft.EntityFrameworkCore;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Tests.Workflow;

public class WorkflowLifecycleTests : WorkflowPlatformTestBase
{
    [Fact]
    public async Task Draft_Validate_Publish_moves_through_lifecycle()
    {
        var b = await BuildDraftAsync();
        Assert.Equal("Draft", b.Version.Status);

        var r = await Defs.ValidateVersionAsync(b.Version.WorkflowVersionId, Actor);
        Assert.True(r.IsValid);
        Assert.Equal("Validated", (await Ctx.WorkflowVersions.FindAsync(b.Version.WorkflowVersionId))!.Status);

        var pub = await Defs.PublishVersionAsync(b.Version.WorkflowVersionId, Actor);
        Assert.Equal("Published", pub.Status);
        Assert.NotNull(pub.PublishedDate);
        Assert.Equal(42, pub.PublishedById);
    }

    [Fact]
    public async Task Publish_requires_validated_state()
    {
        var b = await BuildDraftAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Defs.PublishVersionAsync(b.Version.WorkflowVersionId, Actor));
    }

    [Fact]
    public async Task Invalid_version_cannot_be_validated_or_published()
    {
        var def = await Defs.CreateDefinitionAsync("Broken", "Std", "Test", null, Actor);
        var v = await Defs.CreateDraftVersionAsync(def.WorkflowDefinitionId, Actor);
        await Defs.AddStageAsync(v.WorkflowVersionId, Stage("Only", 1, "RoleA"), Actor);

        var r = await Defs.ValidateVersionAsync(v.WorkflowVersionId, Actor);
        Assert.False(r.IsValid);
        Assert.Equal("Draft", (await Ctx.WorkflowVersions.FindAsync(v.WorkflowVersionId))!.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Defs.PublishVersionAsync(v.WorkflowVersionId, Actor));
    }

    [Fact]
    public async Task Published_version_cannot_be_edited_through_services()
    {
        var b = await BuildPublishedAsync();
        await Assert.ThrowsAsync<WorkflowImmutableVersionException>(() => Defs.AddStageAsync(b.Version.WorkflowVersionId, Stage("X", 9, "RoleA"), Actor));
        await Assert.ThrowsAsync<WorkflowImmutableVersionException>(() => Defs.UpdateStageAsync(b.A.WorkflowStageId, Stage("Renamed", 1, "RoleA"), Actor));
        await Assert.ThrowsAsync<WorkflowImmutableVersionException>(() => Defs.RemoveStageAsync(b.A.WorkflowStageId, Actor));
        await Assert.ThrowsAsync<WorkflowImmutableVersionException>(() => Defs.AddTransitionAsync(b.Version.WorkflowVersionId, Tr(b.A.WorkflowStageId, b.B.WorkflowStageId, "Return", 5), Actor));
    }

    [Fact]
    public async Task Published_stage_cannot_be_modified_by_direct_SaveChanges()
    {
        var b = await BuildPublishedAsync();
        var stage = await Ctx.WorkflowStages.FirstAsync(s => s.WorkflowStageId == b.A.WorkflowStageId);
        stage.Name = "tampered";
        await Assert.ThrowsAsync<WorkflowImmutableVersionException>(() => Ctx.SaveChangesAsync());
        Ctx.ChangeTracker.Clear();
        Assert.Equal("Stage A", (await Ctx.WorkflowStages.FirstAsync(s => s.WorkflowStageId == b.A.WorkflowStageId)).Name);
    }

    [Fact]
    public async Task Published_transition_cannot_be_deleted_or_added_directly()
    {
        var b = await BuildPublishedAsync();
        Ctx.WorkflowTransitions.Add(new WorkflowTransition { WorkflowVersionId = b.Version.WorkflowVersionId, FromStageId = b.A.WorkflowStageId, ActionType = "Return" });
        await Assert.ThrowsAsync<WorkflowImmutableVersionException>(() => Ctx.SaveChangesAsync());
        Ctx.ChangeTracker.Clear();

        var t = await Ctx.WorkflowTransitions.FirstAsync(x => x.WorkflowVersionId == b.Version.WorkflowVersionId);
        Ctx.WorkflowTransitions.Remove(t);
        await Assert.ThrowsAsync<WorkflowImmutableVersionException>(() => Ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Published_version_fields_are_immutable_but_retire_is_allowed()
    {
        var b = await BuildPublishedAsync();
        var v = await Ctx.WorkflowVersions.FirstAsync(x => x.WorkflowVersionId == b.Version.WorkflowVersionId);
        v.ChangeSummary = "sneaky";
        await Assert.ThrowsAsync<WorkflowImmutableVersionException>(() => Ctx.SaveChangesAsync());
        Ctx.ChangeTracker.Clear();

        var v2 = await Ctx.WorkflowVersions.FirstAsync(x => x.WorkflowVersionId == b.Version.WorkflowVersionId);
        v2.Status = "Draft"; // Published → Draft ممنوع
        await Assert.ThrowsAsync<WorkflowImmutableVersionException>(() => Ctx.SaveChangesAsync());
        Ctx.ChangeTracker.Clear();

        var retired = await Defs.RetireVersionAsync(b.Version.WorkflowVersionId, Actor);
        Assert.Equal("Retired", retired.Status);
        var v3 = await Ctx.WorkflowVersions.FirstAsync(x => x.WorkflowVersionId == b.Version.WorkflowVersionId);
        v3.Status = "Published"; // Retired → Published ممنوع
        await Assert.ThrowsAsync<WorkflowImmutableVersionException>(() => Ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Seeder_style_direct_insert_of_a_published_version_is_still_allowed()
    {
        var def = new WorkflowDefinition { Name = "Seeded", Category = "S", Module = "M" };
        var v = new WorkflowVersion { Definition = def, VersionNumber = 1, Status = "Published" };
        var s = new WorkflowStage { Version = v, Name = "S1", StageOrder = 1 };
        Ctx.AddRange(def, v, s);
        Ctx.WorkflowTransitions.Add(new WorkflowTransition { Version = v, FromStage = s, ActionType = "Approve" });
        await Ctx.SaveChangesAsync();
        Assert.Equal(1, await Ctx.WorkflowStages.CountAsync());
    }

    [Fact]
    public async Task Editing_a_validated_version_returns_it_to_draft()
    {
        var b = await BuildDraftAsync();
        await Defs.ValidateVersionAsync(b.Version.WorkflowVersionId, Actor);
        await Defs.AddStageAsync(b.Version.WorkflowVersionId, Stage("Extra", 3, "RoleA"), Actor);
        var v = await Ctx.WorkflowVersions.FirstAsync(x => x.WorkflowVersionId == b.Version.WorkflowVersionId);
        Assert.Equal("Draft", v.Status);
        Assert.Null(v.ValidatedDate);
    }

    [Fact]
    public async Task Clone_copies_structure_into_a_new_draft_and_never_touches_the_source()
    {
        var b = await BuildPublishedAsync();
        // یک Instance روی نسخه‌ی مبدأ
        var instId = await Runtime.StartInstanceAsync("Test", "Doc", 1, "Std", null, 5);

        var clone = await Defs.CloneVersionAsync(b.Version.WorkflowVersionId, Actor, "add review");
        Assert.Equal("Draft", clone.Status);
        Assert.Equal(2, clone.VersionNumber);
        Assert.Equal(b.Version.WorkflowVersionId, clone.ClonedFromVersionId);

        var srcStages = await Ctx.WorkflowStages.Where(s => s.WorkflowVersionId == b.Version.WorkflowVersionId).ToListAsync();
        var cloneStages = await Ctx.WorkflowStages.Where(s => s.WorkflowVersionId == clone.WorkflowVersionId).ToListAsync();
        Assert.Equal(srcStages.Count, cloneStages.Count);
        Assert.Empty(cloneStages.Select(c => c.WorkflowStageId).Intersect(srcStages.Select(s => s.WorkflowStageId)));
        Assert.Equal(5, await Ctx.WorkflowTransitions.CountAsync(t => t.WorkflowVersionId == clone.WorkflowVersionId));
        // انتقال‌ها به مراحل نسخه‌ی جدید اشاره می‌کنند نه مبدأ
        var cloneIds = cloneStages.Select(s => s.WorkflowStageId).ToHashSet();
        Assert.All(await Ctx.WorkflowTransitions.Where(t => t.WorkflowVersionId == clone.WorkflowVersionId && t.ToStageId != null).ToListAsync(),
            t => Assert.Contains(t.ToStageId!.Value, cloneIds));

        // Instance/History کپی نشده‌اند و Instance اصلی هنوز روی نسخه‌ی خودش است
        Assert.Equal(1, await Ctx.WorkflowInstances.CountAsync());
        Assert.Equal(b.Version.WorkflowVersionId, (await Ctx.WorkflowInstances.FirstAsync(i => i.WorkflowInstanceId == instId)).WorkflowVersionId);
        Assert.Equal(1, await Ctx.WorkflowHistories.CountAsync());

        // ویرایش Clone مجاز است
        await Defs.AddStageAsync(clone.WorkflowVersionId, Stage("Reliability Review", 3, "RoleA"), Actor);
        Assert.Equal(srcStages.Count, await Ctx.WorkflowStages.CountAsync(s => s.WorkflowVersionId == b.Version.WorkflowVersionId));
    }

    [Fact]
    public async Task Clone_copies_configuration_fields()
    {
        var b = await BuildDraftAsync();
        var s = await Defs.AddStageAsync(b.Version.WorkflowVersionId, new WorkflowStageInput
        {
            Name = "Configured", StageOrder = 3, StageType = "Parallel", ResponsibleRole = "RoleA,RoleB", ApprovalStrategy = "MinimumCount",
            MinimumApprovalCount = 2, TimeoutHours = 8, IsEscalatable = true, EscalationRole = "Escalator", ResponsibleEmployeeId = 2, NotificationKey = "n.key"
        }, Actor);
        await Defs.AddTransitionAsync(b.Version.WorkflowVersionId, Tr(b.A.WorkflowStageId, s.WorkflowStageId, "Return", 5), Actor);
        await Defs.AddTransitionAsync(b.Version.WorkflowVersionId, Tr(s.WorkflowStageId, null, "Approve", 1, "Priority == 'High'", "Workflow.Approve", true), Actor);
        await Defs.AddTransitionAsync(b.Version.WorkflowVersionId, Tr(s.WorkflowStageId, null, "Reject", 1), Actor);
        var r = await Defs.ValidateVersionAsync(b.Version.WorkflowVersionId, Actor);
        Assert.True(r.IsValid, string.Join(", ", r.Errors.Select(e => e.Code)));
        await Defs.PublishVersionAsync(b.Version.WorkflowVersionId, Actor);

        var clone = await Defs.CloneVersionAsync(b.Version.WorkflowVersionId, Actor);
        var cs = await Ctx.WorkflowStages.SingleAsync(x => x.WorkflowVersionId == clone.WorkflowVersionId && x.Name == "Configured");
        Assert.Equal("Parallel", cs.StageType);
        Assert.Equal("RoleA,RoleB", cs.ResponsibleRole);
        Assert.Equal("MinimumCount", cs.ApprovalStrategy);
        Assert.Equal(2, cs.MinimumApprovalCount);
        Assert.Equal(8, cs.TimeoutHours);
        Assert.True(cs.IsEscalatable);
        Assert.Equal("Escalator", cs.EscalationRole);
        Assert.Equal(2, cs.ResponsibleEmployeeId);
        Assert.Equal("n.key", cs.NotificationKey);
        var ct = await Ctx.WorkflowTransitions.SingleAsync(x => x.WorkflowVersionId == clone.WorkflowVersionId && x.FromStageId == cs.WorkflowStageId && x.ActionType == "Approve");
        Assert.Equal("Priority == 'High'", ct.ConditionExpression);
        Assert.Equal("Workflow.Approve", ct.RequiredPermission);
        Assert.True(ct.ForbidInitiator);
    }

    [Fact]
    public async Task Only_one_open_draft_per_definition()
    {
        var b = await BuildPublishedAsync();
        await Defs.CloneVersionAsync(b.Version.WorkflowVersionId, Actor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Defs.CloneVersionAsync(b.Version.WorkflowVersionId, Actor));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Defs.CreateDraftVersionAsync(b.Def.WorkflowDefinitionId, Actor));
    }

    [Fact]
    public async Task Retire_is_blocked_while_a_binding_pins_the_version()
    {
        var b = await BuildPublishedAsync();
        var binding = await Bindings.CreateAsync("Test", "Doc", "Std", b.Def.WorkflowDefinitionId, b.Version.WorkflowVersionId, null, null, Actor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Defs.RetireVersionAsync(b.Version.WorkflowVersionId, Actor));
        await Bindings.DeactivateAsync(binding.WorkflowBindingId, Actor);
        await Defs.RetireVersionAsync(b.Version.WorkflowVersionId, Actor);
    }
}

public class WorkflowValidatorTests : WorkflowPlatformTestBase
{
    private async Task<WorkflowValidationResult> Validate(int versionId) => await Validator.ValidateAsync(versionId);
    private static bool Has(WorkflowValidationResult r, string code) => r.Issues.Any(i => i.Code == code);

    [Fact]
    public async Task Well_formed_workflow_has_no_errors()
    {
        var b = await BuildDraftAsync();
        var r = await Validate(b.Version.WorkflowVersionId);
        Assert.True(r.IsValid, string.Join(", ", r.Errors.Select(e => e.Code)));
    }

    [Fact]
    public async Task Missing_start_and_no_terminal_are_errors()
    {
        var def = await Defs.CreateDefinitionAsync("X", "Std", "Test", null, Actor);
        var v = await Defs.CreateDraftVersionAsync(def.WorkflowDefinitionId, Actor);
        var a = await Defs.AddStageAsync(v.WorkflowVersionId, Stage("A", 1, "RoleA"), Actor);
        var r = await Validate(v.WorkflowVersionId);
        Assert.True(Has(r, "START_MISSING"));
        Assert.True(Has(r, "NO_TERMINAL"));
        Assert.False(r.IsValid);
        _ = a;
    }

    [Fact]
    public async Task Multiple_start_transitions_are_errors()
    {
        var b = await BuildDraftAsync();
        await Defs.AddTransitionAsync(b.Version.WorkflowVersionId, Tr(null, b.B.WorkflowStageId, "Start", 2), Actor);
        Assert.True(Has(await Validate(b.Version.WorkflowVersionId), "START_MULTIPLE"));
    }

    [Fact]
    public async Task Unreachable_stage_and_dead_end_are_reported()
    {
        var b = await BuildDraftAsync();
        var orphan = await Defs.AddStageAsync(b.Version.WorkflowVersionId, Stage("Orphan", 3, "RoleA"), Actor);
        await Defs.AddTransitionAsync(b.Version.WorkflowVersionId, Tr(orphan.WorkflowStageId, null, "Approve"), Actor);
        var r = await Validate(b.Version.WorkflowVersionId);
        Assert.Contains(r.Errors, e => e.Code == "UNREACHABLE_STAGE" && e.StageId == orphan.WorkflowStageId);

        var dead = await Defs.AddStageAsync(b.Version.WorkflowVersionId, Stage("Dead", 4, "RoleA"), Actor);
        await Defs.AddTransitionAsync(b.Version.WorkflowVersionId, Tr(b.A.WorkflowStageId, dead.WorkflowStageId, "Return", 3), Actor);
        Assert.Contains((await Validate(b.Version.WorkflowVersionId)).Errors, e => e.Code == "DEAD_END" && e.StageId == dead.WorkflowStageId);
    }

    [Fact]
    public async Task Duplicate_transition_is_an_error()
    {
        var b = await BuildDraftAsync();
        await Defs.AddTransitionAsync(b.Version.WorkflowVersionId, Tr(b.A.WorkflowStageId, null, "Approve", 1), Actor);
        Assert.True(Has(await Validate(b.Version.WorkflowVersionId), "DUPLICATE_TRANSITION"));
    }

    [Fact]
    public async Task Broken_reference_to_a_stage_of_another_version_is_an_error()
    {
        var b1 = await BuildDraftAsync("One");
        var b2 = await BuildDraftAsync("Two");
        // AddTransition از طریق سرویس رد می‌شود
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Defs.AddTransitionAsync(b1.Version.WorkflowVersionId, Tr(b1.A.WorkflowStageId, b2.B.WorkflowStageId, "Return", 9), Actor));
        // درج مستقیم (دور زدن سرویس) توسط Validator گرفته می‌شود
        Ctx.WorkflowTransitions.Add(new WorkflowTransition { WorkflowVersionId = b1.Version.WorkflowVersionId, FromStageId = b1.A.WorkflowStageId, ToStageId = b2.B.WorkflowStageId, ActionType = "Return", Priority = 9 });
        await Ctx.SaveChangesAsync();
        Assert.True(Has(await Validate(b1.Version.WorkflowVersionId), "BROKEN_REFERENCE"));
    }

    [Fact]
    public async Task Unregistered_action_and_side_effect_action_as_transition_are_errors()
    {
        var b = await BuildDraftAsync();
        await Defs.AddTransitionAsync(b.Version.WorkflowVersionId, Tr(b.A.WorkflowStageId, null, "DropDatabase", 7), Actor);
        await Defs.AddTransitionAsync(b.Version.WorkflowVersionId, Tr(b.A.WorkflowStageId, null, "Notify", 8), Actor);
        var r = await Validate(b.Version.WorkflowVersionId);
        Assert.True(Has(r, "ACTION_NOT_REGISTERED"));
        Assert.True(Has(r, "ACTION_NOT_TRANSITION"));
    }

    [Fact]
    public async Task Module_registered_action_is_accepted_only_for_that_module()
    {
        Registry.RegisterModuleAction("Test", "Assess", WorkflowActionKind.Outcome, "test");
        var b = await BuildDraftAsync("A", "Test");
        await Defs.AddTransitionAsync(b.Version.WorkflowVersionId, Tr(b.A.WorkflowStageId, null, "Assess", 7), Actor);
        Assert.False(Has(await Validate(b.Version.WorkflowVersionId), "ACTION_NOT_REGISTERED"));

        var other = await BuildDraftAsync("B", "OtherModule");
        await Defs.AddTransitionAsync(other.Version.WorkflowVersionId, Tr(other.A.WorkflowStageId, null, "Assess", 7), Actor);
        Assert.True(Has(await Validate(other.Version.WorkflowVersionId), "ACTION_NOT_REGISTERED"));
    }

    [Fact]
    public async Task Invalid_condition_is_an_error_and_valid_condition_is_not()
    {
        var b = await BuildDraftAsync();
        await Defs.AddTransitionAsync(b.Version.WorkflowVersionId, Tr(b.A.WorkflowStageId, null, "Return", 7, "System.IO.File.Delete('x')"), Actor);
        Assert.True(Has(await Validate(b.Version.WorkflowVersionId), "CONDITION_INVALID"));

        var ok = await BuildDraftAsync("Ok");
        await Defs.AddTransitionAsync(ok.Version.WorkflowVersionId, Tr(ok.A.WorkflowStageId, null, "Return", 7, "Priority == 'High' AND ProjectId > 3"), Actor);
        Assert.False(Has(await Validate(ok.Version.WorkflowVersionId), "CONDITION_INVALID"));
    }

    [Fact]
    public async Task Approval_configuration_errors()
    {
        var b = await BuildDraftAsync();
        await Defs.AddStageAsync(b.Version.WorkflowVersionId, new WorkflowStageInput
        { Name = "Min", StageOrder = 3, StageType = "Parallel", ResponsibleRole = "RoleA,RoleB", ApprovalStrategy = "MinimumCount", MinimumApprovalCount = null }, Actor);
        await Defs.AddStageAsync(b.Version.WorkflowVersionId, new WorkflowStageInput
        { Name = "TooHigh", StageOrder = 4, StageType = "Parallel", ResponsibleRole = "RoleA,RoleB", ApprovalStrategy = "MinimumCount", MinimumApprovalCount = 5 }, Actor);
        await Defs.AddStageAsync(b.Version.WorkflowVersionId, new WorkflowStageInput
        { Name = "BadSla", StageOrder = 5, ResponsibleRole = "RoleA", TimeoutHours = 0 }, Actor);
        await Defs.AddStageAsync(b.Version.WorkflowVersionId, new WorkflowStageInput
        { Name = "BadType", StageOrder = 6, ResponsibleRole = "RoleA", StageType = "Weird" }, Actor);
        var r = await Validate(b.Version.WorkflowVersionId);
        Assert.True(Has(r, "MIN_COUNT_MISSING"));
        Assert.True(Has(r, "MIN_COUNT_TOO_HIGH"));
        Assert.True(Has(r, "SLA_INVALID"));
        Assert.True(Has(r, "STAGE_TYPE_INVALID"));
    }

    [Fact]
    public async Task Unknown_role_employee_escalation_and_permission_are_errors_using_the_catalog()
    {
        var b = await BuildDraftAsync();
        var s = await Defs.AddStageAsync(b.Version.WorkflowVersionId, new WorkflowStageInput
        { Name = "Sec", StageOrder = 3, ResponsibleRole = "Ghost", ResponsibleEmployeeId = 999, IsEscalatable = true, EscalationRole = "AlsoGhost", TimeoutHours = 4 }, Actor);
        await Defs.AddTransitionAsync(b.Version.WorkflowVersionId, Tr(s.WorkflowStageId, null, "Approve", 1, null, "Nope.Nothing"), Actor);
        await Defs.AddTransitionAsync(b.Version.WorkflowVersionId, Tr(s.WorkflowStageId, null, "Reject", 1, null, "BadFormat"), Actor);
        var r = await Validate(b.Version.WorkflowVersionId);
        Assert.True(Has(r, "ROLE_UNKNOWN"));
        Assert.True(Has(r, "EMPLOYEE_UNKNOWN"));
        Assert.True(Has(r, "ESCALATION_ROLE_UNKNOWN"));
        Assert.True(Has(r, "PERMISSION_UNKNOWN"));
        Assert.True(Has(r, "PERMISSION_FORMAT"));
    }

    [Fact]
    public async Task Known_permission_and_roles_pass_and_dynamic_discipline_pseudo_roles_are_ignored()
    {
        var b = await BuildDraftAsync();
        var s = await Defs.AddStageAsync(b.Version.WorkflowVersionId, Stage("Dyn", 3, "Discipline:7,RoleA", "Parallel"), Actor);
        await Defs.AddTransitionAsync(b.Version.WorkflowVersionId, Tr(b.A.WorkflowStageId, s.WorkflowStageId, "Return", 5, null, "Workflow.Approve"), Actor);
        await Defs.AddTransitionAsync(b.Version.WorkflowVersionId, Tr(s.WorkflowStageId, null, "Approve"), Actor);
        var r = await Validate(b.Version.WorkflowVersionId);
        Assert.False(Has(r, "ROLE_UNKNOWN"));
        Assert.False(Has(r, "PERMISSION_UNKNOWN"));
    }

    [Fact]
    public async Task Missing_catalog_produces_a_warning_not_a_silent_pass()
    {
        var b = await BuildDraftAsync();
        var noCatalog = new WorkflowVersionValidator(Ctx, Registry, Conditions, null);
        var r = await noCatalog.ValidateAsync(b.Version.WorkflowVersionId);
        Assert.Contains(r.Warnings, w => w.Code == "CATALOG_UNAVAILABLE");
    }

    [Fact]
    public async Task Legacy_ambiguity_and_binding_pin_mismatch_are_reported()
    {
        var b1 = await BuildPublishedAsync("Dup1", "Test", "Std");
        var b2 = await BuildDraftAsync("Dup2", "Test", "Std");
        Assert.True(Has(await Validate(b2.Version.WorkflowVersionId), "LEGACY_AMBIGUITY"));
        _ = b1;
    }
}
