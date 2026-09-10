using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Phase 7.6 — Engineering Criticality Assessment & Controlled Maintenance
/// Activation»: ممیزی این فاز نشان داد EquipmentCriticality موجود (از فاز ۶B) هیچ گردش‌کار
/// (DRAFT/UNDER_REVIEW/APPROVED)، شواهد ساختاریافته، نسخه‌ی روش‌شناسی، یا تایید انسانی مستقل
/// از سازنده ندارد - فقط یک رکورد تک-فیلدی است که هربار Assess زده می‌شود، غیرفعال+جایگزین
/// می‌شود. طبق بند ۱۰: به‌جای بازتفسیر/شکستن آن Entity ساده (که خودِ WorkOrderReleaseGateService/
/// EquipmentReadinessService هنوز به آن وابسته‌اند)، این Entity جدید و مستقل فقط لایه‌ی
/// «گردش‌کار مهندسی + شواهد + نسخه‌ی روش‌شناسی» را اضافه می‌کند؛ EquipmentCriticality همچنان
/// «مقدار فعلیِ معتبر» باقی می‌ماند و فقط در لحظه‌ی Approve این‌جا (نه خودکار در جای دیگر)
/// به‌روزرسانی می‌شود - دقیقاً همان الگوی RcmOperationalBridgeService فاز ۶.۱ (توصیه‌ی
/// تاییدشده → انتشار در جدول عملیاتی موجود، نه بازسازی آن).
///
/// ساختار گردش‌کار عیناً از FMEAAnalysis (فاز ۴) کپی شده - همان پنج‌حالتی که از قبل در این
/// پروژه تثبیت و تست شده (DRAFT→UNDER_REVIEW→APPROVED→SUPERSEDED، با PreparedBy/ReviewedBy/
/// ApprovedBy/ApprovedAt و SupersededByAssessmentId خودارجاع) - نه یک گردش‌کار جدید اختراعی.
/// </summary>
public class CriticalityAssessment : BaseEntity
{
    public int CriticalityAssessmentId { get; set; }

    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    // طبق بند ۷/۸: روش‌شناسی این‌جا «پروژه‌ای» است (نه ادعای انطباق با ISO/API) - نسخه‌بندی
    // صریح به‌جای یک جدول پیکربندی‌پذیر جداگانه (دقیقاً همان تصمیم فاز ۴ برای S/O/D FMEA:
    // ثابت و مستند، نه یک موتور پیکربندی جدید).
    public string FrameworkVersion { get; set; } = CriticalityFramework.CurrentVersion;

    // DRAFT, UNDER_REVIEW, APPROVED, SUPERSEDED
    public string Status { get; set; } = "DRAFT";

    public DateTime? AssessmentDate { get; set; }
    public string? PreparedBy { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public int? SupersededByAssessmentId { get; set; }
    public CriticalityAssessment? SupersededByAssessment { get; set; }

    // ==============================================================================
    // طبق بند ۹: این سه بُعد عیناً همان سه بُعدی هستند که EquipmentCriticality موجود از قبل
    // (به‌صورت متن آزاد) ثبت می‌کند (SafetyImpact/ProductionImpact/EnvironmentImpact) - هیچ
    // بُعد جدیدی (Quality/Regulatory/Redundancy) بدون الزام واقعی پروژه اضافه نشد (بند ۸).
    // مقیاس ۱ تا ۵ (Negligible..Severe)، Null یعنی «هنوز امتیاز داده نشده» - هرگز صفر جعلی.
    // ==============================================================================
    public int? SafetyScore { get; set; }
    public int? ProductionScore { get; set; }
    public int? EnvironmentScore { get; set; }

    public string? Rationale { get; set; }

    // طبق بند ۲۶: چرا این تصمیم گرفته شد - برای Supersession/Audit
    public string? ReasonForChange { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<CriticalityAssessmentEvidence> Evidence { get; set; } = new List<CriticalityAssessmentEvidence>();

    // طبق بند ۲۶: هرگز ذخیره نمی‌شود - همیشه از روی امتیازهای فعلی مشتق می‌شود (دقیقاً همان
    // الگوی FMEAItem.RiskPriorityNumber فاز ۴: محاسبه‌شده، نه Stale).
    public int? CalculatedMaxScore => (SafetyScore.HasValue && ProductionScore.HasValue && EnvironmentScore.HasValue)
        ? Math.Max(SafetyScore.Value, Math.Max(ProductionScore.Value, EnvironmentScore.Value))
        : null;

    // طبق بند ۷: آستانه‌های صریح و مستندشده (بند ۹ فایل CriticalityFramework) - نه یک فرمول پنهان
    public string? ProposedCriticalityClass => CriticalityFramework.ClassFromMaxScore(CalculatedMaxScore);
}

/// <summary>
/// طبق بند ۱۲: شواهد ساختاریافته که یک ارزیابی را قابل‌ردیابی می‌کنند - هرگز جعل نمی‌شوند، فقط
/// به داده‌ی واقعیِ از قبل موجود در PEMS (یا مدرک مهندسی بیرونی با شماره‌ی مرجع) اشاره می‌کنند.
/// طبق ممیزی: هیچ الگوی عمومیِ «EntityType+EntityId» در این پروژه (به‌جز AuditLog، که مخصوص
/// چارچوب Audit است) وجود ندارد - این‌جا از همان قرارداد FK صریح باقی پروژه (مثل
/// DocumentComment.DocumentId) پیروی می‌شود؛ SourceIdentifier/Reference متن آزاد برای اشاره
/// به مدرک/تجهیز/کلاس مرتبط است (نه یک FK چندریختی جدید).
/// </summary>
public class CriticalityAssessmentEvidence
{
    public int CriticalityAssessmentEvidenceId { get; set; }

    public int CriticalityAssessmentId { get; set; }
    public CriticalityAssessment? CriticalityAssessment { get; set; }

    // Equipment, EquipmentClass, EngineeringDocument, PID, Datasheet, ProjectRecord, FailureTaxonomy, MaintenanceRecord, Other
    public string SourceType { get; set; } = "Other";
    public string? SourceIdentifier { get; set; }
    public string? Reference { get; set; }
    public string Description { get; set; } = string.Empty;

    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}

/// <summary>
/// طبق بند ۷/۸: «روش‌شناسی پروژه‌ای متاثر از رویه‌های مهندسی ریسک/قابلیت‌اطمینان رایج» - هرگز
/// ادعای انطباق با یک استاندارد بیرونی مشخص (ISO/API) نمی‌شود چون این پروژه هیچ مدرکی برای آن
/// ادعا ندارد. قاعده‌ی «بدترین بُعد حاکم است» (Max، نه میانگین) یک اصل رایج و قابل‌دفاع در
/// عملکرد ارزیابی بحرانی‌بودن تجهیز است: یک تجهیز با پیامد ایمنی شدید، صرفاً به این دلیل که
/// پیامد تولید/محیط‌زیستی آن پایین است، نباید کم‌اهمیت جلوه کند.
/// </summary>
public static class CriticalityFramework
{
    public const string CurrentVersion = "PEMS-CRIT-1.0";

    public const int MinScore = 1;
    public const int MaxScore = 5;

    public static string? ClassFromMaxScore(int? maxScore) => maxScore switch
    {
        null => null,
        5 => "A",
        4 => "A",
        3 => "B",
        2 => "C",
        1 => "C",
        _ => null
    };
}
