using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Persistence.Workflow;
using PEMS.Tests.TestSupport;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Tests.Cmms;

/// <summary>Phase 4 - Workflow Administration (facade over the existing platform). InMemory برای منطق؛ SQL واقعی برای Concurrency در کلاس جدا.</summary>
public class WorkflowAdministrationTests : IDisposable
{
    protected readonly PEMSDbContext Ctx = PemsDbContextFactory.CreateInMemory("wf-admin-" + Guid.NewGuid());
    protected readonly WorkflowAdministrationService Svc;
    protected readonly WorkflowAdminActor Admin1 = new(101, "Admin One", new[] { SystemRoles.SystemAdmin });
    protected readonly WorkflowAdminActor Admin2 = new(102, "Admin Two", new[] { SystemRoles.SystemAdmin });
    protected int WrDefinitionId, WrVersionId, WrBindingId, LegacyDefinitionId;

    public WorkflowAdministrationTests()
    {
        Ctx.Roles.Add(new ApplicationRole { Name = SystemRoles.SystemAdmin, NormalizedName = SystemRoles.SystemAdmin.ToUpperInvariant() });
        Ctx.SaveChanges();
        var sp = new ServiceCollection().AddSingleton(Ctx).BuildServiceProvider();
        WorkRequestWorkflowSeeder.SeedAsync(sp).GetAwaiter().GetResult();
        WorkflowManagementSeeder.SeedAsync(sp).GetAwaiter().GetResult();
        var def = Ctx.WorkflowDefinitions.First(d => d.Module == "WorkRequest");
        WrDefinitionId = def.WorkflowDefinitionId;
        WrVersionId = Ctx.WorkflowVersions.First(v => v.WorkflowDefinitionId == WrDefinitionId).WorkflowVersionId;
        WrBindingId = Ctx.WorkflowBindings.First(b => b.WorkflowDefinitionId == WrDefinitionId).WorkflowBindingId;
        // تعریف قدیمی بدون Binding (مثل VDR)
        var legacy = new WorkflowDefinition { Name = "Vendor Review", Category = "Review", Module = "VendorDocument", IsActive = true };
        Ctx.WorkflowDefinitions.Add(legacy); Ctx.SaveChanges();
        Ctx.WorkflowVersions.Add(new WorkflowVersion { WorkflowDefinitionId = legacy.WorkflowDefinitionId, VersionNumber = 1, Status = "Published", PublishedDate = DateTime.Now });
        Ctx.SaveChanges();
        LegacyDefinitionId = legacy.WorkflowDefinitionId;

        var validator = new WorkflowVersionValidator(Ctx, null, null, new PemsWorkflowReferenceCatalog(Ctx));
        Svc = new WorkflowAdministrationService(Ctx, new AccessControlService(Ctx), new WorkflowDefinitionService(Ctx, validator), new WorkflowBindingService(Ctx));
    }

    public void Dispose() => Ctx.Dispose();

    protected async Task<int> CloneAsync(WorkflowAdminActor a)
    {
        var r = await Svc.CloneVersionAsync(a, WrVersionId, "test");
        Assert.True(r.Success, r.Error);
        return r.Id!.Value;
    }

    protected async Task<WorkflowStage> FirstStageAsync(int versionId) => await Ctx.WorkflowStages.AsNoTracking().OrderBy(s => s.StageOrder).FirstAsync(s => s.WorkflowVersionId == versionId);

    protected async Task<int> AuditCount(string action) => await Ctx.WorkflowConfigAudits.CountAsync(a => a.Action == action);

