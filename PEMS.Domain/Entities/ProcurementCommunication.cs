using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// یک ورودی از تاریخچه‌ی گفتگوی خرید (Vendor Inquiry, Technical Clarification,
/// Engineering Question, Response) - طبق سند، همه‌ی این‌ها باید از یک صفحه قابل مشاهده باشند.
/// از یک Entity واحد با فیلد Type استفاده شده تا مدل ساده و قابل‌گسترش بماند.
/// ==============================================================================
/// طبق گزارش سخت‌افزاری نهایی Package Procurement (Gap ۲): این Entity گسترش داده شد، نه
/// جایگزین شد - همان اصل «بسط، نه بازنویسی» که کل این جلسه دنبال شده:
///   (۱) اکنون BaseEntity را ارث می‌برد تا AuditLog عمومی/RowVersion (که پیش از این جلسه
///       نداشت) روی این جدول هم اعمال شود.
///   (۲) واژگان Type اکنون شامل CommercialClarification/ContractualClarification هم می‌شود -
///       فیلد از قبل رشته‌ی آزاد بود، پس این فقط افزودن مقدار مجاز است، نه تغییر Schema.
///   (۳) TenderId اختیاری اضافه شد تا سوالات/شفاف‌سازی‌های مطرح‌شده در طول یک مناقصه‌ی خاص
///       قابل ردیابی باشند - Tender همچنان زیرمجموعه‌ی همان ProcurementPackage می‌ماند.
///   (۴) پاسخ دیگر جای‌گیر روی همین رکورد بازنویسی نمی‌شود - Responses زیر همین کلاس، تاریخچه‌ی
///       append-only واقعی نگه می‌دارد؛ فیلدهای ResponseText/RespondedByName/ResponseDate فقط
///       Cache آخرین پاسخ‌اند (دقیقاً همان الگوی Document/DocumentRevision).
/// ==============================================================================
/// </summary>
public class ProcurementCommunication : BaseEntity
{
    [Key]
    public int ProcurementCommunicationId { get; set; }

    public int ProcurementPackageId { get; set; }
    public ProcurementPackage? ProcurementPackage { get; set; }

    // اگر این گفتگو در طول یک مناقصه‌ی مشخص مطرح شده - اختیاری، Tender همچنان زیرمجموعه‌ی
    // همین Package است، این یک معماری جدا نیست
    public int? TenderId { get; set; }
    public Tender? Tender { get; set; }

    // VendorInquiry, TechnicalClarification, EngineeringQuestion, Response,
    // CommercialClarification, ContractualClarification
    public string Type { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    // اگر این ارتباط مربوط به یک Vendor خاص باشد (Vendor Inquiry معمولاً همین‌طور است)
    public int? VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public string? RaisedByName { get; set; }

    public DateTime RaisedDate { get; set; } = DateTime.Now;

    // فایل پیوست اختیاری (از همان جدول DocumentAttachment موجود استفاده می‌شود)
    public int? AttachmentId { get; set; }
    public DocumentAttachment? Attachment { get; set; }

    // Open, Answered, Closed - طبق قانون تفکیک Status/Action
    public string ActionStatus { get; set; } = "Open";

    // Cache آخرین پاسخ - مرجع حقیقت واقعی Responses زیر است (طبق Gap ۲)
    public string? ResponseText { get; set; }
    public string? RespondedByName { get; set; }
    public DateTime? ResponseDate { get; set; }

    public ICollection<ProcurementCommunicationResponse> Responses { get; set; } = new List<ProcurementCommunicationResponse>();
}
