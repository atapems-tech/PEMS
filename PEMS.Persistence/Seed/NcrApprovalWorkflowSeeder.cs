using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// ==============================================================================
/// گردش‌کار تایید NCR بحرانی - طبق ISO 9001 بند ۸.۷: عدم انطباق‌های بحرانی نمی‌توانند فقط
/// با تصمیم همان کسی که Disposition را ثبت کرده بسته شوند - نیاز به تایید مستقل مدیر QA
/// دارند. برای NCR های Minor/Major این مرحله اجرا نمی‌شود (سربار غیرضروری).
/// ==============================================================================
/// </summary>
public static class NcrApprovalWorkflowSeeder
{
    public const string Module = "NCR";
    public const string Category = "CriticalApproval";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var alreadyExists = await db.WorkflowDefinitions.AnyAsync(x => x.Module == Module && x.Category == Category);
        if (alreadyExists) return;

        var definition = new WorkflowDefinition
        {
            Name = "تایید NCR بحرانی",
            Category = Category,
            Module = Module,
            Description = "قبل از بستن هر NCR با شدت Critical، تایید مستقل مدیر QA الزامی است",
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

        var approvalStage = new WorkflowStage
        {
            Version = version,
            Name = "QAApproval",
            StageOrder = 1,
            StageType = "Sequential",
            ResponsibleRole = string.Join(",", SystemRoles.SystemAdmin, SystemRoles.QAManager),
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = true
        };

        version.Stages.Add(approvalStage);

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = null, ToStage = approvalStage, ActionType = "Start", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = approvalStage, ToStage = null, ActionType = "Approve", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = approvalStage, ToStage = null, ActionType = "Reject", Priority = 0
        });

        definition.Versions.Add(version);

        db.WorkflowDefinitions.Add(definition);
        await db.SaveChangesAsync();
    }
}