    // ---------------- Authorization ----------------
    [Fact]
    public async Task Every_operation_requires_SystemAdmin_and_its_own_WorkflowManagement_permission()
    {
        var v = await CloneAsync(Admin1);
        foreach (var action in WorkflowManagementPermissions.All)
        {
            // فقط همین مجوز را از SystemAdmin بگیر (Grant غیرفعال) - SystemAdmin bypass برای WorkflowManagement وجود ندارد
            var perm = await Ctx.Permissions.FirstAsync(p => p.Name == "WorkflowManagement." + action);
            var grants = await Ctx.RolePermissionGrants.Where(g => g.PermissionId == perm.PermissionId).ToListAsync();
            grants.ForEach(g => g.IsActive = false); await Ctx.SaveChangesAsync();

            var denied = action switch
            {
                "View" => (await Svc.ListAsync(Admin1)).Count == 0,
                "Edit" => (await Svc.UpdateDefinitionAsync(Admin1, WrDefinitionId, "X", null, null)).IsForbidden,
                "Validate" => (await Svc.ValidateAsync(Admin1, v, null)).IsForbidden,
                "Publish" => (await Svc.PublishAsync(Admin2, v, null)).IsForbidden,
                "Bind" => (await Svc.ChangeBindingAsync(Admin1, WrBindingId, WrVersionId, true)).IsForbidden,
                _ => (await Svc.RetireAsync(Admin1, WrVersionId)).IsForbidden
            };
            Assert.True(denied, action + " must be denied without its permission");
            grants.ForEach(g => g.IsActive = true); await Ctx.SaveChangesAsync();
        }
        Assert.NotEmpty(await Svc.ListAsync(Admin1));                                                    // با همه‌ی مجوزها مجاز
    }

    [Fact]
    public async Task Non_SystemAdmin_is_denied_even_with_explicit_permission_grants()
    {
        var nonAdmin = new WorkflowAdminActor(200, "Ordinary", new[] { "Some.OtherRole" });
        var role = new ApplicationRole { Name = "Some.OtherRole", NormalizedName = "SOME.OTHERROLE" }; Ctx.Roles.Add(role); await Ctx.SaveChangesAsync();
        foreach (var p in await Ctx.Permissions.Where(p => p.Name.StartsWith("WorkflowManagement.")).ToListAsync())
            Ctx.RolePermissionGrants.Add(new RolePermissionGrant { RoleId = role.Id, PermissionId = p.PermissionId, IsActive = true, CreatedDate = DateTime.Now });
        await Ctx.SaveChangesAsync();
        Assert.Empty(await Svc.ListAsync(nonAdmin));
        Assert.True((await Svc.CloneVersionAsync(nonAdmin, WrVersionId, null)).IsForbidden);
        Assert.False((await Svc.AuthorizeAsync(nonAdmin, "View")).Success);
    }

    // ---------------- Definitions / policy ----------------
    [Fact]
    public async Task List_shows_bound_reference_definitions_as_editable_and_unbound_legacy_as_view_only_without_error_state()
    {
        var rows = await Svc.ListAsync(Admin1);
        var wr = rows.Single(r => r.DefinitionId == WrDefinitionId);
        var legacy = rows.Single(r => r.DefinitionId == LegacyDefinitionId);
        Assert.True(wr.Editable); Assert.Equal(1, wr.ActiveBindings);
        Assert.False(legacy.Editable); Assert.Equal(0, legacy.ActiveBindings); Assert.Null(legacy.BindingSummary);
        Assert.DoesNotMatch("(?i)error|broken|invalid", legacy.EditableReason);
        Assert.Equal(1, legacy.PublishedVersion);

        // فقط‌مشاهده: هیچ تغییری (Clone/Edit/Bind/Retire/Archive) روی تعریف قدیمی
        var lv = (await Ctx.WorkflowVersions.FirstAsync(v => v.WorkflowDefinitionId == LegacyDefinitionId)).WorkflowVersionId;
        Assert.False((await Svc.CloneVersionAsync(Admin1, lv, null)).Success);
        Assert.False((await Svc.UpdateDefinitionAsync(Admin1, LegacyDefinitionId, "Renamed", null, null)).Success);
        Assert.False((await Svc.RetireAsync(Admin1, lv)).Success);
        Assert.False((await Svc.ArchiveDefinitionAsync(Admin1, LegacyDefinitionId)).Success);
        Assert.NotNull(await Svc.GetDetailsAsync(Admin1, LegacyDefinitionId));                             // قابل مشاهده
        Assert.Equal("Vendor Review", (await Ctx.WorkflowDefinitions.AsNoTracking().FirstAsync(d => d.WorkflowDefinitionId == LegacyDefinitionId)).Name);
    }

