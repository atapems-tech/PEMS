using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// ==============================================================================
/// طبق ممیزی Material Management (Gap B) + Step 3: گردش‌کار تایید Disposition دریافت مادی
/// (IRN) - عیناً هم‌الگو با NcrApprovalWorkflowSeeder (همون موتور IWorkflowRuntime، بدون هیچ
/// تغییری در PEMS.WorkflowPlatform). اصل «Received ≠ Available»: بازرس مقدار
/// Accepted/Rejected/Hold را روی خطوط ثبت می‌کند (IrnReceiptService.RecordDispositionAsync)،
/// ولی این‌جا (تایید مستقل QA) باید تصویب شود پیش از این‌که مقدار Accepted واقعاً به
/// InventoryTransaction/MaterialStock تبدیل شود - دقیقاً هم‌سو با قاعده‌ی ISO 9001 بند ۸.۷ که
/// NCR بحرانی از قبل در این کدبیس پیاده کرده. Hold یک فعل جداگانه‌ی گردش‌کار نیست - در سطح
/// داده (IrnReceiptLine.HoldQuantity) ثبت می‌شود؛ همین دو انتقال Approve/Reject (عیناً مثل NCR)
/// برای تصمیم نهایی روی کل Disposition کافی است.
/// ==============================================================================
/// </summary>
public static class IrnDispositionWorkflowSeeder
{
    public const string Module = "IRN";
    public const string Category = "DispositionApproval";
    public const string BusinessObjectType = "IrnReceiptDisposition";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var alreadyExists = await db.WorkflowDefinitions.AnyAsync(x => x.Module == Module && x.Category == Category);
        if (alreadyExists) return;

        var definition = new WorkflowDefinition
        {
            Name = "تایید Disposition دریافت مادی (IRN)",
            Category = Category,
            Module = Module,
            Description = "پیش از تبدیل مقدار پذیرفته‌شده‌ی یک IRN به موجودی قابل‌استفاده، تایید مستقل QA الزامی است",
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
            Name = "QADispositionApproval",
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
