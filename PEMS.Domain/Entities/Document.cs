using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// Enterprise Document Core: منبع واحد هویت برای هر نوع مدرکی در سازمان (طبق PEMS Development
/// Specification, بخش ۲ - "ONE central document entity"). EngineeringDocument و VDRItem (Vendor
/// Document) هر دو از این کلاس ارث می‌برند (EF Core Table-Per-Type) و PK مشترک دارند، پس یک
/// جستجو/گزارش روی جدول Documents می‌تواند هر دو نوع مدرک را با هم ببیند.
///
/// طراحی عمدی: فیلدهای کاملاً مشترک و قابل جستجو (شماره مدرک، عنوان، نوع، پروژه، دیسیپلین،
/// Area, Revision, Status, Comment Status, Freeze, تاریخ‌ها) این‌جا هستند. فیلدهای تخصصی هر نوع
/// (مثل IssuePurpose برای مدارک مهندسی، یا Vendor/PO برای مدارک Vendor) روی همان کلاس خودشان
/// می‌مانند تا کد و View های موجود بدون تغییر کار کنند.
/// </summary>
public class Document : BaseEntity
{
    [Key]
    public int DocumentId { get; set; }

    // Engineering, Vendor, Contract, Maintenance, Construction
    public string DocumentKind { get; set; } = "Engineering";

    public string DocumentNo { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    // Drawing, Datasheet, Procedure, Report, P&ID, DWG, DSH, PRC, RPT, ...
    public string DocumentType { get; set; } = string.Empty;

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int? DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    // Plant Area / Stage - طبق MDR واقعی (مثلاً "ABS & Common", "Rubber")
    public string? Area { get; set; }

    public string? RevisionNo { get; set; }

    // Review Status: Draft, Submitted, Under Review, Approved, Approved With Comments,
    // Rejected, Revise & Resubmit, Issued, Superseded, Archived ...
    public string Status { get; set; } = "Draft";

    // Comment Status جدا از Review Status طبق قانون تفکیک Status/Action:
    // W.F.C (Waiting For Comment), NC, AC, CO, AP, RE
    public string? CommentStatus { get; set; }

    // Freeze Status طبق MDR واقعی: مدرک منجمد شده و دیگر تغییر نمی‌کند. اجرای واقعی این محدودیت
    // (نه فقط نمایش Badge) در EngineeringDocumentRepository.UpdateAsync/FreezeAsync است - طبق
    // Phase E-01 رفع باگ ممیزی: قبلاً این پرچم صرفاً نمایشی بود و هیچ‌جا چک نمی‌شد.
    public bool IsFrozen { get; set; }
    public DateTime? FrozenDate { get; set; }
    public int? FrozenById { get; set; }
    public Employee? FrozenByEmployee { get; set; }
    public string? FreezeReason { get; set; }

    public DateTime? UnfrozenDate { get; set; }
    public int? UnfrozenById { get; set; }
    public Employee? UnfrozenByEmployee { get; set; }
    public string? UnfreezeReason { get; set; }

    // طبق بخش M (Hold/Clouded ممیزی): وضعیت فعلی محاسبه‌شده از آخرین ردیف باز DocumentHold
    // (تاریخچه‌ی کامل چند-چرخه‌ای در DocumentHold است، نه فقط این پرچم). مستقل از IsFrozen:
    // Hold یعنی «هنوز آماده‌ی ادامه نیست چون یک ورودی معلق دارد»، نه «قفل نهایی».
    public bool IsOnHold { get; set; }

    public ICollection<DocumentHold> Holds { get; set; } = new List<DocumentHold>();

    public DateTime? PlannedDate { get; set; }
    public DateTime? ActualDate { get; set; }

    // Delay (روز) - طبق ستون Delay در MDR واقعی: محاسبه‌شده، نه ذخیره‌شده
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int? DelayDays
    {
        get
        {
            if (PlannedDate == null) return null;

            var reference = ActualDate ?? (PlannedDate < DateTime.Now ? DateTime.Now : (DateTime?)null);

            if (reference == null) return null;

            return (int)(reference.Value - PlannedDate.Value).TotalDays;
        }
    }

    // AI Document Intelligence workflow stage (مشترک بین همه‌ی انواع مدرک):
    // Uploaded, AIProcessing, Classified, DisciplineAssigned, UnderReview, CommentOpen,
    // Revision, Approved, KnowledgeBase
    public string AIWorkflowStage { get; set; } = "Uploaded";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public DateTime? ModifiedDate { get; set; }


    // فرزندان مشترک بین همه‌ی انواع مدرک
    public ICollection<DocumentComment> Comments { get; set; } = new List<DocumentComment>();

    public ICollection<DocumentTagLink> TagLinks { get; set; } = new List<DocumentTagLink>();
}
