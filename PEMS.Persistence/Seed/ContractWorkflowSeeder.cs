using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// ==============================================================================
/// طبق ممیزی Package Procurement Contract Finalization (§C, §D, §I قاعده‌ی ۷/۹): گردش‌کار
/// نهایی‌سازی قرارداد - عیناً هم‌الگو با NcrApprovalWorkflowSeeder/IrnDispositionWorkflowSeeder
/// (همون موتور IWorkflowRuntime، بدون هیچ تغییری در PEMS.WorkflowPlatform). دو مرحله:
///   ۱) DisciplineReview (Parallel، اعضای پویا - دقیقاً همون الگوی مرحله‌ی IDC در
///      EngineeringDocumentWorkflowSeeder): هر رشته‌ی الزامی (از
///      ContractWorkflowService.RequiredDisciplines) باید مستقل Approve کند.
///   ۲) FinalApproval (Sequential، ContractManager یا EngineeringManager): تصمیم نهایی.
/// امضا (Signed) و اثربخشی (Effective) بخشی از این گردش‌کار نیستند - این دو اقدام دستی جداگانه
/// در ContractWorkflowService هستند که فقط پس از تکمیل این گردش‌کار (Status=Approved) مجازند.
/// ==============================================================================
/// </summary>
public static class ContractWorkflowSeeder
{
    public const string Module = "Contract";
    public const string Category = "Finalization";
    public const string BusinessObjectType = "Contract";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var alreadyExists = await db.WorkflowDefinitions.AnyAsync(x => x.Module == Module && x.Category == Category);
        if (alreadyExists) return;

        var definition = new WorkflowDefinition
        {
            Name = "نهایی‌سازی قرارداد (Multi-Discipline Review → Final Approval)",
            Category = Category,
            Module = Module,
            Description = "بازبینی مستقل هر رشته‌ی الزامی، سپس تصمیم نهایی - پیش از امضا/اثربخشی قرارداد",
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

        // اعضای این مرحله ثابت نیستند - ContractWorkflowService با توجه به رشته‌های الزامیِ
        // پیکربندی‌شده، لیست را هنگام StartInstanceAsync می‌دهد (دقیقاً مثل IDC)
        var disciplineReviewStage = new WorkflowStage
        {
            Version = version,
            Name = "DisciplineReview",
            StageOrder = 1,
            StageType = "Parallel",
            ResponsibleRole = string.Empty,
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = true
        };

        var finalApprovalStage = new WorkflowStage
        {
            Version = version,
            Name = "FinalApproval",
            StageOrder = 2,
            StageType = "Sequential",
            ResponsibleRole = string.Join(",", SystemRoles.ContractManager, SystemRoles.EngineeringManager),
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = true
        };

        version.Stages.Add(disciplineReviewStage);
        version.Stages.Add(finalApprovalStage);

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = null, ToStage = disciplineReviewStage, ActionType = "Start", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = disciplineReviewStage, ToStage = finalApprovalStage, ActionType = "Approve", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = disciplineReviewStage, ToStage = null, ActionType = "Reject", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = finalApprovalStage, ToStage = null, ActionType = "Approve", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = finalApprovalStage, ToStage = null, ActionType = "Reject", Priority = 0
        });

        definition.Versions.Add(version);

        db.WorkflowDefinitions.Add(definition);
        await db.SaveChangesAsync();
    }
}
