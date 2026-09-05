using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// Comment Management مشترک برای همه‌ی انواع مدرک (Engineering Document و Vendor Document/VDR)،
/// طبق Enterprise Document Core. جایگزین دو Entity قبلی EngineeringDocumentComment و VDRComment.
/// Action Status مستقل از Review Status خود مدرک پیگیری می‌شود.
/// VDRSubmissionId اختیاری است: اگر Comment مربوط به یک چرخه‌ی ارسال مشخص VDR باشد (نه کل مدرک)،
/// این فیلد پر می‌شود؛ برای مدارک مهندسی معمولی همیشه Null است.
/// </summary>
public class DocumentComment
{
    [Key]
    public int DocumentCommentId { get; set; }

    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public int? VDRSubmissionId { get; set; }
    public VDRSubmission? VDRSubmission { get; set; }

    // طبق استاندارد Comment Sheet واقعی (Technip/Saipem/Petrofac و مشابه): شماره‌ی یکتای
    // ترتیبی به‌ازای هر مدرک (C-001, C-002, ...)
    public string CommentNo { get; set; } = string.Empty;

    public string? Sheet { get; set; }
    public string? Zone { get; set; }
    public string? Reference { get; set; }

    // Technical, Safety, Constructability, Documentation, Specification, Calculation,
    // Typographical, Clarification
    public string? Category { get; set; }

    // Low, Medium, High, Critical
    public string? Severity { get; set; }

    public string? RequiredAction { get; set; }

    public DateTime? DueDate { get; set; }

    public string CommentText { get; set; } = string.Empty;

    public string? RaisedByName { get; set; }

    public DateTime RaisedDate { get; set; } = DateTime.Now;

    // NoActionRequired, OpenComment, VendorResponsePending / ResponsePending, Verified, Closed
    public string ActionStatus { get; set; } = "OpenComment";

    public int? AttachmentId { get; set; }
    public DocumentAttachment? Attachment { get; set; }

    public string? ResponseText { get; set; }
    public DateTime? ResponseDate { get; set; }

    // طبق درخواست: کاربر نخواهد همه‌چیز را دستی تایپ کند - می‌تواند به‌جایش خودِ فایل پاسخ
    // (Reply Sheet) را پیوست کند
    public int? ReplyAttachmentId { get; set; }
    public DocumentAttachment? ReplyAttachment { get; set; }

    public string? ClosedByName { get; set; }
    public DateTime? ClosedDate { get; set; }

    // ارتباط با کامنت مشابه در رویژن قبلی (اگر همان ایراد در رویژن جدید دوباره باز شد)
    public int? ParentCommentId { get; set; }
    public DocumentComment? ParentComment { get; set; }

    // طبق درخواست: Pin Comment روی PDF - شماره صفحه + مختصات نسبی (۰ تا ۱۰۰ درصد، مستقل از
    // اندازه‌ی نمایش) تا با هر Zoom/رزولوشنی درست جا بیفتد
    public int? PageNumber { get; set; }
    public double? PinX { get; set; }
    public double? PinY { get; set; }

    // ==============================================================================
    // طبق بخش M (بند ۱۶-۱۸ - Clouded/Marked-for-Revision) ممیزی: این سه مفهوم عمداً از هم
    // جدا نگه داشته شده‌اند، نه یک bool واحد:
    //   1) هویت رویژن مدرک        = Document.RevisionNo / DocumentRevision (بدون تغییر)
    //   2) نشانه‌گذاری بصری/Clouding = خارج از دامنه (این برنامه ابزار Markup روی نقشه ندارد)
    //   3) وضعیت کسب‌وکاری «نیاز به رویژن دارد» = دقیقاً همین دو فیلد زیر
    // ==============================================================================

    // این کامنت مدرک را به‌عنوان «نیازمند رویژن جدید» علامت می‌زند (Clouded/Marked-for-Revision)
    public bool MarksRevisionRequired { get; set; }

    // وقتی رویژن جدید واقعاً ساخته شد (از طریق "Create New Revision")، این‌جا وصل می‌شود -
    // پاسخ به «آیا رویژن جدید ساخته شد؟» را بدون گزارش جداگانه، مستقیم در دیتامدل نگه می‌دارد
    public int? TriggeredRevisionId { get; set; }
    public DocumentRevision? TriggeredRevision { get; set; }
}
