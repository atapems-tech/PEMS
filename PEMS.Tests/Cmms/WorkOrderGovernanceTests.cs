using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.DTOs;
using PEMS.Domain.Common;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Persistence.Workflow;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Tests.Cmms;

public class WorkOrderWorkflowStructureTests : WorkOrderGovernanceTestBase
{
    [Fact]
    public async Task Lifecycle_definition_is_published_bound_explicitly_and_pinned_and_seeding_is_idempotent()
    {
        var sp = new Microsoft.Extensions.DependencyInjection.ServiceCollection().AddSingleton(Ctx).BuildServiceProvider();
        await WorkOrderLifecycleWorkflowSeeder.SeedAsync(sp);
        await WorkOrderGovernanceSeeder.SeedAsync(sp);
        // طبق F2: نسخه‌ی ۲ (Released/Execution Authorized) از قبل توسط پایه‌ی تست Seed شده - این فراخوانی
        // دوباره فقط Idempotency خودِ V2 Seeder را هم تأیید می‌کند.
        await WorkOrderLifecycleWorkflowV2Seeder.SeedAsync(sp);

        var def = await Ctx.WorkflowDefinitions.SingleAsync(d => d.Name == WorkOrderWorkflow.LifecycleDefinitionName);
        // طبق F2: از این پس دو نسخه وجود دارد (v1 Published قدیمی برای Instance های موجود، v2 Published تازه
        // که Binding روی آن Pin شده) - آخرین/Pin‌شده همان v2 است.
        var v = await Ctx.WorkflowVersions.Where(x => x.WorkflowDefinitionId == def.WorkflowDefinitionId).OrderByDescending(x => x.VersionNumber).FirstAsync();
        Assert.Equal("Published", v.Status);
        Assert.Equal(2, await Ctx.WorkflowVersions.CountAsync(x => x.WorkflowDefinitionId == def.WorkflowDefinitionId));
        var b = await Ctx.WorkflowBindings.SingleAsync(x => x.EntityType == WorkOrderWorkflow.LifecycleEntityType);
        Assert.Equal(v.WorkflowVersionId, b.PinnedVersionId);
        Assert.True(b.IsActive);
        Assert.Equal(new[] { "Created", "Maintenance Planning", "Preparation / Assignment", "Operations Issue / Release", "Released / Execution Authorized", "In Execution", "On Hold", "Completion", "Originating-Discipline Verification", "Final Approval" },
            (await Ctx.WorkflowStages.Where(s => s.WorkflowVersionId == v.WorkflowVersionId).OrderBy(s => s.StageOrder).Select(s => s.Name).ToListAsync()).ToArray());
        // بازبینی: Binding صریح با هویت جدا
        var rb = await Ctx.WorkflowBindings.SingleAsync(x => x.EntityType == WorkOrderWorkflow.ReviewEntityType);
        Assert.NotEqual(b.WorkflowBindingId, rb.WorkflowBindingId);
        Assert.Equal(1, await Ctx.WorkflowBindings.CountAsync(x => x.EntityType == WorkOrderWorkflow.LifecycleEntityType));
    }

    [Fact]
    public async Task Published_lifecycle_version_is_immutable()
    {
        // طبق F2: از این پس دو نسخه‌ی Published وجود دارد (v1 قدیمی + v2 Pin‌شده) - این تست دقیقاً
        // خودِ نسخه‌ی جاری/Pin‌شده (v2) را می‌سنجد، نه فرض «فقط یک نسخه هست».
        var v = await Ctx.WorkflowVersions.Include(x => x.Definition).Where(x => x.Definition!.Name == WorkOrderWorkflow.LifecycleDefinitionName)
            .OrderByDescending(x => x.VersionNumber).FirstAsync();
        var defs = new WorkflowDefinitionService(Ctx, new WorkflowVersionValidator(Ctx, WorkOrderWorkflow.CreateRegistry(), new WorkflowConditionEvaluator(), new PemsWorkflowReferenceCatalog(Ctx)));
        await Assert.ThrowsAsync<WorkflowImmutableVersionException>(() => defs.AddStageAsync(v.WorkflowVersionId, new WorkflowStageInput { Name = "X", StageOrder = 99, ResponsibleRole = "Permission:WorkOrder.Plan" }, new WorkflowActor(1)));
    }

    [Fact]
    public async Task Every_transition_requires_exactly_one_known_task_permission_and_the_validator_accepts_the_definition()
    {
        // طبق F2: باید روی هر دو نسخه‌ی Published (v1 قدیمی که Instance های در حال اجرا هنوز
        // رویش هستند + v2 Pin‌شده) این پایانامه برقرار بماند - نه فقط جدیدترین.
        var versions = await Ctx.WorkflowVersions.Include(x => x.Definition)
            .Where(x => x.Definition!.Name == WorkOrderWorkflow.LifecycleDefinitionName).ToListAsync();
        Assert.NotEmpty(versions);
        foreach (var v in versions)
        {
            var trs = await Ctx.WorkflowTransitions.Where(t => t.WorkflowVersionId == v.WorkflowVersionId && t.FromStageId != null).ToListAsync();
            Assert.All(trs, t => Assert.Contains(t.RequiredPermission, WorkOrderWorkflow.Perm.Workflow));
            // طبق «PEMS Authorization Architecture Completion» فاز ۵ بند ۲۶: علاوه بر عضویت در
            // آرایه‌ی C#، باید یک ردیف Permission واقعی در کاتالوگ هم برایش موجود باشد - وگرنه
            // گذار به‌خاموشی برای همه غیرقابل‌استفاده می‌شود (هیچ RolePermissionGrant ای هرگز
            // نمی‌تواند به یک Permission ناموجود اشاره کند).
            var permissionNames = await Ctx.Permissions.Select(p => p.Name).ToListAsync();
            Assert.All(trs, t => Assert.Contains(t.RequiredPermission, permissionNames));
            var r = await new WorkflowVersionValidator(Ctx, WorkOrderWorkflow.CreateRegistry(), new WorkflowConditionEvaluator(), new PemsWorkflowReferenceCatalog(Ctx)).ValidateAsync(v.WorkflowVersionId);
            Assert.True(r.IsValid, $"v{v.VersionNumber}: " + string.Join(",", r.Errors.Select(e => e.Code)));
        }
    }

