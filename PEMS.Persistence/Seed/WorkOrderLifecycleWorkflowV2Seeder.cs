using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Workflow;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;
using PEMS.WorkflowPlatform.Services;
using static PEMS.Persistence.Workflow.WorkOrderWorkflow;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS — Work Order Governance Remediation» F2: نسخه‌ی ۱ گردش‌کار Work Order، Issue/Release
/// (اختیار Operations) و شروع واقعی اجرا (StartDate) را در یک گذار واحد ادغام کرده بود. این Seeder
/// یک نسخه‌ی تازه (Clone از آخرین نسخه‌ی Published، طبق دستور صریح "prefer the smallest
/// architecture-consistent change") می‌سازد که یک مرحله‌ی میانی تازه اضافه می‌کند:
///
///   Operations Issue/Release --Approve[Issue]--> Released/Execution Authorized --Approve[Start]--> In Execution
///
/// مرحله‌ی تازه عمداً BusinessStatus="Assigned" دارد (دقیقاً هم‌ان مقدار مرحله‌ی قبلی‌اش) - یعنی
/// WorkOrder.Status هیچ مقدار تازه‌ای نمی‌گیرد و هیچ کد دیگری (شمارنده‌های Backlog/Overdue/تقویم/رنگ
/// Badge که همه روی رشته‌های دقیق Status موجود کار می‌کنند) نیازی به تغییر ندارد؛ تمایز «آزاد شده ولی
/// شروع‌نشده» کاملاً در سطح خودِ WorkflowPlatform (Stage جاری Instance) نگه‌داری می‌شود.
///
/// Instance های در حال اجرا (مثل WO-2026-0009) روی WorkflowVersionId خودشان (نسخه‌ی ۱) باقی می‌مانند -
/// طبق طراحی خودِ پلتفرم (WorkflowInstance.WorkflowVersionId در لحظه‌ی شروع ثابت می‌شود)، هیچ Instance
/// موجودی لمس/مهاجرت داده نمی‌شود؛ فقط Work Order های تازه از این پس نسخه‌ی ۲ را می‌بینند.
/// </summary>
public static class WorkOrderLifecycleWorkflowV2Seeder
{
    public const string ReleasedStageName = "Released / Execution Authorized";
    public const string ChangeSummary = "F2: separate Operations Issue/Release from Maintenance Start Work; StartDate now set by Start Work, not Issue.";

    // طبق F2 بند «Start Work must be authorized for the correct Maintenance execution actor»: همان
    // نقش‌هایی که از قبل Perm.Execute دارند (WorkOrderGovernanceSeeder.Matrix) - نه فهرست نقش تازه.
    private static readonly string[] StartWorkRoles = { "MNT.DisciplineHead", "MNT.SeniorExpert", "MNT.Expert" };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        // Idempotent: اگر مرحله‌ی تازه در هر نسخه‌ای از این تعریف از قبل وجود دارد، دیگر کاری نیست.
        var alreadySeeded = await (
            from s in db.WorkflowStages
            join v in db.WorkflowVersions on s.WorkflowVersionId equals v.WorkflowVersionId
            join d in db.WorkflowDefinitions on v.WorkflowDefinitionId equals d.WorkflowDefinitionId
            where d.Module == Module && d.Category == LifecycleContext && d.Name == LifecycleDefinitionName && s.Name == ReleasedStageName
            select s.WorkflowStageId
        ).AnyAsync();
        if (alreadySeeded) return;

        await WorkOrderGovernanceSeeder.EnsurePermissionsAsync(db);
        await GrantStartPermissionAsync(db);

        var registry = CreateRegistry();
        var validator = new WorkflowVersionValidator(db, registry, new WorkflowConditionEvaluator(), new PemsWorkflowReferenceCatalog(db));
        var defs = new WorkflowDefinitionService(db, validator);
        var bindings = new WorkflowBindingService(db);
        var actor = new WorkflowActor(null, "system-seed");

        var def = await db.WorkflowDefinitions.FirstOrDefaultAsync(d => d.Module == Module && d.Category == LifecycleContext && d.Name == LifecycleDefinitionName);
        if (def == null) return; // v1 seeder هنوز اجرا نشده - این Seeder به ترتیب بعد از آن فراخوانی می‌شود

        var v1 = await db.WorkflowVersions.Where(v => v.WorkflowDefinitionId == def.WorkflowDefinitionId && v.Status == "Published")
            .OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync();
        if (v1 == null) return;

        var v2 = await defs.CloneVersionAsync(v1.WorkflowVersionId, actor, ChangeSummary);

        var issueStage = await db.WorkflowStages.FirstAsync(s => s.WorkflowVersionId == v2.WorkflowVersionId && s.Name == "Operations Issue / Release");
        var execStage = await db.WorkflowStages.FirstAsync(s => s.WorkflowVersionId == v2.WorkflowVersionId && s.Name == "In Execution");

