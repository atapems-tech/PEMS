using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// ==============================================================================
/// گردش‌کار MOC - طبق فاز ۲ نقشه‌راه معماری: درخواست → ارزیابی ریسک → تایید → (تایید ارشد
/// فقط اگر ریسک بالا باشد) → اجرا/بستن.
///
/// نکته‌ی معماری کلیدی: مرحله‌ی «تایید ارشد» با همون مکانیزم اعضای پویا/Auto-Skip که برای
/// IDC و DisciplineReview VDR ساختیم پیاده شده - اگر ریسک High نباشد، لیست اعضا خالی
/// فرستاده می‌شود و موتور خودش این مرحله را رد می‌کند. هیچ تغییری در خود موتور لازم نبود.
/// ==============================================================================
/// </summary>
public static class MOCWorkflowSeeder
{
    public const string Module = "MOC";
    public const string Category = "ChangeApproval";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var alreadyExists = await db.WorkflowDefinitions.AnyAsync(x => x.Module == Module && x.Category == Category);
        if (alreadyExists) return;

        var definition = new WorkflowDefinition
        {
            Name = "تایید MOC (مدیریت تغییر)",
            Category = Category,
            Module = Module,
            Description = "ارزیابی ریسک → تایید → تایید ارشد (فقط ریسک بالا) → بستن",
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

        var riskAssessmentStage = new WorkflowStage
        {
            Version = version,
            Name = "RiskAssessment",
            StageOrder = 1,
            StageType = "Sequential",
            // اعضای پویا - دیسیپلین درخواست‌کننده در زمان ثبت مشخص می‌شود
            ResponsibleRole = null,
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = true
        };

        var approvalStage = new WorkflowStage
        {
            Version = version,
            Name = "Approval",
            StageOrder = 2,
            StageType = "Sequential",
            ResponsibleRole = string.Join(",", SystemRoles.SystemAdmin, SystemRoles.EngineeringSupervisor, SystemRoles.EngineeringManager),
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = true
        };

        var seniorApprovalStage = new WorkflowStage
        {
            Version = version,
            Name = "SeniorApproval",
            StageOrder = 3,
            StageType = "Sequential",
            // اعضای پویا - فقط وقتی RiskLevel=High است اعضا فرستاده می‌شوند، وگرنه خودکار Skip می‌شود
            ResponsibleRole = null,
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = false,
            Description = "فقط برای MOC های با ریسک بالا - طبق رویه‌ی PSM نیاز به تایید مستقل مدیریت ارشد دارد"
        };

        var closureStage = new WorkflowStage
        {
            Version = version,
            Name = "Closure",
            StageOrder = 4,
            StageType = "Sequential",
            ResponsibleRole = string.Join(",", SystemRoles.SystemAdmin, SystemRoles.EngineeringManager, SystemRoles.QAManager),
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = true
        };

        version.Stages.Add(riskAssessmentStage);
        version.Stages.Add(approvalStage);
        version.Stages.Add(seniorApprovalStage);
        version.Stages.Add(closureStage);

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = null, ToStage = riskAssessmentStage, ActionType = "Start", Priority = 0
        });

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = riskAssessmentStage, ToStage = approvalStage, ActionType = "Assess", Priority = 0
        });

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = approvalStage, ToStage = seniorApprovalStage, ActionType = "Approve", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = approvalStage, ToStage = null, ActionType = "Reject", Priority = 0
        });

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = seniorApprovalStage, ToStage = closureStage, ActionType = "Approve", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = seniorApprovalStage, ToStage = null, ActionType = "Reject", Priority = 0
        });

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = closureStage, ToStage = null, ActionType = "Close", Priority = 0
        });

        definition.Versions.Add(version);

        db.WorkflowDefinitions.Add(definition);
        await db.SaveChangesAsync();
    }
}
