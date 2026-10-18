using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Persistence.Workflow;
using PEMS.WorkflowPlatform.Entities;
using PEMS.WorkflowPlatform.Interfaces;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Persistence.Seed;

/// <summary>
/// Phase 2 (Workflow Platform): تعریف گردش‌کار Work Request روی پلتفرم مرکزی + Binding صریح.
/// از همان چرخه‌ی Phase 1 عبور می‌کند (Draft → Validate → Publish + لاگ ممیزی پیکربندی)، نه درج مستقیم.
/// مراحل، عیناً جریان موجود سرویس هستند (Submitted → UnderReview → Approved → Planned → Converted؛ Reject/Cancel):
/// هیچ مرحله‌ی کسب‌وکاری جدیدی اختراع نشده. شرکت‌کننده‌ی هر مرحله «Permission:WorkRequest.Create/Approve» است
/// (همان مجوزهای فعلی)، و هر گذار RequiredPermission خودش را دارد.
/// Idempotent: اگر تعریف وجود دارد چیزی تغییر نمی‌کند؛ Binding فقط وقتی ساخته می‌شود که هیچ Binding ای برای این کلید
/// وجود نداشته باشد (غیرفعال‌سازی عمدی مدیر بازنویسی نمی‌شود). Instance های موجود هرگز لمس نمی‌شوند.
/// </summary>
public static class WorkRequestWorkflowSeeder
{
    public const string DefinitionName = "Work Request Approval";
    private const string Both = "Permission:WorkRequest.Create,Permission:WorkRequest.Approve";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        // کاتالوگ مجوزها (طبق همان قالب AccessControlSeeder). ردیف‌های موجود دست نمی‌خورند.
        foreach (var action in new[] { "Create", "Approve" })
        {
            var name = $"WorkRequest.{action}";
            if (!await db.Permissions.AnyAsync(p => p.Name == name))
            {
                db.Permissions.Add(new Permission { Name = name, ResourceName = "WorkRequest", ActionName = action, IsDestructive = false, IsActive = true, CreatedDate = DateTime.Now });
                await db.SaveChangesAsync();
            }
        }

        var registry = new WorkflowActionRegistry();
        var validator = new WorkflowVersionValidator(db, registry, new WorkflowConditionEvaluator(), new PemsWorkflowReferenceCatalog(db));
        var defs = new WorkflowDefinitionService(db, validator);
        var bindings = new WorkflowBindingService(db);
        var actor = new WorkflowActor(null, "system-seed");

        var def = await db.WorkflowDefinitions.FirstOrDefaultAsync(d =>
            d.Module == WorkRequestService.WorkflowModule && d.Category == WorkRequestService.WorkflowContext && d.Name == DefinitionName);

        if (def == null)
        {
            def = await defs.CreateDefinitionAsync(DefinitionName, WorkRequestService.WorkflowContext, WorkRequestService.WorkflowModule,
                "Work Request approval: Submitted → Under Review → Approved → Planned → Converted (Reject/Cancel).", actor);
            var v = await defs.CreateDraftVersionAsync(def.WorkflowDefinitionId, actor, "Initial version - mirrors the existing Work Request flow");
            await BuildStandardStructureAsync(defs, v.WorkflowVersionId, actor);

            var result = await defs.ValidateVersionAsync(v.WorkflowVersionId, actor);
            if (!result.IsValid)
                throw new InvalidOperationException("Work Request workflow validation failed: " + string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
            await defs.PublishVersionAsync(v.WorkflowVersionId, actor);
        }

        if (!await db.WorkflowBindings.AnyAsync(b =>
                b.Module == WorkRequestService.WorkflowModule && b.EntityType == WorkRequestService.WorkflowEntityType && b.Context == WorkRequestService.WorkflowContext))
        {
            var published = await db.WorkflowVersions.Where(x => x.WorkflowDefinitionId == def.WorkflowDefinitionId && x.Status == "Published")
                .OrderByDescending(x => x.VersionNumber).FirstAsync();
            // پین به نسخه‌ی مشخص: انتشار نسخه‌ی بعدی به‌تنهایی رفتار را عوض نمی‌کند؛ مدیر باید Binding را صریحاً تغییر دهد
            await bindings.CreateAsync(WorkRequestService.WorkflowModule, WorkRequestService.WorkflowEntityType, WorkRequestService.WorkflowContext,
                def.WorkflowDefinitionId, published.WorkflowVersionId, null, null, actor);
        }
    }