    [Fact]
    public async Task Governance_seeder_removes_broad_engineering_authority_and_keeps_only_originating_discipline_rights()
    {
        var access = new AccessControlService(Ctx);
        foreach (var role in new[] { SystemRoles.EngineeringManager, SystemRoles.EngineeringSupervisor, SystemRoles.DisciplineEngineer, SystemRoles.MaintenanceEngineer })
        {
            Assert.True((await access.AuthorizeForRolesAsync(new[] { role }, "WorkOrder", "View")).Allowed, role + " View");
            Assert.False((await access.AuthorizeForRolesAsync(new[] { role }, "WorkOrder", "Create")).Allowed, role + " Create");
            Assert.False((await access.AuthorizeForRolesAsync(new[] { role }, "WorkOrder", "Approve")).Allowed, role + " Approve");
            Assert.False((await access.AuthorizeForRolesAsync(new[] { role }, "WorkOrder", "Execute")).Allowed, role + " Execute");
        }
        Assert.True((await access.AuthorizeForRolesAsync(new[] { SystemRoles.DisciplineEngineer }, "WorkOrder", "Verify")).Allowed);
        Assert.True((await access.AuthorizeForRolesAsync(new[] { SystemRoles.EngineeringSupervisor }, "WorkOrder", "FinalApprove")).Allowed);
        Assert.False((await access.AuthorizeForRolesAsync(new[] { SystemRoles.EngineeringSupervisor }, "WorkOrder", "Issue")).Allowed);
        // Warehouse/Operations/Maintenance جدا
        Assert.True((await access.AuthorizeForRolesAsync(new[] { "OPS.IssueAuthority" }, "WorkOrder", "Issue")).Allowed);
        Assert.False((await access.AuthorizeForRolesAsync(new[] { "OPS.IssueAuthority" }, "WorkOrder", "Execute")).Allowed);
        Assert.False((await access.AuthorizeForRolesAsync(new[] { "MNT.Expert" }, "WorkOrder", "Issue")).Allowed);
        Assert.False((await access.AuthorizeForRolesAsync(new[] { "MNT.PlanningHead" }, "WorkOrder", "Execute")).Allowed);
        // هیچ ردیف حذف نشد
        Assert.Equal(4, await Ctx.RolePermissions.CountAsync(x => x.ModuleName == "WorkOrder" && (x.RoleName == SystemRoles.MaintenanceEngineer || x.RoleName == SystemRoles.EngineeringManager || x.RoleName == SystemRoles.EngineeringSupervisor || x.RoleName == SystemRoles.DisciplineEngineer)));
    }

    [Fact]
    public async Task Department_domains_are_backfilled_by_name_and_engineering_and_maintenance_mechanical_are_distinct_identities()
    {
        Assert.Equal(OrganizationDomains.Maintenance, await WorkOrderWorkflow.ResolveDepartmentDomainAsync(Ctx, DeptMaint));
        Assert.Equal(OrganizationDomains.Engineering, await WorkOrderWorkflow.ResolveDepartmentDomainAsync(Ctx, DeptEng));
        Assert.Equal(OrganizationDomains.Operations, await WorkOrderWorkflow.ResolveDepartmentDomainAsync(Ctx, DeptOps));
        var m = await WorkOrderWorkflow.ResolveEmployeeAsync(Ctx, Executor.EmployeeId);
        var e = await WorkOrderWorkflow.ResolveEmployeeAsync(Ctx, EngVerifier.EmployeeId);
        Assert.Equal(m.DisciplineId, e.DisciplineId);          // همان دیسیپلین Mechanical
        Assert.NotEqual(m.Domain, e.Domain);                    // ولی هویت سازمانی متفاوت
    }

    [Fact]
    public async Task Department_domain_is_inherited_from_the_parent_department_and_unknown_domain_fails_closed()
    {
        var parent = new Department { Name = "Root", Code = "R", OrganizationDomain = OrganizationDomains.Maintenance };
        Ctx.Departments.Add(parent); await Ctx.SaveChangesAsync();
        var child = new Department { Name = "Child", Code = "C", ParentDepartmentId = parent.DepartmentId };
        var orphan = new Department { Name = "Orphan", Code = "O" };
        Ctx.Departments.AddRange(child, orphan); await Ctx.SaveChangesAsync();
        Assert.Equal(OrganizationDomains.Maintenance, await WorkOrderWorkflow.ResolveDepartmentDomainAsync(Ctx, child.DepartmentId));
        Assert.Null(await WorkOrderWorkflow.ResolveDepartmentDomainAsync(Ctx, orphan.DepartmentId));

        var noDomain = Actor(orphan.DepartmentId, Mech, "MNT.PlanningExpert");
        var (ok, err, _) = await Svc.CreateGovernedAsync(new WorkOrderCreateRequest { WorkOrderNumber = "WO-X", EquipmentId = EquipmentId, WorkOrderType = "Preventive", Priority = "Low" }, noDomain);
        Assert.False(ok); Assert.Matches("مشخص نیست|not defined", err);
    }
}

public class WorkOrderCreationAndLifecycleTests : WorkOrderGovernanceTestBase
{
    [Fact]
    public async Task Creation_starts_the_pinned_lifecycle_and_snapshots_the_originating_discipline()
    {
        var id = await CreateAsync();
        var wo = await Wo(id);
        Assert.Equal("Open", wo.Status);
        Assert.Equal(OrganizationDomains.Maintenance, wo.OriginatingOrganization);
        Assert.Equal(Mech, wo.OriginatingDisciplineId);
        Assert.Equal(Creator.EmployeeId, wo.OriginatingRequestedById);
        Assert.Null(wo.ResponsibleDisciplineId);

        var inst = await Ctx.WorkflowInstances.SingleAsync(i => i.EntityKey == WorkOrderWorkflow.LifecycleKey(id));
        var binding = await Ctx.WorkflowBindings.SingleAsync(b => b.EntityType == WorkOrderWorkflow.LifecycleEntityType);
        Assert.Equal(binding.WorkflowBindingId, inst.WorkflowBindingId);
        Assert.Equal(binding.PinnedVersionId, inst.WorkflowVersionId);
        Assert.Equal(1, await Ctx.WorkflowHistories.CountAsync(h => h.WorkflowInstanceId == inst.WorkflowInstanceId && h.ActionType == "Start"));
        Assert.Contains(await Ctx.WorkOrderStatusHistories.Where(h => h.WorkOrderId == id).ToListAsync(), h => h.ToStatus == "Open");
    }

    [Fact]
    public async Task Creation_requires_create_authority_a_known_organization_and_an_active_binding()
    {
        var req = new WorkOrderCreateRequest { WorkOrderNumber = "WO-Z", EquipmentId = EquipmentId, WorkOrderType = "Preventive", Priority = "Low" };
        Assert.False((await Svc.CreateGovernedAsync(req, EngManager)).Success);                                  // Engineering: بدون اختیار Create
        Assert.False((await Svc.CreateGovernedAsync(req, Executor)).Success);                                    // discipline membership ≠ create authority
        Assert.False((await Svc.CreateGovernedAsync(new WorkOrderCreateRequest { WorkOrderNumber = "WO-Y", EquipmentId = EquipmentId, WorkOrderType = "Shutdown", Priority = "Low" }, Creator)).Success); // نوع تعریف‌نشده
        Assert.Equal(0, await Ctx.WorkOrders.CountAsync());

        await Ctx.WorkflowBindings.Where(b => b.EntityType == WorkOrderWorkflow.LifecycleEntityType).ExecuteUpdateAsyncCompat(Ctx);
        var (ok, err, _) = await Svc.CreateGovernedAsync(req, Creator);
        Assert.False(ok); Assert.Matches("پیکربندی|not configured", err);
        Assert.Equal(0, await Ctx.WorkOrders.CountAsync());
    }

