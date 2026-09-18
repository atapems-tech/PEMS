using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// ==============================================================================
/// طبق «PEMS Help System — Phase 3» بخش ۱۴ (Seed Content). هر ردیف دقیقاً بر اساس یکی از این
/// دو منبعِ تاییدشده ساخته شده - نه حدس:
///   ۱) WorkflowDiagram: Module/Category واقعی که همین فاز از WorkflowDefinitions/Versions
///      زنده‌ی دیتابیس با sqlcmd دوباره تایید شد (EngineeringDocument/Approval,
///      VendorDocument/Review, Permit/PermitApproval, WorkOrder/MultiDisciplineReview).
///   ۲) دستی (LifecycleDiagram/EntityRelationship/StepGuide): عیناً همان مسیر
///      وضعیت/رابطه‌ای که در کدِ واقعی سرویس/کنترلر تایید شد (نه صرفاً متن مقاله):
///      - WorkRequestService.cs (StatusTransitions دیکشنری، خطوط ۲۳-۲۷) برای درخواست کار،
///        + قاعده‌ی «فقط Planned قابل تبدیل» (ConvertToWorkOrderAsync)
///      - Equipment.FunctionalLocationId (FK اختیاری) برای رابطه‌ی موقعیت فنی/تجهیز
///      - متن تاییدشده‌ی مقاله‌ی idc-inter-discipline-check برای StepGuide آن
///
/// ایدمپوتنت بر اساس (HelpArticleId + VisualType + CaptionEn) - HelpVisual کد یکتای طبیعی
/// ندارد (برخلاف HelpArticle.Code)، پس این سه‌تایی به‌عنوان کلید پایدار استفاده می‌شود.
/// ==============================================================================
/// </summary>
public static class HelpVisualSeeder
{
    private sealed record V(
        string ArticleCode, string VisualType, int Order,
        string? SourceModule, string? SourceCategory, string? DefinitionJson,
        string CaptionEn, string CaptionFa, string? LegendEn, string? LegendFa,
        string AccessEn, string AccessFa);

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var articleIds = await db.HelpArticles.ToDictionaryAsync(a => a.Code, a => a.HelpArticleId);

        foreach (var v in BuildVisuals())
        {
            if (!articleIds.TryGetValue(v.ArticleCode, out var articleId)) continue;

            var exists = await db.HelpVisuals.AnyAsync(x =>
                x.HelpArticleId == articleId && x.VisualType == v.VisualType && x.CaptionEn == v.CaptionEn);
            if (exists) continue;

            db.HelpVisuals.Add(new HelpVisual
            {
                HelpArticleId = articleId,
                VisualType = v.VisualType,
                DisplayOrder = v.Order,
                IsPublished = true,
                SourceWorkflowModule = v.SourceModule,
                SourceWorkflowCategory = v.SourceCategory,
                DefinitionJson = v.DefinitionJson,
                CaptionEn = v.CaptionEn,
                CaptionFa = v.CaptionFa,
                LegendEn = v.LegendEn,
                LegendFa = v.LegendFa,
                AccessibilityDescriptionEn = v.AccessEn,
                AccessibilityDescriptionFa = v.AccessFa,
                CreatedDate = DateTime.Now
            });
        }

