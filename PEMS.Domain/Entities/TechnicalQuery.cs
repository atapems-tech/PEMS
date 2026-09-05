using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// ==============================================================================
/// TQ (Technical Query) / TSR (Technical Study Request) / RFI (Request For Information)
/// طبق فاز ۱ نقشه‌راه معماری - موجودیت مستقل با گردش‌کار اختصاصی، نه زیرمجموعه‌ی Letter.
/// یک جدول واحد با QueryType به‌جای سه جدول جدا، چون ساختار (ثبت → ارجاع به دیسیپلین →
/// پاسخ → تایید/بستن) بین هر سه یکسان است؛ فقط جهت/کاربردشان فرق دارد.
/// ==============================================================================
/// </summary>
public class TechnicalQuery : BaseEntity
{
    public int TechnicalQueryId { get; set; }

    public string QueryNumber { get; set; } = string.Empty;

    // TQ, TSR, RFI
    public string QueryType { get; set; } = "TQ";

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    // دیسیپلین مسئول پاسخ‌گویی
    public int DisciplineId { get; set; }
    public Discipline? Discipline { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }

    // اتصال اختیاری به مدرک مرتبط (چه مهندسی چه VDR - چون DocumentId کلید مشترک است)
    public int? RelatedDocumentId { get; set; }
    public Document? RelatedDocument { get; set; }

    public int? RelatedEquipmentId { get; set; }
    public Equipment? RelatedEquipment { get; set; }

    public int? RaisedById { get; set; }
    public DateTime RaisedDate { get; set; } = DateTime.Now;

    // طبق فاز ۱: رهگیری SLA - مهلت پاسخ
    public DateTime? RequiredResponseDate { get; set; }

    public string? ResponseText { get; set; }
    public int? RespondedById { get; set; }
    public DateTime? RespondedDate { get; set; }

    // Open, PendingConfirmation, Answered, Closed, Reopened
    public string Status { get; set; } = "Open";

    // Low, Normal, High, Urgent
    public string Priority { get; set; } = "Normal";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<DocumentAttachment> Attachments { get; set; } = new List<DocumentAttachment>();

    // طبق SLA: آیا از مهلت پاسخ گذشته و هنوز پاسخ داده نشده
    public bool IsOverdue =>
        Status is "Open" or "Reopened" &&
        RequiredResponseDate.HasValue &&
        RequiredResponseDate.Value < DateTime.Now;
}