    [Fact]
    public async Task Definition_metadata_edit_updates_NameFa_and_is_audited()
    {
        var r = await Svc.UpdateDefinitionAsync(Admin1, WrDefinitionId, "Work Request Approval", "تأیید درخواست کار", "desc");
        Assert.True(r.Success, r.Error);
        var d = await Ctx.WorkflowDefinitions.AsNoTracking().FirstAsync(x => x.WorkflowDefinitionId == WrDefinitionId);
        Assert.Equal("تأیید درخواست کار", d.NameFa); Assert.Equal("desc", d.Description);
        Assert.Equal(1, await AuditCount(WorkflowConfigAuditActions.DefinitionUpdated));
        Assert.False((await Svc.UpdateDefinitionAsync(Admin1, WrDefinitionId, "", null, null)).Success);   // نام الزامی
    }

    // ---------------- Lifecycle ----------------
    [Fact]
    public async Task Clone_edit_validate_publish_by_a_different_administrator_and_the_last_editor_cannot_publish()
    {
        var v = await CloneAsync(Admin1);
        var stage = await FirstStageAsync(v);
        var ed = new WorkflowStageEditModel { StageId = stage.WorkflowStageId, Name = stage.Name, NameFa = "مرحله‌ی ویرایش‌شده", StageOrder = stage.StageOrder, StageType = stage.StageType, ResponsibleRole = stage.ResponsibleRole, ApprovalStrategy = stage.ApprovalStrategy, IsMandatory = stage.IsMandatory, BusinessStatus = stage.BusinessStatus };
        Assert.True((await Svc.UpdateStageAsync(Admin1, v, null, ed)).Success);
        var val = await Svc.ValidateAsync(Admin1, v, null);
        Assert.True(val.Success, string.Join(",", val.Validation?.Errors.Select(e => e.Code) ?? Array.Empty<string>()));
        Assert.Equal("Validated", (await Ctx.WorkflowVersions.AsNoTracking().FirstAsync(x => x.WorkflowVersionId == v)).Status);

        var self = await Svc.PublishAsync(Admin1, v, null);                                                // آخرین ویرایشگر
        Assert.False(self.Success); Assert.True(self.IsForbidden);
        Assert.Equal("Validated", (await Ctx.WorkflowVersions.AsNoTracking().FirstAsync(x => x.WorkflowVersionId == v)).Status);

        var pub = await Svc.PublishAsync(Admin2, v, null);
        Assert.True(pub.Success, pub.Error);
        var pv = await Ctx.WorkflowVersions.AsNoTracking().FirstAsync(x => x.WorkflowVersionId == v);
        Assert.Equal("Published", pv.Status); Assert.Equal(102, pv.PublishedById);
        Assert.Equal("مرحله‌ی ویرایش‌شده", (await Ctx.WorkflowStages.AsNoTracking().FirstAsync(s => s.WorkflowStageId == stage.WorkflowStageId)).NameFa);

        // ممیزی کامل
        foreach (var a in new[] { WorkflowConfigAuditActions.VersionCloned, WorkflowConfigAuditActions.DraftEdited, WorkflowConfigAuditActions.Validated, WorkflowConfigAuditActions.Published })
            Assert.True(await AuditCount(a) >= 1, a);
    }

    [Fact]
    public async Task A_different_editor_after_the_last_edit_makes_that_person_the_last_editor()
    {
        var v = await CloneAsync(Admin1);
        var stage = await FirstStageAsync(v);
        var ed = new WorkflowStageEditModel { StageId = stage.WorkflowStageId, Name = stage.Name, StageOrder = stage.StageOrder, StageType = stage.StageType, ResponsibleRole = stage.ResponsibleRole, ApprovalStrategy = stage.ApprovalStrategy, IsMandatory = true, BusinessStatus = stage.BusinessStatus, Description = "by admin 2" };
        Assert.True((await Svc.UpdateStageAsync(Admin2, v, null, ed)).Success);
        Assert.True((await Svc.ValidateAsync(Admin2, v, null)).Success);
        Assert.True((await Svc.PublishAsync(Admin1, v, null)).Success);                                    // Admin1 دیگر آخرین ویرایشگر نیست
        var v2 = await CloneAsync(Admin1);
        Assert.True((await Svc.ValidateAsync(Admin1, v2, null)).Success);
        Assert.True((await Svc.PublishAsync(Admin1, v2, null)).IsForbidden);                               // Clone هم ویرایش است
    }