        await db.SaveChangesAsync();
    }

    private static List<V> BuildVisuals() => new()
    {
        // ===================== Workflow-sourced (live, from WorkflowDefinitions) =====================
        new V("document-workflow", "WorkflowDiagram", 0,
            "EngineeringDocument", "Approval", null,
            "Engineering document approval workflow (generated from the live workflow configuration)",
            "گردش‌کار تایید مدرک مهندسی (تولیدشده از پیکربندی زنده‌ی گردش‌کار)",
            "Stages shown are the actual, currently-published stage/transition configuration - never a manually maintained copy.",
            "مراحل نمایش‌داده‌شده همان پیکربندی واقعیِ منتشرشده‌ی فعلیِ مراحل/گذارها هستند - هرگز یک نسخه‌ی دستیِ جداگانه نیست.",
            "Diagram of the engineering document approval workflow stages and the transitions between them, generated live from the workflow engine configuration.",
            "نموداری از مراحل گردش‌کار تایید مدرک مهندسی و گذارهای میان آن‌ها، تولیدشده به‌صورت زنده از پیکربندی موتور گردش‌کار."),

        new V("idc-inter-discipline-check", "WorkflowDiagram", 0,
            "EngineeringDocument", "Approval", null,
            "Where IDC sits within the overall document approval workflow",
            "جایگاه IDC در کل گردش‌کار تایید مدرک",
            "Same live workflow as the main document-workflow article - shown here focused on the IDC/Check stage.",
            "همان گردش‌کار زنده‌ی مقاله‌ی «گردش‌کار مدرک» - این‌جا با تمرکز روی مرحله‌ی IDC/Check نمایش داده شده.",
            "Diagram of the full document approval workflow, generated live from the workflow engine configuration, to show where the IDC/Check stage fits relative to Approval and Issue.",
            "نموداری از کل گردش‌کار تایید مدرک، تولیدشده به‌صورت زنده از پیکربندی موتور گردش‌کار، برای نشان‌دادن جایگاه مرحله‌ی IDC/Check نسبت به Approval و Issue."),

        new V("vdr-overview", "WorkflowDiagram", 0,
            "VendorDocument", "Review", null,
            "Vendor document review workflow (generated from the live workflow configuration)",
            "گردش‌کار بازبینی مدرک وندور (تولیدشده از پیکربندی زنده‌ی گردش‌کار)",
            "Terminal action codes (AP/NC/AC/CO/RE) are shown exactly as recorded in the system; their full expansion is not documented in the codebase and is intentionally not guessed here.",
            "کدهای اقدام پایانی (AP/NC/AC/CO/RE) دقیقاً همان‌گونه که در سیستم ثبت می‌شوند نمایش داده شده‌اند؛ بسط کامل آن‌ها در کد مستند نشده و عمداً این‌جا حدس زده نشده.",
            "Diagram of the vendor document review workflow stages and transitions, generated live from the workflow engine configuration.",
            "نموداری از مراحل و گذارهای گردش‌کار بازبینی مدرک وندور، تولیدشده به‌صورت زنده از پیکربندی موتور گردش‌کار."),

        new V("permits-overview", "WorkflowDiagram", 0,
            "Permit", "PermitApproval", null,
            "Permit approval workflow (generated from the live workflow configuration)",
            "گردش‌کار تایید مجوز کار (تولیدشده از پیکربندی زنده‌ی گردش‌کار)",
            null, null,
            "Diagram of the permit approval workflow stages (HSE Review, Maintenance Approval, Operations Approval) and the transitions between them, generated live from the workflow engine configuration.",
            "نموداری از مراحل گردش‌کار تایید مجوز کار (بازبینی HSE، تایید نگهداری‌وتعمیرات، تایید عملیات) و گذارهای میان آن‌ها، تولیدشده به‌صورت زنده از پیکربندی موتور گردش‌کار."),

        new V("work-order-details", "WorkflowDiagram", 0,
            "WorkOrder", "MultiDisciplineReview", null,
            "Work order multi-discipline review (generated from the live workflow configuration)",
            "بازبینی چند-دیسیپلینی دستور کار (تولیدشده از پیکربندی زنده‌ی گردش‌کار)",
            "This is a single, parallel review stage in the live configuration - not a multi-stage chain.",
            "این یک مرحله‌ی منفرد و موازی در پیکربندی زنده است - نه یک زنجیره‌ی چندمرحله‌ای.",
            "Diagram of the work order's discipline-review workflow stage, generated live from the workflow engine configuration.",
            "نموداری از مرحله‌ی بازبینی دیسیپلینیِ دستور کار، تولیدشده به‌صورت زنده از پیکربندی موتور گردش‌کار."),

        // ===================== Manual (grounded in verified service/controller code) =====================
        new V("work-request-vs-work-order", "LifecycleDiagram", 0,
            null, null, WorkRequestLifecycleJson(),
            "Full Work Request → Work Order status lifecycle (verified against the actual code)",
            "چرخه‌ی کامل وضعیت درخواست کار ← دستور کار (تاییدشده در برابر کد واقعی)",
            "Solid path = normal progress. \"Reject\"/\"Cancel\" branches only exist from the stages actually shown - verified against WorkRequestService's status-transition rules.",
            "مسیر اصلی = روند عادی. شاخه‌های «رد»/«لغو» فقط از همان مراحلی که نشان داده شده وجود دارند - در برابر قواعد گذار وضعیتِ WorkRequestService تاییدشده.",
            "Diagram showing the Work Request statuses Draft, Submitted, Under Review, Approved, Planned and Converted, the points where Reject or Cancel are allowed, and how a Converted Work Request becomes a Work Order that then moves through Open, Assigned, In Progress, Completed and Closed.",
            "نموداری که وضعیت‌های درخواست کار (Draft، Submitted، Under Review، Approved، Planned، Converted)، نقاطی که رد یا لغو مجاز است، و این‌که چگونه یک درخواست کارِ Converted به دستور کاری تبدیل می‌شود که سپس از Open، Assigned، In Progress، Completed و Closed عبور می‌کند را نشان می‌دهد."),

        new V("functional-location-vs-equipment", "EntityRelationship", 0,
            null, null, FunctionalLocationEquipmentJson(),
            "Functional Location ↔ Equipment relationship",
            "رابطه‌ی موقعیت فنی ↔ تجهیز",
            null, null,
            "Diagram showing that a Functional Location can optionally have an Equipment record installed at it, via Equipment's own optional link to a Functional Location - the Functional Location itself does not depend on which equipment is currently installed.",
            "نموداری که نشان می‌دهد یک موقعیت فنی می‌تواند به‌صورت اختیاری یک تجهیز نصب‌شده در خود داشته باشد، از طریق پیوند اختیاری خودِ تجهیز به موقعیت فنی - خودِ موقعیت فنی به این‌که کدام تجهیز فعلاً نصب است وابسته نیست."),

        new V("idc-inter-discipline-check", "StepGuide", 1,
            null, null, IdcStepGuideJson(),
            "How an IDC review reaches a decision",
            "چگونه بازبینی IDC به یک تصمیم می‌رسد",
            null, null,
            "Step by step: the document is submitted and disciplines are nominated for parallel review; each discipline independently records No Comment, Approve with Comments, or Revision Required; once every nominated discipline has responded, the document moves to the formal Approval stage; the approver reviews the consolidated IDC input and makes the real decision.",
            "گام‌به‌گام: مدرک ارسال می‌شود و دیسیپلین‌ها برای بازبینی موازی معرفی می‌شوند؛ هر دیسیپلین مستقلاً یکی از «بدون نظر»، «تایید همراه با نظر» یا «نیازمند اصلاح» را ثبت می‌کند؛ پس از پاسخ همه‌ی دیسیپلین‌های معرفی‌شده، مدرک به مرحله‌ی رسمی Approval می‌رود؛ تاییدکننده با دیدن مجموع نظرات IDC، تصمیم واقعی را می‌گیرد."),
    };

    private static string WorkRequestLifecycleJson()
    {
        var def = new PEMS.Application.DTOs.HelpVisualDefinition();
        void N(string id, string en, string fa) => def.Nodes.Add(new PEMS.Application.DTOs.HelpVisualNodeDto { Id = id, LabelEn = en, LabelFa = fa });
        void E(string from, string? to, string en, string fa, string kind) =>
            def.Edges.Add(new PEMS.Application.DTOs.HelpVisualEdgeDto { FromNodeId = from, ToNodeId = to, LabelEn = en, LabelFa = fa, Kind = kind });

        N("draft", "Draft", "پیش‌نویس");
        N("submitted", "Submitted", "ارسال‌شده");
        N("review", "Under Review", "در حال بازبینی");
        N("approved", "Approved", "تاییدشده");
        N("planned", "Planned", "برنامه‌ریزی‌شده");
        N("converted", "Converted", "تبدیل‌شده");
        N("rejected", "Rejected", "رد شده");
        N("cancelled", "Cancelled", "لغوشده");
        N("wo_open", "Work Order: Open", "دستور کار: باز");
        N("wo_assigned", "Assigned", "واگذارشده");
        N("wo_inprogress", "In Progress", "در حال انجام");
        N("wo_completed", "Completed", "تکمیل‌شده");
        N("wo_closed", "Closed", "بسته‌شده");

        E("draft", "submitted", "Submit", "ارسال", "normal");
        E("submitted", "review", "Start Review", "شروع بازبینی", "normal");
        E("review", "approved", "Approve", "تایید", "normal");
        E("approved", "planned", "Plan", "برنامه‌ریزی", "normal");
        E("planned", "converted", "Convert (only from Planned)", "تبدیل (فقط از Planned)", "normal");
        E("converted", "wo_open", "Work Order created", "دستور کار ایجاد شد", "normal");
        E("wo_open", "wo_assigned", "Assign", "واگذاری", "normal");
        E("wo_assigned", "wo_inprogress", "Start", "شروع", "normal");
        E("wo_inprogress", "wo_completed", "Complete", "تکمیل", "normal");
        E("wo_completed", "wo_closed", "Close", "بستن", "normal");

        E("review", "rejected", "Reject", "رد", "reject");
        E("approved", "rejected", "Reject", "رد", "reject");
        E("draft", "cancelled", "Cancel", "لغو", "terminal");
        E("submitted", "cancelled", "Cancel", "لغو", "terminal");
        E("review", "cancelled", "Cancel", "لغو", "terminal");
        E("approved", "cancelled", "Cancel", "لغو", "terminal");
        E("planned", "cancelled", "Cancel", "لغو", "terminal");

        return System.Text.Json.JsonSerializer.Serialize(def);
    }

    private static string FunctionalLocationEquipmentJson()
    {
        var def = new PEMS.Application.DTOs.HelpVisualDefinition();
        def.Nodes.Add(new PEMS.Application.DTOs.HelpVisualNodeDto { Id = "fl", LabelEn = "Functional Location", LabelFa = "موقعیت فنی" });
        def.Nodes.Add(new PEMS.Application.DTOs.HelpVisualNodeDto { Id = "eq", LabelEn = "Equipment", LabelFa = "تجهیز" });
        def.Edges.Add(new PEMS.Application.DTOs.HelpVisualEdgeDto
        {
            FromNodeId = "eq", ToNodeId = "fl",
            LabelEn = "optionally installed at (Equipment.FunctionalLocationId)",
            LabelFa = "به‌صورت اختیاری نصب‌شده در (Equipment.FunctionalLocationId)",
            Kind = "normal"
        });
        return System.Text.Json.JsonSerializer.Serialize(def);
    }

    private static string IdcStepGuideJson()
    {
        var def = new PEMS.Application.DTOs.HelpVisualDefinition();
        void N(string id, string en, string fa) => def.Nodes.Add(new PEMS.Application.DTOs.HelpVisualNodeDto { Id = id, LabelEn = en, LabelFa = fa });

        N("step1", "Document submitted; originating discipline nominates other disciplines to review in parallel", "مدرک ارسال می‌شود؛ دیسیپلین سازنده سایر دیسیپلین‌ها را برای بازبینی موازی معرفی می‌کند");
        N("step2", "Each nominated discipline independently records: No Comment, Approve with Comments, or Revision Required", "هر دیسیپلین معرفی‌شده مستقلاً ثبت می‌کند: بدون نظر، تایید همراه با نظر، یا نیازمند اصلاح");
        N("step3", "No single discipline's result sets the document to Rejected - once all have responded, the document moves to the Approval stage", "نتیجه‌ی هیچ دیسیپلینی به‌تنهایی مدرک را Rejected نمی‌کند - پس از پاسخ همه، مدرک به مرحله‌ی Approval می‌رود");
        N("step4", "The approver reviews the consolidated IDC input and makes the real decision: approve, reject, or return for revision", "تاییدکننده با دیدن مجموع نظرات IDC، تصمیم واقعی را می‌گیرد: تایید، رد، یا بازگشت برای اصلاح");

        return System.Text.Json.JsonSerializer.Serialize(def);
    }
}
