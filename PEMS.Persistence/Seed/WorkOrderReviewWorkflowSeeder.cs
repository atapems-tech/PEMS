using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// ==============================================================================
/// طبق درخواست: یک WorkOrder ممکن است نیاز به ثبت نظر چند دیسیپلین تعمیراتی مختلف داشته
/// باشد - طبق رویه‌ی استاندارد هماهنگی چند-تخصصی (Multi-Craft Coordination، EN 13460).
/// دقیقاً همون الگوی IDC مدرک مهندسی و DisciplineReview VDR: یک مرحله‌ی Parallel با اعضای
/// پویا؛ اگر دیسیپلینی ارجاع داده نشود، خودکار Skip می‌شود (بدون تغییر در موتور).
/// ==============================================================================
/// </summary>
public static class WorkOrderReviewWorkflowSeeder
{
    public const string Module = "WorkOrder";
    public const string Category = "MultiDisciplineReview";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var alreadyExists = await db.WorkflowDefinitions.AnyAsync(x => x.Module == Module && x.Category == Category);
        if (alreadyExists) return;

        var definition = new WorkflowDefinition
        {
            Name = "بازبینی چند-دیسیپلینه WorkOrder",
            Category = Category,
            Module = Module,
            Description = "دیسیپلین‌های تعمیراتی ارجاع‌شده، یافته/نظر خودشان را روی همین WorkOrder ثبت می‌کنند",
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        var version = new WorkflowVersion
        {
            Definition = definition,
            VersionNumber = 1,
            Status = "Published",
            EffectiveDate = DateTime.Now,
            PublishedDate = DateTime.Now
        };

        var disciplineReviewStage = new WorkflowStage
        {
            Version = version,
            Name = "DisciplineReview",
            StageOrder = 1,
            StageType = "Parallel",
            ResponsibleRole = null,
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = false
        };

        version.Stages.Add(disciplineReviewStage);

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = null, ToStage = disciplineReviewStage, ActionType = "Start", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = disciplineReviewStage, ToStage = null, ActionType = "Approve", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = disciplineReviewStage, ToStage = null, ActionType = "Reject", Priority = 0
        });

        definition.Versions.Add(version);

        db.WorkflowDefinitions.Add(definition);
        await db.SaveChangesAsync();
    }
}
