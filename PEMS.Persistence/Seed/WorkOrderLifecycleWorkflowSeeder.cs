using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Persistence.Workflow;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;
using PEMS.WorkflowPlatform.Services;
using static PEMS.Persistence.Workflow.WorkOrderWorkflow;

namespace PEMS.Persistence.Seed;

/// <summary>
/// Phase 3B - تعریف گردش‌کار چرخه‌عمر Work Order (طبق Phase 3A §19) + Binding صریح و پین‌شده؛ و Binding صریح برای بازبینی
/// چند-دیسیپلینه‌ی موجود (Architecture A: هویت جدا). از چرخه‌ی Phase 1 (Draft → Validate → Publish + ممیزی پیکربندی) عبور می‌کند.
/// Idempotent؛ Work Order های Legacy و Instance های موجود هرگز لمس نمی‌شوند.
/// </summary>
public static class WorkOrderLifecycleWorkflowSeeder
{
    private static string P(params string[] perms) => string.Join(",", perms.Select(p => ParticipantPrefix + p));

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();
        await WorkOrderGovernanceSeeder.EnsurePermissionsAsync(db);

        var registry = CreateRegistry();
        var validator = new WorkflowVersionValidator(db, registry, new WorkflowConditionEvaluator(), new PemsWorkflowReferenceCatalog(db));
        var defs = new WorkflowDefinitionService(db, validator);
        var bindings = new WorkflowBindingService(db);
        var actor = new WorkflowActor(null, "system-seed");

        var def = await db.WorkflowDefinitions.FirstOrDefaultAsync(d => d.Module == Module && d.Category == LifecycleContext && d.Name == LifecycleDefinitionName);
        if (def == null)
        {
            def = await defs.CreateDefinitionAsync(LifecycleDefinitionName, LifecycleContext, Module,
                "Work Order lifecycle: Created → Maintenance Planning → Preparation/Assignment → Operations Issue/Release → Execution → Completion → Originating-Discipline Verification → Final Approval → Closed.", actor);
            var v = await defs.CreateDraftVersionAsync(def.WorkflowDefinitionId, actor, "Initial version - Phase 3A governance baseline");
            await BuildStructureAsync(defs, v.WorkflowVersionId, actor);
            var r = await defs.ValidateVersionAsync(v.WorkflowVersionId, actor);
            if (!r.IsValid)
                throw new InvalidOperationException("Work Order lifecycle workflow validation failed: " + string.Join("; ", r.Errors.Select(e => $"{e.Code}: {e.Message}")));
            await defs.PublishVersionAsync(v.WorkflowVersionId, actor);
        }

        if (!await db.WorkflowBindings.AnyAsync(b => b.Module == Module && b.EntityType == LifecycleEntityType && b.Context == LifecycleContext))
        {
            var published = await db.WorkflowVersions.Where(x => x.WorkflowDefinitionId == def.WorkflowDefinitionId && x.Status == "Published")
                .OrderByDescending(x => x.VersionNumber).FirstAsync();
            await bindings.CreateAsync(Module, LifecycleEntityType, LifecycleContext, def.WorkflowDefinitionId, published.WorkflowVersionId, null, null, actor);
        }

