using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// ==============================================================================
/// گردش‌کار TQ/TSR/RFI - طبق فاز ۱ نقشه‌راه معماری. دقیقاً همون اصلی که در نقشه‌راه قول
/// دادیم: "هر فرآیند جدید ابتدا روی موتور عمومی Workflow Platform موجود پیاده می‌شود، نه یک
/// موتور گردش‌کار جدید" - بدون تغییر در خودِ موتور.
///
///   PendingResponse (دیسیپلین مسئول پاسخ می‌دهد)
///        → PendingConfirmation (دیسیپلین ثبت‌کننده تایید/رد می‌کند)
///        → Closed  یا  → بازگشت به PendingResponse (اگر رد شود)
/// ==============================================================================
/// </summary>
public static class TechnicalQueryWorkflowSeeder
{
    public const string Module = "TechnicalQuery";
    public const string Category = "Response";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var alreadyExists = await db.WorkflowDefinitions.AnyAsync(x => x.Module == Module && x.Category == Category);
        if (alreadyExists) return;

        var definition = new WorkflowDefinition
        {
            Name = "پاسخ‌دهی TQ/TSR/RFI",
            Category = Category,
            Module = Module,
            Description = "ارجاع به دیسیپلین مسئول → پاسخ → تایید ثبت‌کننده یا بازگشت",
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

        var pendingResponseStage = new WorkflowStage
        {
            Version = version,
            Name = "PendingResponse",
            StageOrder = 1,
            StageType = "Sequential",
            // اعضای پویا - دیسیپلین مسئول در زمان ثبت مشخص می‌شود (مثل IDC/VDR)
            ResponsibleRole = null,
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = true
        };

        var pendingConfirmationStage = new WorkflowStage
        {
            Version = version,
            Name = "PendingConfirmation",
            StageOrder = 2,
            StageType = "Sequential",
            ResponsibleRole = null,
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = true
        };

        version.Stages.Add(pendingResponseStage);
        version.Stages.Add(pendingConfirmationStage);

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = null, ToStage = pendingResponseStage, ActionType = "Start", Priority = 0
        });

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = pendingResponseStage, ToStage = pendingConfirmationStage, ActionType = "Respond", Priority = 0
        });

        // تایید ثبت‌کننده → بسته می‌شود
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = pendingConfirmationStage, ToStage = null, ActionType = "Confirm", Priority = 0
        });

        // رد پاسخ توسط ثبت‌کننده → دوباره برمی‌گردد به دیسیپلین مسئول
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = pendingConfirmationStage, ToStage = pendingResponseStage, ActionType = "Reopen", Priority = 0
        });

        definition.Versions.Add(version);

        db.WorkflowDefinitions.Add(definition);
        await db.SaveChangesAsync();
    }
}