    [Fact]
    public async Task Full_lifecycle_for_every_approved_type_reaches_Closed_through_all_mandatory_stages_with_status_projection()
    {
        foreach (var type in new[] { "Preventive", "Corrective", "Predictive", "Emergency" })
        {
            var id = await CreateAsync(type);
            var statuses = new List<string> { (await Wo(id)).Status };
            async Task Snap() => statuses.Add((await Wo(id)).Status);

            await Do(id, "Approve", Creator); await Snap();                                            // → Planning
            Assert.True((await Svc.SetPlanningAsync(id, Mech, Planner)).Success);
            await Do(id, "Approve", Planner); await Snap();                                            // → Preparation
            Assert.True((await Svc.AssignExecutorAsync(id, null, Executor.EmployeeId, DisciplineHead)).Success);
            await Do(id, "Approve", Planner); await Snap();                                            // → Issue
            await Do(id, "Approve", Ops); await Snap();                                                // → Released / Execution Authorized (F2)
            await Do(id, "Approve", DisciplineHead); await Snap();                                      // → Start Work → Execution (F2)
            await Do(id, "Approve", Executor, null, Data()); await Snap();                              // → Completion
            await Do(id, "Approve", Executor); await Snap();                                           // → Verification
            await Do(id, "Approve", Verifier); await Snap();                                           // → Final Approval
            await Do(id, "Approve", Approver); await Snap();                                           // → Closed

            Assert.Equal(new[] { "Open", "Open", "Assigned", "Assigned", "Assigned", "InProgress", "Completed", "Completed", "Completed", "Closed" }, statuses.ToArray());
            var inst = await Ctx.WorkflowInstances.SingleAsync(i => i.EntityKey == WorkOrderWorkflow.LifecycleKey(id));
            Assert.Equal("Completed", inst.Status);
            var wo = await Wo(id);
            Assert.NotNull(wo.StartDate); Assert.NotNull(wo.CompletionDate);
            Assert.Equal(Mech, wo.ResponsibleDisciplineId); Assert.Equal(OrganizationDomains.Maintenance, wo.ResponsibleOrganization);
            Assert.Equal(1, await Ctx.MaintenanceHistories.CountAsync(m => m.WorkOrderId == id));
            Assert.Equal(type is "Corrective" or "Emergency" ? 1 : 0, await Ctx.FailureRecords.CountAsync(f => f.WorkOrderId == id));
        }
    }

    [Fact]
    public async Task Completion_rules_stay_in_the_work_order_domain()
    {
        var id = await CreateAsync("Corrective");
        await PlanAsync(id); await PrepareAndIssueAsync(id);
        var err = await Fail(id, "Approve", Executor, new WorkOrderCompletionData());                    // ISO 14224 حالت خرابی الزامی
        Assert.Matches("ISO 14224", err);
        Assert.Equal("InProgress", (await Wo(id)).Status);
        Assert.Equal(0, await Ctx.MaintenanceHistories.CountAsync());
        await Do(id, "Approve", Executor, null, Data());
        Assert.Equal("Leakage", (await Wo(id)).FailureModeCode);
    }

    [Fact]
    public async Task Planning_and_preparation_domain_rules_block_the_transition_without_changing_state()
    {
        var id = await CreateAsync();
        await Do(id, "Approve", Creator);
        Assert.Matches("دیسیپلین مسئول|responsible maintenance discipline", await Fail(id, "Approve", Planner));   // بدون دیسیپلین مسئول
        Assert.True((await Svc.SetPlanningAsync(id, Mech, Planner)).Success);
        await Do(id, "Approve", Planner);
        Assert.Matches("تیم یا مجری|team or executor", await Fail(id, "Approve", Planner));                        // بدون تخصیص
        Assert.Equal("Assigned", (await Wo(id)).Status);
    }

    [Fact]
    public async Task Issue_is_blocked_by_the_permit_release_gate_and_only_operations_can_release()
    {
        var jp = new JobPlan { Title = "JP", PermitRequired = true };
        Ctx.JobPlans.Add(jp); await Ctx.SaveChangesAsync();
        var plan = new MaintenancePlan { PlanNumber = "MP-1", Title = "PM", EquipmentId = EquipmentId, JobPlanId = jp.JobPlanId, IsActive = true };
        Ctx.MaintenancePlans.Add(plan); await Ctx.SaveChangesAsync();
        var (ok, err, id) = await Svc.CreateGovernedAsync(new WorkOrderCreateRequest { WorkOrderNumber = "WO-G", EquipmentId = EquipmentId, WorkOrderType = "Preventive", Priority = "Low", MaintenancePlanId = plan.MaintenancePlanId }, Creator);
        Assert.True(ok, err);
        await PlanAsync(id!.Value);
        Assert.True((await Svc.AssignExecutorAsync(id.Value, null, Executor.EmployeeId, DisciplineHead)).Success);
        await Do(id.Value, "Approve", Planner);                                                            // → Issue

        await Fail(id.Value, "Approve", Planner);                                                          // Maintenance Planning نمی‌تواند Operations را جایگزین کند
        await Fail(id.Value, "Approve", DisciplineHead);
        Assert.Matches("Permit|آزادسازی", await Fail(id.Value, "Approve", Ops));                           // Release Gate (قاعده‌ی دامنه)
        Assert.Equal("Assigned", (await Wo(id.Value)).Status);

        Ctx.Permits.Add(new Permit { PermitNumber = "PT-1", PermitType = "Hot", Title = "p", WorkOrderId = id.Value, Status = "Active", RequestedByEmployeeId = Ops.EmployeeId!.Value });
        await Ctx.SaveChangesAsync();
        await Do(id.Value, "Approve", Ops);
        // طبق F2: Issue/Release دیگر خودش InProgress نمی‌شود - فقط تا Released/Execution Authorized
        // می‌رود؛ StartDate هم هنوز ست نشده.
        Assert.Equal("Assigned", (await Wo(id.Value)).Status);
        Assert.Null((await Wo(id.Value)).StartDate);
        Assert.Equal("Released / Execution Authorized", (await Svc.GetWorkflowViewAsync(id.Value, DisciplineHead))!.CurrentStageName);

        await Do(id.Value, "Approve", DisciplineHead);                                                // Start Work
        Assert.Equal("InProgress", (await Wo(id.Value)).Status);
        Assert.NotNull((await Wo(id.Value)).StartDate);
    }

    [Fact]
    public async Task Hold_resume_return_reject_and_cancel_are_governed_transitions()
    {
        var id = await CreateAsync();
        await PlanAsync(id); await PrepareAndIssueAsync(id);
        await Do(id, "Hold", DisciplineHead);
        Assert.Equal("OnHold", (await Wo(id)).Status);
        await Fail(id, "Hold", Executor);                                                                   // Hold فقط سرگروه/معاون (Executor مجوز Hold ندارد)
        await Do(id, "Approve", DisciplineHead);                                                            // Resume
        Assert.Equal("InProgress", (await Wo(id)).Status);

        // Operations Decline → بازگشت به برنامه‌ریزی
        var id2 = await CreateAsync();
        await PlanAsync(id2);
        Assert.True((await Svc.AssignExecutorAsync(id2, null, Executor.EmployeeId, DisciplineHead)).Success);
        await Do(id2, "Approve", Planner);
        await Do(id2, "Return", Ops);
        Assert.Equal("Open", (await Wo(id2)).Status);
        Assert.Equal("Maintenance Planning", (await Svc.GetWorkflowViewAsync(id2, Planner))!.CurrentStageName);

        // Reject در Planning (Planning Head) و Cancel (Manager در اجرا)
        var id3 = await CreateAsync(); await Do(id3, "Approve", Creator);
        await Do(id3, "Reject", Planner);
        Assert.Equal("Rejected", (await Wo(id3)).Status);
        await Do(id, "Cancel", Manager);
        Assert.Equal("Cancelled", (await Wo(id)).Status);
        Assert.Equal("Cancelled", (await Ctx.WorkflowInstances.SingleAsync(i => i.EntityKey == WorkOrderWorkflow.LifecycleKey(id))).Status);
        Assert.Empty((await Svc.GetWorkflowViewAsync(id, Manager))!.AvailableActions);
        await Fail(id, "Approve", Executor);                                                                // پس از لغو هیچ اکشنی
    }

