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
/// نسخه‌ی ۳ (فعلی): Check → IDC (بازبینی موازی مشورتی، اعضای پویا بر اساس تشخیص سازنده‌ی
/// مدرک) → Approval (تصمیم رسمی) → Issue. تفاوت با نسخه‌ی ۲: نتیجه‌ی IDC (حتی اگر شامل
/// Reject از یک یا چند دیسیپلین باشد) دیگر کل مدرک را رد/تمام نمی‌کند - همیشه به مرحله‌ی
/// Approval می‌رود تا تصمیم رسمی (Approve/Reject/Revise & Resubmit) واقعاً دست تأییدکننده/
/// سازنده‌ی مدرک بماند، نه دست بازبین‌های دیسیپلین (نگاه کنید SeedVersion3Async پایین‌تر).
///
/// طبق قانون Versioning که در فاز A طراحی شد: نسخه‌های منتشرشده‌ی قبلی (۱ و ۲) هرگز تغییر
/// نمی‌کنند - Instance های در حال اجرای قدیمی همچنان با همان نسخه‌ای که باهاش شروع شده‌اند
/// ادامه می‌دهند. هر اصلاح رفتاری (مثل این یکی) فقط با افزودن یک نسخه‌ی جدید و Published
/// انجام می‌شود - Instance های جدید خودکار از آخرین نسخه استفاده می‌کنند.
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

        if (!hasIdcVersion)
        {
            await SeedVersion2Async(db, definition);
        }

        await SeedVersion3Async(db, definition);
    }

    // ==============================================================================
    // نسخه‌ی ۲: Check → IDC (موازی) → Approval → Issue. مرحله‌ی IDC: هر دیسیپلین Approve/
    // Comment/Reject می‌دهد؛ اگر حتی یکی Reject بدهد، کل Instance بلافاصله با وضعیت "Rejected"
    // تمام می‌شود (ToStage=null) - این دقیقاً همان نقصی است که نسخه‌ی ۳ اصلاح می‌کند (پایین‌تر).
    // نسخه‌ی ۲ این‌جا دست‌نخورده باقی می‌ماند چون Instance های قدیمیِ در حال اجرا با همین نسخه
    // شروع شده‌اند - طبق قانون Versioning همین فایل، نسخه‌های منتشرشده هرگز تغییر نمی‌کنند.
    // ==============================================================================
    private static async Task SeedVersion2Async(PEMSDbContext db, WorkflowDefinition definition)
    {
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

    // ==============================================================================
    // نسخه‌ی ۳ - طبق «PEMS Engineering IDC Ownership Correction»: تنها تفاوت با نسخه‌ی ۲،
    // گذار Reject خروجی از مرحله‌ی IDC است. در نسخه‌ی ۲، اگر حتی یک دیسیپلینِ ارجاع‌شده Reject
    // می‌داد، کل Instance بلافاصله با وضعیت نهاییِ "Rejected" تمام می‌شد - یعنی یک بازبین
    // دیسیپلین (نه سازنده‌ی مدرک، نه تأییدکننده‌ی رسمی) عملاً قدرت پایان‌دادن به کل چرخه‌ی
    // مدرک را داشت. طبق الزام کسب‌وکار، IDC فقط یک بازبینی داخلی مشورتی است - نتیجه‌اش (چه
    // Approve چه Comment چه Reject از هر تعداد دیسیپلین) باید فقط به‌عنوان ورودی جمع‌آوری‌شده
    // به سازنده/تأییدکننده‌ی مدرک برسد، نه این‌که مستقیماً وضعیت رسمی مدرک را تعیین کند.
    //
    // اصلاح: خروجیِ IDC (چه Approve چه Reject) هر دو به همان مرحله‌ی Approval می‌روند - دقیقاً
    // همان مرحله‌ای که از قبل با مجوز درست (EngineeringManager/SystemAdmin + بررسی SoD در
    // EngineeringDocumentsController.CanApproveDocumentDisciplineAsync) برای تصمیم رسمی
    // (Approve/Reject/Revise & Resubmit) وجود داشت. یعنی «تصمیم رسمی» هیچ‌وقت به دست بازبین
    // IDC نبود و نیست - فقط حالا واقعاً به همان‌جا می‌رسد به‌جای این‌که IDC خودش سند را ببندد.
    //
    // طبق قانون Versioning: نسخه‌ی ۲ دست‌نخورده می‌ماند (Instance های در حال اجرا با آن ادامه
    // می‌دهند)؛ این‌جا فقط یک نسخه‌ی جدید Published اضافه می‌شود - Instance های جدید (از بعد
    // این Deploy) خودکار نسخه‌ی ۳ را می‌گیرند.
    // ==============================================================================
    private static async Task SeedVersion3Async(PEMSDbContext db, WorkflowDefinition definition)
    {
        var hasV3 = await db.WorkflowVersions
            .AnyAsync(v => v.WorkflowDefinitionId == definition.WorkflowDefinitionId && v.VersionNumber == 3);

        if (hasV3) return;

        var version = new WorkflowVersion
        {
            WorkflowDefinitionId = definition.WorkflowDefinitionId,
            VersionNumber = 3,
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

        var idcStage = new WorkflowStage
        {
            Version = version,
            Name = "IDC",
            StageOrder = 2,
            StageType = "Parallel",
            ResponsibleRole = null,
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = false,
            Description = "بازبینی بین‌دیسیپلینی موازی - صرفاً مشورتی؛ نتیجه‌اش (Approve/Comment/Reject" +
                " هر دیسیپلین) هرگز به‌تنهایی وضعیت رسمی مدرک را تعیین نمی‌کند - همیشه به مرحله‌ی" +
                " Approval (تصمیم سازنده/تأییدکننده‌ی مدرک) می‌رود"
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

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = checkStage, ToStage = idcStage, ActionType = "Approve", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = checkStage, ToStage = null, ActionType = "Reject", Priority = 0
        });

        // ==============================================================================
        // تنها تغییر واقعی نسبت به نسخه‌ی ۲: خروجی Reject مرحله‌ی IDC دیگر ToStage=null
        // (پایان/رد کل مدرک) نیست - به همان مرحله‌ی Approval می‌رود که خروجی Approve می‌رود.
        // موتور همچنان در WorkflowHistory دقیقاً ثبت می‌کند که خروجی این مرحله Reject بوده
        // (برای Audit Trail)؛ فقط این خروجی دیگر معادل «رد رسمی مدرک» نیست.
        // ==============================================================================
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = idcStage, ToStage = approvalStage, ActionType = "Approve", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = idcStage, ToStage = approvalStage, ActionType = "Reject", Priority = 0
        });

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = approvalStage, ToStage = issueStage, ActionType = "Approve", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = approvalStage, ToStage = null, ActionType = "Reject", Priority = 0
        });

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = issueStage, ToStage = null, ActionType = "Issue", Priority = 0
        });

        db.WorkflowVersions.Add(version);
        await db.SaveChangesAsync();
    }
}
