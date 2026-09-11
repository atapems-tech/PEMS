using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

public class DocumentAttachment
{
    [Key]
    public int AttachmentId { get; set; }


    public int? DocumentId { get; set; }

    // طبق «PEMS EDMS Deep Repository Audit» یافته F-06: قبلاً پیوست فقط به مدرک وصل می‌شد، نه به
    // یک رویژن مشخص - یعنی هیچ راهی برای تشخیص «فایل رسمی رویژن ۰۳» از «فایل رسمی رویژن ۰۵» در
    // یک مدرک با چند رویژن وجود نداشت. این فیلد کاملاً اختیاری است (رکوردهای قدیمی همچنان
    // DocumentRevisionId=null دارند و معتبرند - سازگاری کامل با گذشته).
    public int? DocumentRevisionId { get; set; }
    public DocumentRevision? DocumentRevision { get; set; }

    // آیا این فایل، فایل رسمی همان رویژن است (وقتی چند فایل برای یک رویژن آپلود می‌شود - مثلاً
    // Native + PDF Rendition + Marked-up - باید مشخص باشد کدام یکی مرجع رسمی است)
    public bool IsOfficial { get; set; }

    // Native, PdfRendition, MarkedUp, ReviewCopy, SupportingAttachment - طبق بند ۹ سند؛ آزاد
    // (نه Enum) چون این‌جا فقط برچسب توصیفی است، نه یک وضعیت گردش‌کاری قابل انتقال
    public string? FileRole { get; set; }

    // SHA-256 هگزادسیمال - طبق یافته F-07، حین Streaming آپلود محاسبه می‌شود (نه با خواندن دوباره‌ی
    // فایل). رکوردهای قدیمی این فیلد را خالی دارند - بازمحاسبه‌ی گذشته‌نگر عمداً انجام نشده (نیازمند
    // خواندن مجدد هر فایل از دیسک، خارج از محدوده و پرریسک).
    public string? FileHash { get; set; }

    // طبق بررسی قبلی: "چه کسی این فایل را آپلود کرد" اصلاً هیچ‌جا ذخیره نمی‌شد - پارامترش
    // در سرویس وجود داشت ولی نادیده گرفته می‌شد
    public int? UploadedById { get; set; }
    public Employee? UploadedBy { get; set; }

    public EngineeringDocument? Document { get; set; }


    // =========================
    // AI Intake Queue (طبق AI Engineering Intelligence Redesign)
    // =========================
    // Upload دیگر خودکار یک EngineeringDocument نمی‌سازد. فایل ابتدا این‌جا (بدون DocumentId)
    // می‌نشیند تا AI آن را دسته‌بندی کند و بعد به ماژول درست هدایت شود.

    public int? IntakeProjectId { get; set; }
    public Project? IntakeProject { get; set; }

    // Uploaded, Classifying, PendingEngineeringReview, PendingManualClassification, Routed, Rejected
    public string? IntakeStage { get; set; }

    // MDR, VDR, MR, MRQ, Contract, CMMS, Unrouted
    public string? RoutedModule { get; set; }

    public int? RoutedEntityId { get; set; }

    // پیوست نامه (Correspondence Module) - همان زیرساخت آپلود موجود، بدون کد جدید
    public int? LetterId { get; set; }
    public Letter? Letter { get; set; }

    // طبق درخواست: TQ/TSR/RFI و MOC واقعی همیشه با فرم پرشده/نقشه/برگه‌ی ارزیابی همراهند -
    // از همون زیرساخت پیوست موجود بازاستفاده می‌شود، نه جدول جدید
    public int? TechnicalQueryId { get; set; }
    public TechnicalQuery? TechnicalQuery { get; set; }

    public int? MOCId { get; set; }
    public MOC? MOC { get; set; }


    public string FileName { get; set; } = string.Empty;

    public string FileExtension { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string? MimeType { get; set; }

    public string AIStatus { get; set; } = "Pending";

    public string? AISummary { get; set; }

    public string? AIKeywords { get; set; }

    public string? AITags { get; set; }

    public double? AIConfidence { get; set; }

    public string? AIError { get; set; }

    public DateTime? AIProcessedDate { get; set; }

    public DateTime UploadDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;

    public ICollection<AIExtractedTag> ExtractedTags { get; set; } = new List<AIExtractedTag>();

    public ICollection<AIExtractedSpecification> ExtractedSpecifications { get; set; } = new List<AIExtractedSpecification>();
}