    [Fact]
    public async Task Rejected_transitions_write_no_history_and_no_business_audit()
    {
        var id = await CreateAsync();
        await Do(id, "Approve", Creator);
        var inst = await Ctx.WorkflowInstances.SingleAsync(i => i.EntityKey == WorkOrderWorkflow.LifecycleKey(id));
        var h = await Ctx.WorkflowHistories.CountAsync(x => x.WorkflowInstanceId == inst.WorkflowInstanceId);
        var a = await Ctx.WorkOrderStatusHistories.CountAsync(x => x.WorkOrderId == id);

        await Fail(id, "Approve", EngManager);       // Engineering: بدون مجوز Plan
        await Fail(id, "Approve", Ops);
        await Fail(id, "Cancel", Executor);
        Assert.Equal(h, await Ctx.WorkflowHistories.CountAsync(x => x.WorkflowInstanceId == inst.WorkflowInstanceId));
        Assert.Equal(a, await Ctx.WorkOrderStatusHistories.CountAsync(x => x.WorkOrderId == id));
        Assert.Equal("Open", (await Wo(id)).Status);
    }
}

public class WorkOrderAuthorizationAndSodTests : WorkOrderGovernanceTestBase
{
    [Fact]
    public async Task Organization_authorization_each_duty_belongs_only_to_its_organization()
    {
        var id = await CreateAsync();
        await PlanAsync(id);
        Assert.True((await Svc.AssignExecutorAsync(id, null, Executor.EmployeeId, DisciplineHead)).Success);
        var engPlanner = WithRoles(EngManager, "MNT.PlanningHead");                                         // نقش Maintenance روی کارمند Engineering
        Assert.Matches("نگهداری|Maintenance", await Fail(id, "Approve", engPlanner));                       // Prepare: دامنه Engineering مجاز نیست
        var opsAsMnt = WithRoles(Ops, "MNT.PlanningHead");
        Assert.Matches("نگهداری|Maintenance", await Fail(id, "Approve", opsAsMnt));
        await Do(id, "Approve", Planner);
        var mntAsOps = WithRoles(Planner, "OPS.IssueAuthority");                                            // کارمند Maintenance با نقش Operations
        Assert.Matches("عملیات|Operations", await Fail(id, "Approve", mntAsOps));
        await Do(id, "Approve", Ops);
        Assert.Matches("نگهداری|Maintenance", await Fail(id, "Approve", WithRoles(Ops, "MNT.Expert")));    // Execute فقط Maintenance
    }

    [Fact]
    public async Task Discipline_authorization_executor_must_belong_to_the_responsible_discipline()
    {
        var id = await CreateAsync();
        await PlanAsync(id, Mech); await PrepareAndIssueAsync(id);
        Assert.Matches("دیسیپلین مسئول|responsible maintenance discipline", await Fail(id, "Approve", ElecExecutor, Data()));
        Assert.Matches("دیسیپلین مسئول|responsible maintenance discipline", await Fail(id, "Hold", WithRoles(ElecExecutor, "MNT.DisciplineHead")));
        // عضویت در دیسیپلین ≠ مجوز: Mechanical Maintenance بدون مجوز Execute هم رد می‌شود
        await Fail(id, "Approve", WithRoles(Actor(DeptMaint, Mech, "MNT.PlanningExpert")), Data());
        await Do(id, "Approve", Executor, null, Data());
    }

    [Fact]
    public async Task Assignment_is_restricted_to_the_responsible_discipline_head_and_a_matching_executor()
    {
        var id = await CreateAsync();
        await PlanAsync(id);
        var elecHead = Actor(DeptMaint, Elec, "MNT.DisciplineHead");
        Assert.False((await Svc.AssignExecutorAsync(id, null, Executor.EmployeeId, elecHead)).Success);              // سرگروه دیسیپلین دیگر
        Assert.False((await Svc.AssignExecutorAsync(id, null, Executor.EmployeeId, Planner)).Success);               // بدون مجوز Assign
        Assert.False((await Svc.AssignExecutorAsync(id, null, ElecExecutor.EmployeeId, DisciplineHead)).Success);    // مجری از دیسیپلین دیگر
        Assert.False((await Svc.AssignExecutorAsync(id, null, EngVerifier.EmployeeId, DisciplineHead)).Success);     // مجری از سازمان Engineering
        Assert.True((await Svc.AssignExecutorAsync(id, null, Executor.EmployeeId, DisciplineHead)).Success);
        Assert.False((await Svc.SetPlanningAsync(id, Mech, EngManager)).Success);                                    // Engineering نمی‌تواند برنامه‌ریزی کند
    }

    [Fact]
    public async Task Engineering_Mechanical_is_not_Maintenance_Mechanical_for_verification_and_execution()
    {
        var id = await CreateAsync("Corrective");
        await MakeEngineeringOriginAsync(id);                                                                       // مبدأ: Engineering / Mechanical
        await RunToVerificationAsync(id);

        var maintMechVerifier = WithRoles(Verifier);                                                                 // Maintenance / Mechanical
        Assert.Matches("مبدأ|originating", await Fail(id, "Approve", maintMechVerifier));                            // همان دیسیپلین ولی سازمان دیگر
        await Fail(id, "Approve", WithRoles(Manager));                                                               // Manager فقط برای مبدأ Maintenance cross-discipline است
        Assert.Matches("مبدأ|originating", await Fail(id, "Approve", Actor(DeptEng, Elec, SystemRoles.DisciplineEngineer)));   // Engineering/Electrical ≠ مبدأ
        await Do(id, "Approve", EngVerifier);                                                                        // Engineering / Mechanical
        Assert.Matches("مبدأ|originating", await Fail(id, "Approve", WithRoles(Approver)));
        await Do(id, "Approve", EngApprover);
        var wo = await Wo(id);
        Assert.Equal("Closed", wo.Status);
        Assert.Equal(OrganizationDomains.Engineering, wo.OriginatingOrganization);
        Assert.Equal(OrganizationDomains.Maintenance, wo.ResponsibleOrganization);   // مسئول و مبدأ هرگز جایگزین هم نمی‌شوند
    }

    // ---------------- SoD: تک‌تک قواعد ----------------
    [Fact]
    public async Task Sod_creator_and_executor_are_allowed_to_be_the_same_person()
    {
        var creatorExec = WithRoles(Executor, "MNT.PlanningExpert");    // همان فرد: Create + Execute
        var id = await CreateAsync("Preventive", creatorExec);
        await Do(id, "Approve", creatorExec);
        Assert.True((await Svc.SetPlanningAsync(id, Mech, Planner)).Success);
        await Do(id, "Approve", Planner);
        Assert.True((await Svc.AssignExecutorAsync(id, null, creatorExec.EmployeeId, DisciplineHead)).Success);
        await Do(id, "Approve", Planner); await Do(id, "Approve", Ops);
        await Do(id, "Approve", creatorExec);                            // Start Work (F2) - همان فرد Create+Execute
        await Do(id, "Approve", creatorExec, null, Data(null));         // Creator ≠ Executor اجباری نیست
        Assert.Equal("Completed", (await Wo(id)).Status);
    }

