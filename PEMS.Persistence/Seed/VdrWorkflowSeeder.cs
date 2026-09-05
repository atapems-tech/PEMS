using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// ==============================================================================
/// گردش‌کار بازبینی مدارک وندور (VDR) - طبق استاندارد جهانی EPC (مشابه رویه‌ی ISO 10007
/// برای مدیریت پیکربندی، و رویه‌ی متداول Vendor Document Review در قراردادهای EPC):
///
///   وندور ارسال می‌کند → بازبینی موازی دیسیپلین‌ها (مثل IDC مدرک مهندسی) → تصمیم نهایی
///   بازبین ارشد با یکی از ۵ کد استاندارد صنعت → بازگشت به وندور توسط DCC
///
/// کدهای بازبینی (دقیقاً همان چیزی که از قبل در VDRSubmission.ReviewCategory تعریف شده بود):
///   AP = Approved                    - می‌تواند ادامه دهد
///   NC = No Comment                  - می‌تواند ادامه دهد
///   AC = Approved with Comments      - می‌تواند ادامه دهد، کامنت‌ها در رویژن بعدی اعمال شود
///   CO = Comment / Revise & Resubmit - باید اصلاح و دوباره ارسال شود
///   RE = Rejected                    - رد کامل، دوباره از صفر
///
/// طبق همان اصل مدرک مهندسی: اگر پروژه نیاز به بازبینی چند دیسیپلین نداشته باشد، آن مرحله
/// توسط موتور خودکار Skip می‌شود (اعضای پویا - از طریق DocumentIdcReferral که قبلاً برای
/// IDC مدرک مهندسی ساختیم و اینجا بازاستفاده می‌شود، چون DocumentId کلید مشترک هر دو
/// VDRItem و EngineeringDocument است - هر دو از Document ارث می‌برند).
/// ==============================================================================
/// </summary>
public static class VdrWorkflowSeeder
{
    public const string Module = "VendorDocument";
    public const string Category = "Review";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var alreadyExists = await db.WorkflowDefinitions.AnyAsync(x => x.Module == Module && x.Category == Category);
        if (alreadyExists) return;

        var definition = new WorkflowDefinition
        {
            Name = "بازبینی مدارک وندور (VDR)",
            Category = Category,
            Module = Module,
            Description = "بازبینی موازی دیسیپلین‌ها → تصمیم نهایی → بازگشت به وندور",
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

        // مرحله ۱: بازبینی موازی دیسیپلین‌ها - اعضا پویا (مثل IDC)، اگر خالی باشد Skip می‌شود
        var disciplineReviewStage = new WorkflowStage
        {
            Version = version,
            Name = "DisciplineReview",
            StageOrder = 1,
            StageType = "Parallel",
            ResponsibleRole = null,
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = false,
            Description = "بازبینی موازی دیسیپلین‌های مرتبط - نظرات ورودی برای تصمیم نهایی"
        };

        // مرحله ۲: تصمیم نهایی بازبین ارشد - یکی از ۵ کد استاندارد صنعت
        var leadReviewStage = new WorkflowStage
        {
            Version = version,
            Name = "LeadReview",
            StageOrder = 2,
            StageType = "Sequential",
            ResponsibleRole = string.Join(",",
                SystemRoles.SystemAdmin, SystemRoles.EngineeringManager, SystemRoles.EngineeringSupervisor),
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = true
        };

        // مرحله ۳: بازگشت رسمی به وندور - فقط برای مسیرهای AP/NC/AC (نه CO/RE که خاتمه می‌یابند)
        var returnToVendorStage = new WorkflowStage
        {
            Version = version,
            Name = "ReturnToVendor",
            StageOrder = 3,
            StageType = "Sequential",
            ResponsibleRole = string.Join(",",
                SystemRoles.SystemAdmin, SystemRoles.EngineeringManager, SystemRoles.DocumentController),
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = true
        };

        version.Stages.Add(disciplineReviewStage);
        version.Stages.Add(leadReviewStage);
        version.Stages.Add(returnToVendorStage);

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = null, ToStage = disciplineReviewStage, ActionType = "Start", Priority = 0
        });

        // نتیجه‌ی بازبینی دیسیپلین‌ها (چه Approve چه Reject/Comment) هردو به تصمیم نهایی
        // می‌رود - نظر دیسیپلین‌ها ورودی تصمیم است، نه تصمیم قطعی
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = disciplineReviewStage, ToStage = leadReviewStage, ActionType = "Approve", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = disciplineReviewStage, ToStage = leadReviewStage, ActionType = "Reject", Priority = 0
        });

        // تصمیم نهایی - هر ۵ کد استاندارد باید رسماً به وندور اطلاع داده شوند (حتی CO/RE -
        // DCC نمی‌تواند بی‌سروصدا فقط وضعیت را «نیاز به ارسال مجدد» کند بدون اطلاع‌رسانی رسمی)
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = leadReviewStage, ToStage = returnToVendorStage, ActionType = "AP", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = leadReviewStage, ToStage = returnToVendorStage, ActionType = "NC", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = leadReviewStage, ToStage = returnToVendorStage, ActionType = "AC", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = leadReviewStage, ToStage = returnToVendorStage, ActionType = "CO", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = leadReviewStage, ToStage = returnToVendorStage, ActionType = "RE", Priority = 0
        });

        // بازگشت رسمی به وندور - پایان چرخه (وضعیت نهایی Closed/PendingResubmission را لایه‌ی
        // سرویس از روی ReviewCategory ثبت‌شده در LeadReview تعیین می‌کند، نه از این گذار)
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = returnToVendorStage, ToStage = null, ActionType = "Issue", Priority = 0
        });

        definition.Versions.Add(version);

        db.WorkflowDefinitions.Add(definition);
        await db.SaveChangesAsync();
    }
}