    [Fact]
    public async Task Published_and_retired_versions_are_immutable_through_the_administration_layer_and_the_platform()
    {
        var stage = await FirstStageAsync(WrVersionId);
        var ed = new WorkflowStageEditModel { StageId = stage.WorkflowStageId, Name = "Hack", StageOrder = stage.StageOrder, StageType = "Sequential", ApprovalStrategy = "AllApprovalsRequired" };
        Assert.False((await Svc.UpdateStageAsync(Admin1, WrVersionId, null, ed)).Success);
        Assert.False((await Svc.AddStageAsync(Admin1, WrVersionId, null, ed)).Success);
        Assert.False((await Svc.ValidateAsync(Admin1, WrVersionId, null)).Success);
        var defs = new WorkflowDefinitionService(Ctx);
        await Assert.ThrowsAsync<WorkflowImmutableVersionException>(() => defs.UpdateStageAsync(stage.WorkflowStageId, new WorkflowStageInput { Name = "Hack", StageOrder = 1 }, new WorkflowActor(1)));

        // Retire رد می‌شود تا Binding به نسخه پین است؛ پس از انتشار نسخه‌ی جدید و Rebind مجاز است
        Assert.False((await Svc.RetireAsync(Admin1, WrVersionId)).Success);
        var v = await CloneAsync(Admin1);
        Assert.True((await Svc.ValidateAsync(Admin1, v, null)).Success);
        Assert.True((await Svc.PublishAsync(Admin2, v, null)).Success);
        Assert.True((await Svc.ChangeBindingAsync(Admin1, WrBindingId, v, true)).Success);
        Assert.True((await Svc.RetireAsync(Admin1, WrVersionId)).Success);
        Assert.Equal("Retired", (await Ctx.WorkflowVersions.AsNoTracking().FirstAsync(x => x.WorkflowVersionId == WrVersionId)).Status);
        Assert.False((await Svc.UpdateStageAsync(Admin1, WrVersionId, null, ed)).Success);                  // Retired هم تغییرناپذیر
        Assert.Equal(1, await AuditCount(WorkflowConfigAuditActions.Retired));
        Assert.Equal(1, await AuditCount(WorkflowConfigAuditActions.BindingChanged));
    }

    [Fact]
    public async Task An_invalid_draft_cannot_be_validated_or_published()
    {
        var v = await CloneAsync(Admin1);
        foreach (var t in await Ctx.WorkflowTransitions.Where(t => t.WorkflowVersionId == v).ToListAsync())
            Assert.True((await Svc.RemoveTransitionAsync(Admin1, v, null, t.WorkflowTransitionId)).Success);
        var r = await Svc.ValidateAsync(Admin1, v, null);
        Assert.False(r.Success); Assert.NotNull(r.Validation); Assert.NotEmpty(r.Validation!.Errors);
        Assert.NotEmpty(r.Error!);
        Assert.False((await Svc.PublishAsync(Admin2, v, null)).Success);
        Assert.NotEqual("Published", (await Ctx.WorkflowVersions.AsNoTracking().FirstAsync(x => x.WorkflowVersionId == v)).Status);
        Assert.Equal(1, await AuditCount(WorkflowConfigAuditActions.ValidationFailed));
    }