    [Fact]
    public async Task Sod_planner_cannot_execute_and_operations_issuer_cannot_execute_or_the_reverse()
    {
        var id = await CreateAsync();
        var planner = WithRoles(Planner, "MNT.Expert");                    // برنامه‌ریز با مجوز Execute
        await Do(id, "Approve", Creator);
        Assert.True((await Svc.SetPlanningAsync(id, Mech, planner)).Success);
        await Do(id, "Approve", planner);
        Assert.True((await Svc.AssignExecutorAsync(id, null, Executor.EmployeeId, DisciplineHead)).Success);
        await Do(id, "Approve", planner);
        await Do(id, "Approve", Ops);
        Assert.Matches("برنامه‌ریز|planner", await Fail(id, "Approve", planner));                            // Planner ≠ Start Work (F2: همان SoD روی Start هم اعمال می‌شود)
        Assert.Equal("Assigned", (await Wo(id)).Status);
        await Do(id, "Approve", DisciplineHead);                                                            // Start Work توسط شخص ثالث بی‌طرف
        Assert.Equal("InProgress", (await Wo(id)).Status);
        Assert.Matches("برنامه‌ریز|planner", await Fail(id, "Approve", planner, Data()));                   // Planner ≠ Executor برای تکمیل هم
        Assert.Equal("InProgress", (await Wo(id)).Status);

        // Issuer ≠ Executor: صادرکننده بعداً به Maintenance منتقل شود و مجوز Execute بگیرد
        var dept2 = DeptMaint;
        var issuerEmp = await Ctx.Employees.FirstAsync(e => e.EmployeeId == Ops.EmployeeId);
        issuerEmp.DepartmentId = dept2; await Ctx.SaveChangesAsync();
        Assert.Matches("صادرکننده|issuer", await Fail(id, "Approve", WithRoles(Ops, "MNT.Expert"), Data()));
    }

    [Fact]
    public async Task Sod_executor_cannot_verify_and_verifier_cannot_give_final_approval()
    {
        var id = await CreateAsync("Preventive");
        await PlanAsync(id); await PrepareAndIssueAsync(id);
        await Do(id, "Approve", Executor, null, Data(null)); await Do(id, "Approve", Executor);
        Assert.Matches("مجری|executor", await Fail(id, "Approve", WithRoles(Executor, "MNT.SeniorExpert")));   // Executor ≠ Verifier
        var verifier = WithRoles(Verifier, "MNT.DisciplineHead");
        await Do(id, "Approve", verifier);
        Assert.Matches("تأییدکننده|verifier", await Fail(id, "Approve", verifier));                              // Verifier ≠ Final Approver
        Assert.Matches("مجری|executor", await Fail(id, "Approve", WithRoles(Executor, "MNT.DisciplineHead")));  // Executor ≠ Final Approver
        Assert.Equal("Completed", (await Wo(id)).Status);
        await Do(id, "Approve", Approver);
        Assert.Equal("Closed", (await Wo(id)).Status);
    }

    [Fact]
    public async Task Sod_creator_cannot_close_the_work_order()
    {
        var creator = DisciplineHead;                                                    // Create + FinalApprove
        var id = await CreateAsync("Preventive", creator);
        await Do(id, "Approve", creator);
        Assert.True((await Svc.SetPlanningAsync(id, Mech, Planner)).Success); await Do(id, "Approve", Planner);
        await PrepareAndIssueAsync(id);
        await Do(id, "Approve", Executor, null, Data(null)); await Do(id, "Approve", Executor);
        await Do(id, "Approve", Verifier);
        Assert.Matches("ایجادکننده|creator", await Fail(id, "Approve", creator));
        await Do(id, "Approve", Approver);
    }

    [Fact]
    public async Task Manager_override_cancels_but_cannot_final_approve_a_work_order_they_executed_or_verified()
    {
        var mgrExec = WithRoles(Manager, "MNT.Expert");
        var id = await CreateAsync("Preventive");
        await PlanAsync(id);
        Assert.True((await Svc.AssignExecutorAsync(id, null, Executor.EmployeeId, DisciplineHead)).Success);        // اجرای واقعی توسط Executor
        await Do(id, "Approve", Planner); await Do(id, "Approve", Ops);
        // Manager در Maintenance/Electrical است؛ Execute نیازمند دیسیپلین مسئول (Mechanical) است
        var mgrMech = new WorkOrderActor(Manager.EmployeeId, mgrExec.Roles);
        var mgrEmp = await Ctx.Employees.FirstAsync(e => e.EmployeeId == Manager.EmployeeId); mgrEmp.DisciplineId = Mech; await Ctx.SaveChangesAsync();
        await Do(id, "Approve", mgrMech);                                                                            // Start Work (F2)
        await Do(id, "Approve", mgrMech, null, Data(null)); await Do(id, "Approve", mgrMech);                       // Manager اجرا کرد
        await Do(id, "Approve", Verifier);
        Assert.Matches("مجری|executor", await Fail(id, "Approve", mgrMech));                                        // نمی‌تواند تصویب نهایی کند
        await Do(id, "Approve", Approver);

        // Manager بدون اجرا/تأیید: تصویب نهایی مجاز (cross-discipline برای مبدأ Maintenance) و Cancel در هر مرحله‌ی مجاز
        var id2 = await CreateAsync("Preventive");
        await RunToVerificationAsync(id2, "Preventive"); await Do(id2, "Approve", Verifier);
        await Do(id2, "Approve", WithRoles(Manager));                                                                // مجاز
        Assert.Equal("Closed", (await Wo(id2)).Status);
        var id3 = await CreateAsync(); await Do(id3, "Cancel", Manager);
        Assert.Equal("Cancelled", (await Wo(id3)).Status);
    }

    [Fact]
    public async Task Emergency_work_orders_are_not_an_authorization_or_sod_bypass()
    {
        var id = await CreateAsync("Emergency");
        await Do(id, "Approve", Creator);
        // بدون برنامه‌ریزی/عملیات نمی‌شود اجرا کرد
        Assert.Matches("دیسیپلین مسئول|responsible", await Fail(id, "Approve", Planner));
        Assert.True((await Svc.SetPlanningAsync(id, Mech, Planner)).Success);
        await Do(id, "Approve", Planner);
        Assert.True((await Svc.AssignExecutorAsync(id, null, Executor.EmployeeId, DisciplineHead)).Success);
        await Do(id, "Approve", Planner);
        await Fail(id, "Approve", Executor, Data());                                                                  // هنوز صادر نشده (Operations)
        await Do(id, "Approve", Ops);
        // طبق F2: بلافاصله بعد از Issue/Release هنوز InProgress نیست - Execute (تکمیل) هنوز در
        // دسترس نیست؛ فقط Start Work (F2) در دسترس است.
        Assert.Equal("Assigned", (await Wo(id)).Status);
        await Do(id, "Approve", Executor);                                                                            // Start Work
        Assert.Equal("InProgress", (await Wo(id)).Status);
        await Fail(id, "Approve", Executor, new WorkOrderCompletionData());                                           // ISO 14224 برای Emergency الزامی
        await Do(id, "Approve", Executor, null, Data());
    }