        // بازبینی چند-دیسیپلینه: Binding صریح روی تعریف موجود (نسخه‌ی پین‌شده). Instance های قدیمی بدون Binding دست‌نخورده می‌مانند.
        var review = await db.WorkflowDefinitions.FirstOrDefaultAsync(d => d.Module == Module && d.Category == ReviewContext && d.IsActive);
        if (review != null && !await db.WorkflowBindings.AnyAsync(b => b.Module == Module && b.EntityType == ReviewEntityType && b.Context == ReviewContext))
        {
            var rv = await db.WorkflowVersions.Where(x => x.WorkflowDefinitionId == review.WorkflowDefinitionId && x.Status == "Published")
                .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync();
            if (rv != null)
                await bindings.CreateAsync(Module, ReviewEntityType, ReviewContext, review.WorkflowDefinitionId, rv.WorkflowVersionId, null, null, actor);
        }
    }

    /// <summary>ساختار نسخه‌ی 1 (همچنین برای تست‌ها). هر گذار دقیقاً یک «مجوز وظیفه‌ای» دارد؛ سازمان/دیسیپلین/SoD در هوک اعمال می‌شود.</summary>
    public static async Task BuildStructureAsync(IWorkflowDefinitionService defs, int versionId, WorkflowActor actor)
    {
        WorkflowStageInput S(string name, string fa, int order, string status, params string[] perms) => new()
        {
            Name = name, NameFa = fa, StageOrder = order, StageType = "Sequential", ResponsibleRole = P(perms), BusinessStatus = status,
            ApprovalStrategy = "AllApprovalsRequired"
        };
        WorkflowTransitionInput T(int? from, int? to, string action, string? perm, string? label, string? labelFa) => new()
        {
            FromStageId = from, ToStageId = to, ActionType = action, Priority = 1, RequiredPermission = perm, Label = label, LabelFa = labelFa
        };

        var created = await defs.AddStageAsync(versionId, S("Created", "ایجاد‌شده", 10, "Open", Perm.Create, Perm.CancelPreIssue), actor);
        var planning = await defs.AddStageAsync(versionId, S("Maintenance Planning", "برنامه‌ریزی نگهداری", 20, "Open", Perm.Plan, Perm.Reject, Perm.CancelPreIssue), actor);
        var prep = await defs.AddStageAsync(versionId, S("Preparation / Assignment", "آماده‌سازی / تخصیص", 30, "Assigned", Perm.Prepare, Perm.Reject, Perm.CancelPreIssue), actor);
        var issue = await defs.AddStageAsync(versionId, S("Operations Issue / Release", "صدور / آزادسازی توسط عملیات", 40, "Assigned", Perm.Issue, Perm.CancelPreIssue), actor);
        var exec = await defs.AddStageAsync(versionId, S("In Execution", "در حال اجرا", 50, "InProgress", Perm.Execute, Perm.Hold, Perm.CancelExecution), actor);
        var hold = await defs.AddStageAsync(versionId, S("On Hold", "متوقف", 55, "OnHold", Perm.Hold, Perm.CancelExecution), actor);
        var done = await defs.AddStageAsync(versionId, S("Completion", "تکمیل", 60, "Completed", Perm.Execute), actor);
        var verify = await defs.AddStageAsync(versionId, S("Originating-Discipline Verification", "تأیید دیسیپلین مبدأ", 70, "Completed", Perm.Verify), actor);
        var final = await defs.AddStageAsync(versionId, S("Final Approval", "تصویب نهایی", 80, "Completed", Perm.FinalApprove), actor);

        async Task Add(WorkflowTransitionInput t) => await defs.AddTransitionAsync(versionId, t, actor);
        await Add(T(null, created.WorkflowStageId, "Start", null, null, null));

        await Add(T(created.WorkflowStageId, planning.WorkflowStageId, "Approve", Perm.Create, "Submit to Planning", "ارسال به برنامه‌ریزی"));
        await Add(T(created.WorkflowStageId, null, "Cancel", Perm.CancelPreIssue, "Cancel", "لغو"));

        await Add(T(planning.WorkflowStageId, prep.WorkflowStageId, "Approve", Perm.Plan, "Plan Complete", "تکمیل برنامه‌ریزی"));
        await Add(T(planning.WorkflowStageId, null, "Reject", Perm.Reject, "Reject", "رد"));
        await Add(T(planning.WorkflowStageId, null, "Cancel", Perm.CancelPreIssue, "Cancel", "لغو"));

        await Add(T(prep.WorkflowStageId, issue.WorkflowStageId, "Approve", Perm.Prepare, "Request Issue", "درخواست صدور"));
        await Add(T(prep.WorkflowStageId, planning.WorkflowStageId, "Return", Perm.Prepare, "Return to Planning", "بازگشت به برنامه‌ریزی"));
        await Add(T(prep.WorkflowStageId, null, "Reject", Perm.Reject, "Reject", "رد"));
        await Add(T(prep.WorkflowStageId, null, "Cancel", Perm.CancelPreIssue, "Cancel", "لغو"));

        await Add(T(issue.WorkflowStageId, exec.WorkflowStageId, "Approve", Perm.Issue, "Issue / Release", "صدور / آزادسازی"));
        await Add(T(issue.WorkflowStageId, planning.WorkflowStageId, "Return", Perm.Issue, "Decline - Return to Planning", "عدم پذیرش - بازگشت به برنامه‌ریزی"));
        await Add(T(issue.WorkflowStageId, null, "Reject", Perm.Issue, "Decline - Reject", "عدم پذیرش - رد"));
        await Add(T(issue.WorkflowStageId, null, "Cancel", Perm.CancelPreIssue, "Cancel", "لغو"));

        await Add(T(exec.WorkflowStageId, done.WorkflowStageId, "Approve", Perm.Execute, "Record Completion", "ثبت تکمیل"));
        await Add(T(exec.WorkflowStageId, hold.WorkflowStageId, "Hold", Perm.Hold, "Put on Hold", "توقف"));
        await Add(T(exec.WorkflowStageId, null, "Cancel", Perm.CancelExecution, "Cancel", "لغو"));

        await Add(T(hold.WorkflowStageId, exec.WorkflowStageId, "Approve", Perm.Hold, "Resume", "ازسرگیری"));
        await Add(T(hold.WorkflowStageId, null, "Cancel", Perm.CancelExecution, "Cancel", "لغو"));

        await Add(T(done.WorkflowStageId, verify.WorkflowStageId, "Approve", Perm.Execute, "Send to Verification", "ارسال برای تأیید"));
        await Add(T(done.WorkflowStageId, exec.WorkflowStageId, "Return", Perm.Execute, "Return to Execution", "بازگشت به اجرا"));

        await Add(T(verify.WorkflowStageId, final.WorkflowStageId, "Approve", Perm.Verify, "Verify Completion", "تأیید تکمیل"));
        await Add(T(verify.WorkflowStageId, exec.WorkflowStageId, "Return", Perm.Verify, "Return for Rework", "بازگشت برای اصلاح"));

        await Add(T(final.WorkflowStageId, null, "Approve", Perm.FinalApprove, "Final Approval - Close", "تصویب نهایی و بستن"));
        await Add(T(final.WorkflowStageId, verify.WorkflowStageId, "Return", Perm.FinalApprove, "Return to Verification", "بازگشت به تأیید"));
    }
}
