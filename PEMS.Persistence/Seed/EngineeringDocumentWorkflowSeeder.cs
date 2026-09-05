using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// ==============================================================================
/// تعریف گردش‌کار «تایید مدرک مهندسی» را روی Workflow Platform Seed می‌کند.
///
/// نسخه‌ی ۲ (فعلی): Check → IDC (بازبینی موازی، اعضای پویا بر اساس تشخیص سازنده‌ی مدرک) →
/// Approval → Issue.
///
/// طبق قانون Versioning که در فاز A طراحی شد: اگر نسخه‌ی ۱ (بدون IDC) از قبل روی
/// دیتابیس Seed شده باشد، این‌جا آن را دست‌نخورده می‌گذاریم (Instance های در حال اجرای
/// قدیمی همچنان با همان نسخه ادامه می‌دهند) و فقط نسخه‌ی ۲ را به‌عنوان نسخه‌ی جدید و
/// Published اضافه می‌کنیم - Instance های جدید خودکار از نسخه‌ی ۲ استفاده می‌کنند.
/// ==============================================================================
/// </summary>
public static class EngineeringDocumentWorkflowSeeder
{
    public const string Module = "EngineeringDocument";
    public const string Category = "Approval";

    // پیشوند نقش‌های پویای IDC - هر دیسیپلین انتخاب‌شده به‌صورت "Discipline:{DisciplineId}"
    // به‌عنوان یک "نقش" موقت به موتور عمومی معرفی می‌شود (موتور معنای آن را نمی‌داند، فقط
    // یک رشته است - دقیقاً طبق اصل عمومی‌بودن موتور).
    public const string DisciplineRolePrefix = "Discipline:";

    public static string DisciplineRole(int disciplineId) => $"{DisciplineRolePrefix}{disciplineId}";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var definition = await db.WorkflowDefinitions
            .FirstOrDefaultAsync(x => x.Module == Module && x.Category == Category);

        if (definition == null)
        {
            definition = new WorkflowDefinition
            {
                Name = "تایید مدرک مهندسی",
                Category = Category,
                Module = Module,
                Description = "گردش‌کار Check → IDC → Approve → Issue برای مدارک مهندسی",
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            db.WorkflowDefinitions.Add(definition);
            await db.SaveChangesAsync();
        }

        var hasIdcVersion = await db.WorkflowVersions
            .AnyAsync(v => v.WorkflowDefinitionId == definition.WorkflowDefinitionId && v.VersionNumber == 2);

        if (hasIdcVersion) return;

        var nextVersionNumber = 1 + await db.WorkflowVersions
            .Where(v => v.WorkflowDefinitionId == definition.WorkflowDefinitionId)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync() ?? 1;

        var version = new WorkflowVersion
        {
            WorkflowDefinitionId = definition.WorkflowDefinitionId,
            VersionNumber = nextVersionNumber,
            Status = "Published",
            EffectiveDate = DateTime.Now,
            PublishedDate = DateTime.Now
        };

        var checkStage = new WorkflowStage
        {
            Version = version,
            Name = "Check",
            StageOrder = 1,
            StageType = "Sequential",
            ResponsibleRole = string.Join(",",
                SystemRoles.SystemAdmin, SystemRoles.EngineeringManager, SystemRoles.EngineeringSupervisor),
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = true
        };

        // ==============================================================================
        // مرحله‌ی IDC: اعضایش در این‌جا ثابت نیست (ResponsibleRole خالی می‌ماند) - سازنده‌ی
        // مدرک هنگام Submit تشخیص می‌دهد کدام دیسیپلین‌ها لازم است بازبینی کنند، و آن لیست
        // موقع اجرای اکشن Check به‌صورت پویا (nextStageParticipantRoles) به موتور داده
        // می‌شود. اگر هیچ دیسیپلینی انتخاب نشود، موتور خودش این مرحله را خودکار Skip می‌کند.
        // ==============================================================================
        var idcStage = new WorkflowStage
        {
            Version = version,
            Name = "IDC",
            StageOrder = 2,
            StageType = "Parallel",
            ResponsibleRole = null,
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = false,
            Description = "بازبینی بین‌دیسیپلینی موازی - اعضا پویا و بر اساس تشخیص سازنده‌ی مدرک"
        };

        var approvalStage = new WorkflowStage
        {
            Version = version,
            Name = "Approval",
            StageOrder = 3,
            StageType = "Sequential",
            ResponsibleRole = string.Join(",", SystemRoles.SystemAdmin, SystemRoles.EngineeringManager),
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = true
        };

        var issueStage = new WorkflowStage
        {
            Version = version,
            Name = "Issue",
            StageOrder = 4,
            StageType = "Sequential",
            ResponsibleRole = string.Join(",",
                SystemRoles.SystemAdmin, SystemRoles.EngineeringManager, SystemRoles.DocumentController),
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = true
        };

        version.Stages.Add(checkStage);
        version.Stages.Add(idcStage);
        version.Stages.Add(approvalStage);
        version.Stages.Add(issueStage);

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = null, ToStage = checkStage, ActionType = "Start", Priority = 0
        });

        // Check → IDC (یا اگر IDC لازم نبود، موتور خودش رد می‌شود) / Check → پایان (Reject)
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = checkStage, ToStage = idcStage, ActionType = "Approve", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = checkStage, ToStage = null, ActionType = "Reject", Priority = 0
        });

        // IDC → Approval (همه‌ی دیسیپلین‌های ارجاع‌شده Approve کردند) / IDC → پایان (حداقل
        // یکی Comment یا Reject داد - طبق درخواست، فرآیند باید مجدد شود؛ سازنده باید اصلاح
        // و دوباره Submit کند)
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = idcStage, ToStage = approvalStage, ActionType = "Approve", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = idcStage, ToStage = null, ActionType = "Reject", Priority = 0
        });

        // Approval
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = approvalStage, ToStage = issueStage, ActionType = "Approve", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = approvalStage, ToStage = null, ActionType = "Reject", Priority = 0
        });

        // Issue
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = issueStage, ToStage = null, ActionType = "Issue", Priority = 0
        });

        db.WorkflowVersions.Add(version);
        await db.SaveChangesAsync();
    }
}