    [Fact]
    public async Task Forged_or_missing_identity_and_unknown_actions_are_rejected_server_side()
    {
        var id = await CreateAsync();
        await Fail(id, "Approve", new WorkOrderActor(null, new[] { "MNT.PlanningExpert" }));                           // بدون کارمند
        await Fail(id, "Approve", new WorkOrderActor(999999, new[] { "MNT.PlanningExpert" }));                         // کارمند ناموجود
        await Fail(id, "Approve", new WorkOrderActor(Creator.EmployeeId, new[] { "SomethingForged" }));                // نقش ساختگی
        await Fail(id, "DropDatabase", Creator);
        await Fail(id, "Complete", Creator);
        Assert.Equal("Open", (await Wo(id)).Status);
        // مسیر Legacy نمی‌تواند Work Order حاکمیتی را دور بزند
        var (ok, err) = await Svc.AdvanceStatusAsync(id, "Assigned", null, null, null, null, null, null, null, null);
        Assert.False(ok); Assert.Matches("گردش‌کار مرکزی|central workflow", err);
        Assert.Equal("Open", (await Wo(id)).Status);
    }
}

public class WorkOrderReviewAndLegacyTests : WorkOrderGovernanceTestBase
{
    private WorkOrderReviewService Reviews()
    {
        var hook = new WorkOrderAuthorizationHook(Ctx, new AccessControlService(Ctx));
        var runtime = new WorkflowRuntimeService(Ctx, new WorkflowBindingService(Ctx), null, hook, null, WorkOrderWorkflow.CreateRegistry());
        return new WorkOrderReviewService(Ctx, runtime);
    }

    [Fact]
    public async Task Lifecycle_and_review_instances_have_distinct_identities_and_do_not_interfere()
    {
        var id = await CreateAsync();
        await Reviews().RequestDisciplineReviewAsync(id, new[] { Elec, Instr }, Planner.EmployeeId);
        var instances = await Ctx.WorkflowInstances.Where(i => i.BusinessObjectId == id).ToListAsync();
        Assert.Equal(2, instances.Count);
        Assert.Contains(instances, i => i.EntityKey == WorkOrderWorkflow.LifecycleKey(id));
        Assert.Contains(instances, i => i.EntityKey == $"WorkOrder|{WorkOrderWorkflow.ReviewEntityType}|{id}");
        Assert.Equal(1, (await WorkOrderWorkflow.ReviewInstancesAsync(Ctx, id, true)).Count);
        Assert.DoesNotContain(await WorkOrderWorkflow.ReviewInstancesAsync(Ctx, id, false), i => i.EntityKey == WorkOrderWorkflow.LifecycleKey(id));
        var view = await Svc.GetWorkflowViewAsync(id, Creator);
        Assert.Equal("Created", view!.CurrentStageName);                                            // لایف‌سایکل دست‌نخورده
        Assert.Equal(2, (await Reviews().GetDisciplineReviewStatusAsync(id)).Count);                // وضعیت بازبینی از فرآیند خودش
        await Assert.ThrowsAnyAsync<Exception>(() => Reviews().RequestDisciplineReviewAsync(id, new[] { Civil }, Planner.EmployeeId));   // یک بازبینی فعال
    }

    [Fact]
    public async Task A_running_review_blocks_completion_and_cancelling_the_work_order_cancels_it()
    {
        var id = await CreateAsync("Preventive");
        await PlanAsync(id); await PrepareAndIssueAsync(id);
        await Reviews().RequestDisciplineReviewAsync(id, new[] { Elec }, Planner.EmployeeId);
        Assert.Matches("بازبینی|review", await Fail(id, "Approve", Executor, Data(null)));
        Assert.Equal("InProgress", (await Wo(id)).Status);

        await Do(id, "Cancel", Manager);
        var review = (await WorkOrderWorkflow.ReviewInstancesAsync(Ctx, id, false)).Single();
        Assert.Equal("Cancelled", review.Status);
        Assert.Contains(await Ctx.WorkflowHistories.Where(h => h.WorkflowInstanceId == review.WorkflowInstanceId).ToListAsync(), h => h.ActionType == "Cancel");
        Assert.False(await Reviews().HasPendingDisciplineReviewAsync(id));
    }

    [Fact]
    public async Task Finished_review_no_longer_blocks_completion()
    {
        var id = await CreateAsync("Preventive");
        await PlanAsync(id); await PrepareAndIssueAsync(id);
        var reviewer = Actor(DeptMaint, Elec, "MNT.Expert");
        await Reviews().RequestDisciplineReviewAsync(id, new[] { Elec }, Planner.EmployeeId);
        await Reviews().RespondAsync(id, "Approve", "ok", null, null, reviewer.EmployeeId!.Value, Elec);
        await Do(id, "Approve", Executor, null, Data(null));
        Assert.Equal("Completed", (await Wo(id)).Status);
    }

    private async Task<int> AddLegacyWoAsync(string status, DateTime created)
    {
        var w = new WorkOrder { EquipmentId = EquipmentId, WorkOrderNumber = "WO-LEG" + (++_num), WorkOrderType = "Preventive", Priority = "Medium", Status = status, IsActive = true, CreatedDate = created };
        Ctx.WorkOrders.Add(w); await Ctx.SaveChangesAsync();
        w.CreatedDate = created; await Ctx.SaveChangesAsync();          // SaveChanges تاریخ ایجاد را هنگام Insert می‌گذارد
        return w.WorkOrderId;
    }

    [Fact]
    public async Task Legacy_work_orders_stay_legacy_are_not_backfilled_and_keep_the_old_path()
    {
        var cutoff = await Ctx.WorkflowBindings.Where(b => b.EntityType == WorkOrderWorkflow.LifecycleEntityType).MinAsync(b => b.CreatedDate);
        var legacy = await AddLegacyWoAsync("Open", cutoff.AddDays(-30));
        var closed = await AddLegacyWoAsync("Closed", cutoff.AddDays(-60));
        // بازبینی Legacy در حال اجرا (بدون Binding، نوع قدیمی) روی رکورد InProgress
        var inProg = await AddLegacyWoAsync("InProgress", cutoff.AddDays(-10));
        var legacyReview = await StartLegacyReviewAsync(inProg);

        Assert.Null(await Svc.GetWorkflowViewAsync(legacy, Creator));
        Assert.False(await Svc.IsGovernedAsync(legacy));
        Assert.Equal(0, await Ctx.WorkflowInstances.CountAsync(i => i.EntityKey == WorkOrderWorkflow.LifecycleKey(legacy)));
        var (ok, err) = await Svc.AdvanceStatusAsync(legacy, "Assigned", null, null, null, null, null, null, null, null);
        Assert.True(ok, err);                                                                                    // مسیر قدیمی برای Legacy کار می‌کند
        Assert.Equal("Assigned", (await Wo(legacy)).Status);

        // فعال‌سازی/حاکمیت‌بخشی رکوردهای جدید، Legacy را لمس نمی‌کند
        var fresh = await CreateAsync();
        var (gok, gerr, count) = await Svc.GovernPendingNewAsync(Creator);
        Assert.True(gok, gerr); Assert.Equal(0, count);
        Assert.Equal(0, await Ctx.WorkflowInstances.CountAsync(i => i.EntityKey == WorkOrderWorkflow.LifecycleKey(closed)));
        Assert.Equal("Closed", (await Wo(closed)).Status);
        Assert.Null((await Wo(inProg)).OriginatingOrganization);

        // بازبینی Legacy دست‌نخورده، با هویت جدا از چرخه‌عمر جدید
        var still = (await WorkOrderWorkflow.ReviewInstancesAsync(Ctx, inProg, true)).Single();
        Assert.Equal(legacyReview, still.WorkflowInstanceId);
        Assert.Equal("Running", still.Status);
        Assert.True(await Reviews().HasPendingDisciplineReviewAsync(inProg));
        Assert.NotNull(await Svc.GetWorkflowViewAsync(fresh, Creator));
    }