    // ---------------- Draft management ----------------
    [Fact]
    public async Task Only_one_open_draft_per_definition_and_discard_frees_it_without_deleting_rows()
    {
        var v = await CloneAsync(Admin1);
        Assert.False((await Svc.CloneVersionAsync(Admin1, WrVersionId, null)).Success);                    // قاعده‌ی «یک Draft باز»
        var stages = await Ctx.WorkflowStages.CountAsync(s => s.WorkflowVersionId == v);
        var d = await Svc.DiscardDraftAsync(Admin1, v, null);
        Assert.True(d.Success, d.Error);
        var dv = await Ctx.WorkflowVersions.AsNoTracking().FirstAsync(x => x.WorkflowVersionId == v);
        Assert.Equal("Discarded", dv.Status);
        Assert.Equal(stages, await Ctx.WorkflowStages.CountAsync(s => s.WorkflowVersionId == v));         // حذف فیزیکی نشد
        Assert.Equal(1, await AuditCount(WorkflowConfigAuditActions.DraftDiscarded));
        Assert.False((await Svc.ValidateAsync(Admin1, v, null)).Success);                                  // Discarded تغییرناپذیر
        Assert.True((await Svc.CloneVersionAsync(Admin1, WrVersionId, null)).Success);                     // Draft جدید مجاز
        Assert.False((await Svc.DiscardDraftAsync(Admin1, WrVersionId, null)).Success);                    // Published قابل رها کردن نیست
    }

    [Fact]
    public async Task A_draft_referenced_by_an_instance_cannot_be_discarded()
    {
        var v = await CloneAsync(Admin1);
        var stage = await FirstStageAsync(v);
        Ctx.WorkflowInstances.Add(new WorkflowInstance { WorkflowVersionId = v, Module = "WorkRequest", BusinessObjectType = "WorkRequest", BusinessObjectId = 999, Status = "Running", CurrentStageId = stage.WorkflowStageId });
        await Ctx.SaveChangesAsync();
        Assert.False((await Svc.DiscardDraftAsync(Admin1, v, null)).Success);
    }

    // ---------------- Designer operations ----------------
    [Fact]
    public async Task Stage_and_transition_editing_covers_add_update_remove_and_preserves_unexposed_stage_fields()
    {
        var v = await CloneAsync(Admin1);
        var first = await FirstStageAsync(v);
        // فیلدهایی که UI ویرایش نمی‌کند نباید با UpdateStage پاک شوند
        var tracked = await Ctx.WorkflowStages.FirstAsync(s => s.WorkflowStageId == first.WorkflowStageId);
        tracked.IsSkippable = true; tracked.EscalationRole = "SomeRole"; tracked.NotificationKey = "n.key"; await Ctx.SaveChangesAsync();
        Ctx.ChangeTracker.Clear();
        Assert.True((await Svc.UpdateStageAsync(Admin1, v, null, new WorkflowStageEditModel { StageId = first.WorkflowStageId, Name = first.Name, NameFa = "الف", StageOrder = first.StageOrder, StageType = first.StageType, ResponsibleRole = first.ResponsibleRole, ApprovalStrategy = first.ApprovalStrategy, IsMandatory = true })).Success);
        var after = await Ctx.WorkflowStages.AsNoTracking().FirstAsync(s => s.WorkflowStageId == first.WorkflowStageId);
        Assert.True(after.IsSkippable); Assert.Equal("SomeRole", after.EscalationRole); Assert.Equal("n.key", after.NotificationKey); Assert.Equal("الف", after.NameFa);

        // add stage + transitions
        var add = await Svc.AddStageAsync(Admin1, v, null, new WorkflowStageEditModel { Name = "Extra Review", NameFa = "بازبینی اضافه", StageOrder = 0, ResponsibleRole = first.ResponsibleRole, StageType = "Sequential", ApprovalStrategy = "AllApprovalsRequired", IsMandatory = true, BusinessStatus = "UnderReview" });
        Assert.True(add.Success, add.Error);
        var extra = await Ctx.WorkflowStages.AsNoTracking().FirstAsync(s => s.WorkflowVersionId == v && s.Name == "Extra Review");
        Assert.True(extra.StageOrder > 0);
        var t = await Svc.AddTransitionAsync(Admin1, v, null, new WorkflowTransitionEditModel { FromStageId = first.WorkflowStageId, ToStageId = extra.WorkflowStageId, ActionType = "Approve", Label = "To extra", LabelFa = "به اضافه", Priority = 5 });
        Assert.True(t.Success, t.Error);
        var tr = await Ctx.WorkflowTransitions.AsNoTracking().FirstAsync(x => x.WorkflowVersionId == v && x.ToStageId == extra.WorkflowStageId);
        var up = await Svc.UpdateTransitionAsync(Admin1, v, null, new WorkflowTransitionEditModel { TransitionId = tr.WorkflowTransitionId, FromStageId = first.WorkflowStageId, ToStageId = extra.WorkflowStageId, ActionType = "Approve", Label = "Renamed", LabelFa = "نام جدید", RequiredPermission = "WorkRequest.Approve", Priority = 7, ForbidInitiator = true });
        Assert.True(up.Success, up.Error);
        var tr2 = await Ctx.WorkflowTransitions.AsNoTracking().FirstAsync(x => x.WorkflowTransitionId == tr.WorkflowTransitionId);
        Assert.Equal(("Renamed", "نام جدید", "WorkRequest.Approve", 7, true), (tr2.Label, tr2.LabelFa, tr2.RequiredPermission, tr2.Priority, tr2.ForbidInitiator));
        // مبدأ/مقصد از نسخه‌ی دیگر رد می‌شود؛ گذار نسخه‌ی دیگر قابل ویرایش/حذف از این مسیر نیست
        var foreign = await Ctx.WorkflowStages.AsNoTracking().FirstAsync(s => s.WorkflowVersionId == WrVersionId);
        Assert.False((await Svc.UpdateTransitionAsync(Admin1, v, null, new WorkflowTransitionEditModel { TransitionId = tr.WorkflowTransitionId, FromStageId = foreign.WorkflowStageId, ToStageId = extra.WorkflowStageId, ActionType = "Approve" })).Success);
        var foreignTr = await Ctx.WorkflowTransitions.AsNoTracking().FirstAsync(x => x.WorkflowVersionId == WrVersionId);
        Assert.False((await Svc.RemoveTransitionAsync(Admin1, v, null, foreignTr.WorkflowTransitionId)).Success);
        Assert.False((await Svc.RemoveStageAsync(Admin1, v, null, foreign.WorkflowStageId)).Success);

        Assert.True((await Svc.RemoveTransitionAsync(Admin1, v, null, tr.WorkflowTransitionId)).Success);
        Assert.True((await Svc.RemoveStageAsync(Admin1, v, null, extra.WorkflowStageId)).Success);
        Assert.False(await Ctx.WorkflowStages.AnyAsync(s => s.WorkflowStageId == extra.WorkflowStageId));
        // حذف مرحله‌ای که گراف را نامعتبر می‌کند ذخیره می‌شود ولی اعتبارسنجی سمت سرور آن را رد می‌کند
        Assert.True((await Svc.RemoveStageAsync(Admin1, v, null, first.WorkflowStageId)).Success);
        Assert.False((await Svc.ValidateAsync(Admin1, v, null)).Success);
    }