    /// <summary>ساختار نسخه‌ی 1 (همان جریان موجود). برای تست‌ها هم قابل استفاده است.</summary>
    public static async Task BuildStandardStructureAsync(IWorkflowDefinitionService defs, int versionId, WorkflowActor actor)
    {
        WorkflowStageInput S(string name, string fa, int order, string status) => new()
        {
            Name = name, NameFa = fa, StageOrder = order, StageType = "Sequential", ResponsibleRole = Both,
            BusinessStatus = status, ApprovalStrategy = "AllApprovalsRequired"
        };
        WorkflowTransitionInput T(int? from, int? to, string action, int prio, string? perm, string? label, string? labelFa) => new()
        {
            FromStageId = from, ToStageId = to, ActionType = action, Priority = prio, RequiredPermission = perm, Label = label, LabelFa = labelFa
        };

        var submitted = await defs.AddStageAsync(versionId, S("Submitted", "ثبت‌شده", 10, "Submitted"), actor);
        var review = await defs.AddStageAsync(versionId, S("Under Review", "در حال بررسی", 20, "UnderReview"), actor);
        var approved = await defs.AddStageAsync(versionId, S("Approved", "تأییدشده", 30, "Approved"), actor);
        var planned = await defs.AddStageAsync(versionId, S("Planned", "برنامه‌ریزی‌شده", 40, "Planned"), actor);

        const string Create = "WorkRequest.Create";
        const string Approve = "WorkRequest.Approve";
        await defs.AddTransitionAsync(versionId, T(null, submitted.WorkflowStageId, "Start", 1, null, null, null), actor);

        await defs.AddTransitionAsync(versionId, T(submitted.WorkflowStageId, review.WorkflowStageId, "Approve", 1, Create, "Start Review", "شروع بررسی"), actor);
        await defs.AddTransitionAsync(versionId, T(submitted.WorkflowStageId, null, "Cancel", 1, Create, "Cancel Request", "لغو درخواست"), actor);

        await defs.AddTransitionAsync(versionId, T(review.WorkflowStageId, approved.WorkflowStageId, "Approve", 1, Approve, "Approve", "تأیید"), actor);
        await defs.AddTransitionAsync(versionId, T(review.WorkflowStageId, null, "Reject", 1, Approve, "Reject", "رد"), actor);
        await defs.AddTransitionAsync(versionId, T(review.WorkflowStageId, null, "Cancel", 1, Create, "Cancel Request", "لغو درخواست"), actor);

        await defs.AddTransitionAsync(versionId, T(approved.WorkflowStageId, planned.WorkflowStageId, "Approve", 1, Approve, "Plan", "برنامه‌ریزی"), actor);
        await defs.AddTransitionAsync(versionId, T(approved.WorkflowStageId, null, "Reject", 1, Approve, "Reject", "رد"), actor);
        await defs.AddTransitionAsync(versionId, T(approved.WorkflowStageId, null, "Cancel", 1, Create, "Cancel Request", "لغو درخواست"), actor);

        await defs.AddTransitionAsync(versionId, T(planned.WorkflowStageId, null, "Complete", 1, Approve, "Convert to Work Order", "تبدیل به دستور کار"), actor);
        await defs.AddTransitionAsync(versionId, T(planned.WorkflowStageId, null, "Cancel", 1, Create, "Cancel Request", "لغو درخواست"), actor);
    }
}