    private async Task<int> StartLegacyReviewAsync(int woId)
    {
        var def = await Ctx.WorkflowDefinitions.SingleAsync(d => d.Category == WorkOrderWorkflow.ReviewContext);
        var ver = await Ctx.WorkflowVersions.SingleAsync(v => v.WorkflowDefinitionId == def.WorkflowDefinitionId);
        var stage = await Ctx.WorkflowStages.SingleAsync(s => s.WorkflowVersionId == ver.WorkflowVersionId);
        var inst = new WorkflowInstance { WorkflowVersionId = ver.WorkflowVersionId, Module = "WorkOrder", BusinessObjectType = "WorkOrder", BusinessObjectId = woId, Status = "Running", CurrentStageId = stage.WorkflowStageId };
        Ctx.WorkflowInstances.Add(inst); await Ctx.SaveChangesAsync();
        Ctx.WorkflowInstanceStages.Add(new WorkflowInstanceStage { WorkflowInstanceId = inst.WorkflowInstanceId, WorkflowStageId = stage.WorkflowStageId, AssignedToRole = "Discipline:" + Elec, Status = "Pending" });
        await Ctx.SaveChangesAsync();
        return inst.WorkflowInstanceId;
    }

    [Fact]
    public async Task Work_orders_created_after_activation_without_an_instance_cannot_advance_through_the_legacy_path_until_governed()
    {
        var w = new WorkOrder { EquipmentId = EquipmentId, WorkOrderNumber = "WO-PM1", WorkOrderType = "Preventive", Priority = "Medium", Status = "Open", IsActive = true, CreatedDate = DateTime.Now.AddMinutes(1) };
        Ctx.WorkOrders.Add(w); await Ctx.SaveChangesAsync();
        var (ok, err) = await Svc.AdvanceStatusAsync(w.WorkOrderId, "Assigned", null, null, null, null, null, null, null, null);
        Assert.False(ok); Assert.Matches("گردش‌کار مرکزی|central workflow", err);

        // مسیر تولید (PM/کالیبراسیون): حاکمیتی می‌شود؛ مبدأ = اجراکننده
        var (gok, gerr, count) = await Svc.GovernPendingNewAsync(Creator);
        Assert.True(gok, gerr); Assert.Equal(1, count);
        var wo = await Wo(w.WorkOrderId);
        Assert.Equal(OrganizationDomains.Maintenance, wo.OriginatingOrganization);
        Assert.True(await Svc.IsGovernedAsync(w.WorkOrderId));
        Assert.Equal(0, (await Svc.GovernPendingNewAsync(Creator)).Count);                                       // Idempotent
    }
}