    [Fact]
    public async Task Designer_model_is_generated_from_the_real_version_with_a_deterministic_non_overlapping_layout()
    {
        var m1 = await Svc.GetDesignerAsync(Admin1, WrVersionId);
        var m2 = await Svc.GetDesignerAsync(Admin1, WrVersionId);
        Assert.NotNull(m1); Assert.False(m1!.CanEditStructure);                                            // Published: فقط‌خواندنی
        var stages = await Ctx.WorkflowStages.CountAsync(s => s.WorkflowVersionId == WrVersionId);
        var trs = await Ctx.WorkflowTransitions.CountAsync(t => t.WorkflowVersionId == WrVersionId);
        Assert.Equal(stages, m1.Graph.Nodes.Count); Assert.Equal(trs, m1.Graph.Edges.Count);
        Assert.Equal(string.Join("|", m1.Graph.Nodes.Select(n => $"{n.StageId}:{n.X}:{n.Y}")), string.Join("|", m2!.Graph.Nodes.Select(n => $"{n.StageId}:{n.X}:{n.Y}")));
        foreach (var a in m1.Graph.Nodes) foreach (var b in m1.Graph.Nodes.Where(x => x.StageId > a.StageId))
            Assert.False(a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H, "nodes overlap");
        Assert.All(m1.Graph.Nodes, n => Assert.True(n.X >= 0 && n.Y >= 0 && n.X + n.W <= m1.Graph.Width));
        Assert.Contains(m1.Graph.Edges, e => e.Kind == "Start");
        Assert.Contains(m1.Graph.Edges, e => e.Kind == "Terminal");
    }

