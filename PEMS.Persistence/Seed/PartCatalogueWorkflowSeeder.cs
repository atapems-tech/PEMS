using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Persistence.Workflow;
using PEMS.WorkflowPlatform.Interfaces;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Persistence.Seed;

/// <summary>
/// ثبت گردش‌کار «تأیید درخواست کاتالوگ قطعه» در PEMS.WorkflowPlatform + پیکربندی ساختاری مسئولیت‌ها.
/// منبع تعریف، ردیف‌های فعلی <c>PartCatalogueWorkflowSteps</c> (ترتیب/فعال‌بودن/شرط‌های تنظیم‌شده توسط مدیر) است؛ شرط‌ها
/// ثابت‌نویسی نمی‌شوند. فقط ساختار Seed می‌شود: هیچ کارمند/Position/تخصیص واقعی ساخته نمی‌شود.
/// Idempotent: تعریف/Binding/مسئولیتِ موجود بازنویسی نمی‌شود؛ Instance های موجود لمس نمی‌شوند.
/// </summary>
public static class PartCatalogueWorkflowSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();
        await SeedAsync(db);
    }

    public static async Task SeedAsync(PEMSDbContext db)
    {
        // ---- مجوزها در کاتالوگ Permission (همان قالب سایر Seederها)
        foreach (var action in new[] { "Review", "Approve" })
        {
            var name = $"{PartCatalogueWorkflow.Module}.{action}";
            if (!await db.Permissions.AnyAsync(p => p.Name == name))
            {
                db.Permissions.Add(new Permission { Name = name, ResourceName = PartCatalogueWorkflow.Module, ActionName = action, IsDestructive = false, IsActive = true, CreatedDate = DateTime.Now });
                await db.SaveChangesAsync();
            }
        }

        var steps = await db.PartCatalogueWorkflowSteps.AsNoTracking().OrderBy(s => s.StepOrder).ToListAsync();
        if (steps.Count == 0) return;   // ابتدا PartCatalogueSeeder

        // ---- مسئولیت‌ها (فقط افزودن؛ ویرایش مدیر حفظ می‌شود)
        var existingKeys = (await db.WorkflowActorResponsibilities.Select(r => r.ResponsibilityKey).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var s in steps.Where(s => !existingKeys.Contains(s.StepKey)))
        {
            var kind = s.ApproverSource switch
            {
                PartCatalogueApproverSources.DirectManager or PartCatalogueApproverSources.SuperiorManager => ActorResponsibilityKinds.RequesterManager,
                // پیش‌فرض سازگار با داده‌ی موجود: فهرست صریح Legacy. مدیر می‌تواند به PositionScope/CommodityPosition تغییر دهد.
                _ => ActorResponsibilityKinds.AssignmentPool
            };
            db.WorkflowActorResponsibilities.Add(new WorkflowActorResponsibility
            {
                ResponsibilityKey = s.StepKey, DisplayName = s.NameEn, DisplayNameFa = s.NameFa, Kind = kind,
                ManagerLevel = s.ApproverSource == PartCatalogueApproverSources.DirectManager ? 1 : s.ApproverSource == PartCatalogueApproverSources.SuperiorManager ? 2 : null,
                RequiredModule = PartCatalogueWorkflow.Module, RequiredAction = PartCatalogueWorkflow.PermissionAction(s.Kind),
                ScopeType = ActorResponsibilityScopes.None, SelectionStrategy = ActorSelectionStrategies.Queue, IsActive = true
            });
        }
        await db.SaveChangesAsync();

        // ---- تعریف گردش‌کار
        var registry = new WorkflowActionRegistry();
        var evaluator = new WorkflowConditionEvaluator(PartCatalogueWorkflow.Fields);
        var validator = new WorkflowVersionValidator(db, registry, evaluator, new PemsWorkflowReferenceCatalog(db));
        var defs = new WorkflowDefinitionService(db, validator);
        var bindings = new WorkflowBindingService(db);
        var actor = new WorkflowActor(null, "system-seed");

        var def = await db.WorkflowDefinitions.FirstOrDefaultAsync(d =>
            d.Module == PartCatalogueWorkflow.Module && d.Category == PartCatalogueWorkflow.Context && d.Name == PartCatalogueWorkflow.DefinitionName);

        if (def == null)
        {
            def = await defs.CreateDefinitionAsync(PartCatalogueWorkflow.DefinitionName, PartCatalogueWorkflow.Context, PartCatalogueWorkflow.Module,
                "Part Catalogue (Add/Change) approval. Stages and entry conditions mirror the configured PartCatalogueWorkflowSteps; actors are resolved by the Enterprise Actor Resolver.", actor);
            var v = await defs.CreateDraftVersionAsync(def.WorkflowDefinitionId, actor, "Initial version - generated from the configured Part Catalogue steps");

            var active = steps.Where(s => s.IsActive).OrderBy(s => s.StepOrder).ToList();
            var stages = new List<(PartCatalogueWorkflowStep Step, int StageId)>();
            foreach (var s in active)
            {
                var st = await defs.AddStageAsync(v.WorkflowVersionId, new WorkflowStageInput
                {
                    Name = s.StepKey, NameFa = s.NameFa, StageOrder = s.StepOrder, StageType = "Sequential",
                    ApprovalStrategy = "AllApprovalsRequired", ResponsibilityKey = s.StepKey,
                    EntryCondition = PartCatalogueWorkflow.ConditionExpression(s.ConditionKey),
                    Description = s.NameEn, IsDelegatable = false
                }, actor);
                stages.Add((s, st.WorkflowStageId));
            }

            if (stages.Count == 0) throw new InvalidOperationException("Part Catalogue workflow has no active steps.");

            await defs.AddTransitionAsync(v.WorkflowVersionId, new WorkflowTransitionInput { FromStageId = null, ToStageId = stages[0].StageId, ActionType = "Start", Priority = 1 }, actor);
            for (var i = 0; i < stages.Count; i++)
            {
                var (step, id) = stages[i];
                var perm = $"{PartCatalogueWorkflow.Module}.{PartCatalogueWorkflow.PermissionAction(step.Kind)}";
                int? next = i + 1 < stages.Count ? stages[i + 1].StageId : null;
                await defs.AddTransitionAsync(v.WorkflowVersionId, new WorkflowTransitionInput
                { FromStageId = id, ToStageId = next, ActionType = "Approve", Priority = 1, RequiredPermission = perm, ForbidInitiator = true, Label = "Approve", LabelFa = "تأیید" }, actor);
                await defs.AddTransitionAsync(v.WorkflowVersionId, new WorkflowTransitionInput
                { FromStageId = id, ToStageId = null, ActionType = "Return", Priority = 1, RequiredPermission = perm, ForbidInitiator = true, Label = "Return for correction", LabelFa = "بازگشت برای اصلاح" }, actor);
            }

            var result = await defs.ValidateVersionAsync(v.WorkflowVersionId, actor);
            if (!result.IsValid)
                throw new InvalidOperationException("Part Catalogue workflow validation failed: " + string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
            await defs.PublishVersionAsync(v.WorkflowVersionId, actor);
        }

        if (!await db.WorkflowBindings.AnyAsync(b =>
                b.Module == PartCatalogueWorkflow.Module && b.EntityType == PartCatalogueWorkflow.EntityType && b.Context == PartCatalogueWorkflow.Context))
        {
            var published = await db.WorkflowVersions.Where(x => x.WorkflowDefinitionId == def.WorkflowDefinitionId && x.Status == "Published")
                .OrderByDescending(x => x.VersionNumber).FirstAsync();
            await bindings.CreateAsync(PartCatalogueWorkflow.Module, PartCatalogueWorkflow.EntityType, PartCatalogueWorkflow.Context,
                def.WorkflowDefinitionId, published.WorkflowVersionId, null, null, actor);
        }
    }
}