public class WorkOrderConversionAndDynamicProofTests : WorkOrderGovernanceTestBase
{
    private sealed class FakeNumbering : PEMS.Application.Interfaces.INumberingService
    {
        private int _n;
        public Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null) => Task.FromResult($"{documentType}-N{++_n:D4}");
    }

    [Fact]
    public async Task Work_request_conversion_preserves_the_originating_organization_and_discipline_and_starts_the_lifecycle()
    {
        // درخواست‌دهنده: Engineering / Mechanical؛ مسئول نگهداری بعداً (Planning) Maintenance / Electrical تعیین می‌شود
        var requester = Actor(DeptEng, Mech, SystemRoles.DisciplineEngineer);
        var wr = new WorkRequest { RequestNumber = "WR-1", Title = "leak", RequestedById = requester.EmployeeId!.Value, EquipmentId = EquipmentId, Priority = "High", Status = "Planned", IsActive = true };
        Ctx.WorkRequests.Add(wr); await Ctx.SaveChangesAsync();

        var wrSvc = new WorkRequestService(Ctx, new FakeNumbering(), null!);
        var (ok, err, woId) = await wrSvc.ConvertToWorkOrderAsync(wr.WorkRequestId, "Corrective", "tester");
        Assert.True(ok, err);

        var wo = await Wo(woId!.Value);
        Assert.Equal(wr.WorkRequestId, wo.SourceWorkRequestId);
        Assert.Equal(OrganizationDomains.Engineering, wo.OriginatingOrganization);
        Assert.Equal(Mech, wo.OriginatingDisciplineId);
        Assert.Equal(requester.EmployeeId, wo.OriginatingRequestedById);
        Assert.Null(wo.ResponsibleDisciplineId);                                     // مسئول نگهداری ≠ مبدأ؛ جدا و بعداً توسط Planning
        Assert.True(await Svc.IsGovernedAsync(woId.Value));
        Assert.Equal("Converted", (await Ctx.WorkRequests.AsNoTracking().FirstAsync(w => w.WorkRequestId == wr.WorkRequestId)).Status);

        await Do(woId.Value, "Approve", Creator);
        Assert.True((await Svc.SetPlanningAsync(woId.Value, Elec, Planner)).Success);
        var w2 = await Wo(woId.Value);
        Assert.Equal(OrganizationDomains.Engineering, w2.OriginatingOrganization); Assert.Equal(Mech, w2.OriginatingDisciplineId);
        Assert.Equal(Elec, w2.ResponsibleDisciplineId); Assert.Equal(OrganizationDomains.Maintenance, w2.ResponsibleOrganization);
    }

    [Fact]
    public async Task Conversion_without_a_requester_organization_fails_closed_and_creates_nothing()
    {
        var orphanDept = new Department { Name = "NoDomain", Code = "ND" }; Ctx.Departments.Add(orphanDept); await Ctx.SaveChangesAsync();
        var requester = Actor(orphanDept.DepartmentId, Mech);
        var wr = new WorkRequest { RequestNumber = "WR-2", Title = "x", RequestedById = requester.EmployeeId!.Value, EquipmentId = EquipmentId, Priority = "Low", Status = "Planned", IsActive = true };
        Ctx.WorkRequests.Add(wr); await Ctx.SaveChangesAsync();
        var (ok, err, _) = await new WorkRequestService(Ctx, new FakeNumbering(), null!).ConvertToWorkOrderAsync(wr.WorkRequestId, "Corrective", "t");
        Assert.False(ok);
        Assert.Equal(0, await Ctx.WorkOrders.CountAsync());
        Assert.Equal("Planned", (await Ctx.WorkRequests.AsNoTracking().FirstAsync()).Status);
    }

    [Fact]
    public async Task Reliability_Review_added_by_configuration_appears_for_new_work_orders_only_without_code_changes()
    {
        var old = await CreateAsync();
        await Do(old, "Approve", Creator);                                                                         // نسخه‌ی جاری (F2: v2 که Released/Execution Authorized دارد): Planning
        // طبق F2: پایه‌ی تست از قبل v2 را Seed/Pin کرده - "قدیمی‌ترین نسخه‌ی این تست" همان نسخه‌ی
        // در حال حاضر Pin‌شده است، نه لزوماً v1 خام.
        var v1 = await Ctx.WorkflowVersions.Include(v => v.Definition).Where(v => v.Definition!.Name == WorkOrderWorkflow.LifecycleDefinitionName)
            .OrderByDescending(v => v.VersionNumber).FirstAsync();
        var oldVersionNumber = v1.VersionNumber;

        var defs = new WorkflowDefinitionService(Ctx, new WorkflowVersionValidator(Ctx, WorkOrderWorkflow.CreateRegistry(), new WorkflowConditionEvaluator(), new PemsWorkflowReferenceCatalog(Ctx)));
        var admin = new WorkflowActor(99, "admin");
        var v2 = await defs.CloneVersionAsync(v1.WorkflowVersionId, admin, "Reliability Review after Planning");
        var planning = await Ctx.WorkflowStages.SingleAsync(s => s.WorkflowVersionId == v2.WorkflowVersionId && s.Name == "Maintenance Planning");
        var prep = await Ctx.WorkflowStages.SingleAsync(s => s.WorkflowVersionId == v2.WorkflowVersionId && s.Name == "Preparation / Assignment");
        var rr = await defs.AddStageAsync(v2.WorkflowVersionId, new WorkflowStageInput
        { Name = "Reliability Review", NameFa = "بازبینی قابلیت اطمینان", StageOrder = 25, ResponsibleRole = "Permission:WorkOrder.Plan", BusinessStatus = "Open" }, admin);
        var edge = await Ctx.WorkflowTransitions.SingleAsync(t => t.WorkflowVersionId == v2.WorkflowVersionId && t.FromStageId == planning.WorkflowStageId && t.ActionType == "Approve");
        await defs.RemoveTransitionAsync(edge.WorkflowTransitionId, admin);
        await defs.AddTransitionAsync(v2.WorkflowVersionId, new WorkflowTransitionInput { FromStageId = planning.WorkflowStageId, ToStageId = rr.WorkflowStageId, ActionType = "Approve", Priority = 1, RequiredPermission = "WorkOrder.Plan", Label = "Send to Reliability", LabelFa = "ارسال برای قابلیت اطمینان" }, admin);
        await defs.AddTransitionAsync(v2.WorkflowVersionId, new WorkflowTransitionInput { FromStageId = rr.WorkflowStageId, ToStageId = prep.WorkflowStageId, ActionType = "Approve", Priority = 1, RequiredPermission = "WorkOrder.Plan", Label = "Approve Reliability", LabelFa = "تأیید قابلیت اطمینان" }, admin);
        await defs.AddTransitionAsync(v2.WorkflowVersionId, new WorkflowTransitionInput { FromStageId = rr.WorkflowStageId, ToStageId = null, ActionType = "Reject", Priority = 1, RequiredPermission = "WorkOrder.Reject" }, admin);
        await defs.AddTransitionAsync(v2.WorkflowVersionId, new WorkflowTransitionInput { FromStageId = rr.WorkflowStageId, ToStageId = null, ActionType = "Cancel", Priority = 1, RequiredPermission = "WorkOrder.CancelPreIssue" }, admin);
        var vr = await defs.ValidateVersionAsync(v2.WorkflowVersionId, admin);
        Assert.True(vr.IsValid, string.Join(",", vr.Errors.Select(e => e.Code + e.Message)));
        await defs.PublishVersionAsync(v2.WorkflowVersionId, admin);
        var b = await Ctx.WorkflowBindings.SingleAsync(x => x.EntityType == WorkOrderWorkflow.LifecycleEntityType);
        await new WorkflowBindingService(Ctx).ChangeAsync(b.WorkflowBindingId, v2.WorkflowVersionId, null, null, admin);

        var fresh = await CreateAsync();
        await Do(fresh, "Approve", Creator);
        var viewNew = (await Svc.GetWorkflowViewAsync(fresh, Planner))!;
        var viewOld = (await Svc.GetWorkflowViewAsync(old, Planner))!;
        Assert.Contains("Reliability Review", viewNew.Stages.Select(s => s.Name));
        Assert.DoesNotContain("Reliability Review", viewOld.Stages.Select(s => s.Name));
        Assert.Equal(oldVersionNumber, viewOld.VersionNumber); Assert.Equal(oldVersionNumber + 1, viewNew.VersionNumber);
        Assert.Equal("Reliability Review", viewNew.AvailableActions.Single(a => a.ActionType == "Approve").ToStageName);
        Assert.True((await Svc.SetPlanningAsync(fresh, Mech, Planner)).Success);
        await Do(fresh, "Approve", Planner);
        Assert.Equal("Reliability Review", (await Svc.GetWorkflowViewAsync(fresh, Planner))!.CurrentStageName);
        // نمونه‌ی قدیمی همچنان روی v1 پیش می‌رود (بدون Reliability Review)
        Assert.True((await Svc.SetPlanningAsync(old, Mech, Planner)).Success);
        await Do(old, "Approve", Planner);
        Assert.Equal("Preparation / Assignment", (await Svc.GetWorkflowViewAsync(old, Planner))!.CurrentStageName);
    }

    [Fact]
    public void Work_order_ui_and_controller_contain_no_hard_coded_new_workflow_stage_names()
    {
        string? root = null;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "PEMS.Web", "Views", "WorkOrder", "Details.cshtml"))) { root = d.FullName; break; }
        Assert.NotNull(root);
        var view = File.ReadAllText(Path.Combine(root!, "PEMS.Web", "Views", "WorkOrder", "Details.cshtml"));
        var ctrl = File.ReadAllText(Path.Combine(root!, "PEMS.Web", "Controllers", "WorkOrderController.cs"));
        var svc = File.ReadAllText(Path.Combine(root!, "PEMS.Persistence", "Servises", "WorkOrderLifecycleService.cs"));
        foreach (var name in new[] { "Reliability Review", "Maintenance Planning", "Operations Issue", "Originating-Discipline Verification", "Preparation / Assignment", "Final Approval" })
        {
            Assert.DoesNotContain(name, view);
            Assert.DoesNotContain("\"" + name + "\"", ctrl);
            Assert.DoesNotContain("\"" + name + "\"", svc);
        }
        Assert.Contains("wf.Stages", view);
        Assert.Contains("actions", view);
    }
}

internal static class WorkOrderTestExtensions
{
    /// <summary>InMemory سازگار: Binding چرخه‌عمر را غیرفعال می‌کند (ExecuteUpdate در InMemory پشتیبانی نمی‌شود).</summary>
    public static async Task ExecuteUpdateAsyncCompat(this IQueryable<WorkflowBinding> q, PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        foreach (var b in await q.ToListAsync()) b.IsActive = false;
        await ctx.SaveChangesAsync();
    }
}