    [Fact]
    public void Layout_places_branches_side_by_side_and_routes_returns_as_back_edges()
    {
        WorkflowStage S(int id, int order) => new() { WorkflowStageId = id, Name = "S" + id, StageOrder = order };
        WorkflowTransition T(int id, int? f, int? t, string a) => new() { WorkflowTransitionId = id, FromStageId = f, ToStageId = t, ActionType = a };
        var g = WorkflowGraphLayout.Compute(new[] { S(1, 1), S(2, 2), S(3, 3), S(4, 4) },
            new[] { T(1, null, 1, "Start"), T(2, 1, 2, "Approve"), T(3, 1, 3, "Hold"), T(4, 2, 4, "Approve"), T(5, 3, 4, "Approve"), T(6, 4, 1, "Return"), T(7, 4, null, "Reject") });
        var n = g.Nodes.ToDictionary(x => x.StageId);
        Assert.Equal(n[2].Y, n[3].Y); Assert.NotEqual(n[2].X, n[3].X);                                       // شاخه‌ها هم‌سطح
        Assert.True(n[4].Y > n[2].Y && n[2].Y > n[1].Y);
        Assert.Equal("Back", g.Edges.Single(e => e.TransitionId == 6).Kind);
        Assert.Equal("Terminal", g.Edges.Single(e => e.TransitionId == 7).Kind);
        Assert.Equal("Start", g.Edges.Single(e => e.TransitionId == 1).Kind);
        Assert.Equal(7, g.Edges.Count);
    }

    // ---------------- Binding / pinning ----------------
    [Fact]
    public async Task Rebinding_needs_confirmation_targets_a_published_version_and_never_touches_existing_pinned_instances()
    {
        var stage = await FirstStageAsync(WrVersionId);
        Ctx.WorkflowInstances.Add(new WorkflowInstance { WorkflowVersionId = WrVersionId, Module = "WorkRequest", BusinessObjectType = "WorkRequest", BusinessObjectId = 1, Status = "Running", CurrentStageId = stage.WorkflowStageId });
        await Ctx.SaveChangesAsync();
        var v = await CloneAsync(Admin1);
        Assert.False((await Svc.ChangeBindingAsync(Admin1, WrBindingId, v, true)).Success);                 // Draft قابل Bind نیست
        Assert.True((await Svc.ValidateAsync(Admin1, v, null)).Success);
        Assert.True((await Svc.PublishAsync(Admin2, v, null)).Success);
        var impact = await Svc.GetBindingImpactAsync(Admin1, WrBindingId, v);
        Assert.Equal(1, impact!.RunningInstances); Assert.Equal(1, impact.CurrentVersion); Assert.Equal(2, impact.TargetVersion);
        Assert.False((await Svc.ChangeBindingAsync(Admin1, WrBindingId, v, confirmed: false)).Success);     // بدون تأیید صریح هیچ تغییری
        Assert.Equal(WrVersionId, (await Ctx.WorkflowBindings.AsNoTracking().FirstAsync(b => b.WorkflowBindingId == WrBindingId)).PinnedVersionId);
        Assert.True((await Svc.ChangeBindingAsync(Admin1, WrBindingId, v, confirmed: true)).Success);
        Assert.Equal(v, (await Ctx.WorkflowBindings.AsNoTracking().FirstAsync(b => b.WorkflowBindingId == WrBindingId)).PinnedVersionId);
        Assert.Equal(WrVersionId, (await Ctx.WorkflowInstances.AsNoTracking().FirstAsync(i => i.BusinessObjectId == 1)).WorkflowVersionId);   // Instance موجود همچنان روی نسخه‌ی قبلی

        // بازگشت = Rebind به نسخه‌ی قدیمی Published (Rollback با نسخه‌ی جدید/Rebind صریح)
        Assert.True((await Svc.ChangeBindingAsync(Admin1, WrBindingId, WrVersionId, true)).Success);
        var details = await Svc.GetDetailsAsync(Admin1, WrDefinitionId);
        Assert.Equal(1, details!.Usage.Single(u => u.VersionNumber == 1).Running);
        Assert.True(details.Versions.Single(x => x.VersionNumber == 1).IsBound);
    }
}