        // مرحله‌ی تازه: هم‌ان BusinessStatus مرحله‌ی قبلی‌اش ("Assigned") - عمداً هیچ رشته‌ی Status تازه‌ای معرفی نمی‌شود.
        var released = await defs.AddStageAsync(v2.WorkflowVersionId, new WorkflowStageInput
        {
            Name = ReleasedStageName, NameFa = "آزادسازی / مجاز به اجرا", StageOrder = 45, StageType = "Sequential",
            ResponsibleRole = $"{ParticipantPrefix}{Perm.Start},{ParticipantPrefix}{Perm.CancelExecution}",
            BusinessStatus = "Assigned", ApprovalStrategy = "AllApprovalsRequired"
        }, actor);

        // گذار قبلی issue→exec (Approve/Issue، برچسب "Issue / Release") را به‌جای In Execution، به مرحله‌ی
        // تازه هدایت می‌کنیم - خودِ گذار (مجوز/برچسب/نوع اکشن) دست‌نخورده می‌ماند، فقط مقصدش عوض می‌شود.
        var issueTransition = await db.WorkflowTransitions.FirstAsync(t =>
            t.WorkflowVersionId == v2.WorkflowVersionId && t.FromStageId == issueStage.WorkflowStageId &&
            t.ToStageId == execStage.WorkflowStageId && t.ActionType == "Approve" && t.RequiredPermission == Perm.Issue);
        await defs.UpdateTransitionAsync(issueTransition.WorkflowTransitionId, new WorkflowTransitionInput
        {
            FromStageId = issueStage.WorkflowStageId, ToStageId = released.WorkflowStageId, ActionType = "Approve",
            Priority = issueTransition.Priority, RequiredPermission = Perm.Issue, Label = issueTransition.Label, LabelFa = issueTransition.LabelFa
        }, actor);

        // گذار تازه: Start Work (Maintenance، Perm.Start) - جایی که StartDate واقعاً ست می‌شود (در WorkOrderLifecycleService).
        await defs.AddTransitionAsync(v2.WorkflowVersionId, new WorkflowTransitionInput
        {
            FromStageId = released.WorkflowStageId, ToStageId = execStage.WorkflowStageId, ActionType = "Approve",
            Priority = 1, RequiredPermission = Perm.Start, Label = "Start Work", LabelFa = "شروع کار"
        }, actor);

        // دریچه‌ی ایمنی: اگر بعد از آزادسازی هنوز شروع نشده، همچنان قابل لغو باشد (بدون این، مرحله‌ی
        // تازه یک بن‌بست بدون خروج می‌شد).
        await defs.AddTransitionAsync(v2.WorkflowVersionId, new WorkflowTransitionInput
        {
            FromStageId = released.WorkflowStageId, ToStageId = null, ActionType = "Cancel",
            Priority = 1, RequiredPermission = Perm.CancelExecution, Label = "Cancel", LabelFa = "لغو"
        }, actor);

        var result = await defs.ValidateVersionAsync(v2.WorkflowVersionId, actor);
        if (!result.IsValid)
            throw new InvalidOperationException("Work Order lifecycle v2 validation failed: " + string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
        await defs.PublishVersionAsync(v2.WorkflowVersionId, actor);

        // Work Order های تازه از این پس v2 را می‌گیرند؛ Instance های در حال اجرا (پین‌شده روی نسخه‌ی خودشان
        // در لحظه‌ی شروع) دست‌نخورده باقی می‌مانند - طبق طراحی خودِ پلتفرم، نه یک استثنای دستی این‌جا.
        var binding = await db.WorkflowBindings.FirstAsync(b => b.Module == Module && b.EntityType == LifecycleEntityType && b.Context == LifecycleContext);
        await bindings.ChangeAsync(binding.WorkflowBindingId, v2.WorkflowVersionId, binding.EffectiveFrom, binding.EffectiveTo, actor);
    }

    private static async Task GrantStartPermissionAsync(PEMSDbContext db)
    {
        var perm = await WorkOrderGovernanceSeeder.GetOrCreatePermissionAsync(db, Perm.Start);
        foreach (var role in StartWorkRoles)
        {
            var roleId = await db.Roles.Where(r => r.Name == role).Select(r => (int?)r.Id).FirstOrDefaultAsync();
            if (roleId == null) continue;
            var grant = await db.RolePermissionGrants.FirstOrDefaultAsync(g => g.RoleId == roleId && g.PermissionId == perm.PermissionId);
            if (grant == null) db.RolePermissionGrants.Add(new RolePermissionGrant { RoleId = roleId.Value, PermissionId = perm.PermissionId, IsActive = true, CreatedDate = DateTime.Now });
            else grant.IsActive = true;
        }
        await db.SaveChangesAsync();
    }
}
