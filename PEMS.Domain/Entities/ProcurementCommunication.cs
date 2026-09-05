using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// یک ورودی از تاریخچه‌ی گفتگوی خرید (Vendor Inquiry, Technical Clarification,
/// Engineering Question, Response) - طبق سند، همه‌ی این‌ها باید از یک صفحه قابل مشاهده باشند.
/// از یک Entity واحد با فیلد Type استفاده شده تا مدل ساده و قابل‌گسترش بماند.
/// </summary>
public class ProcurementCommunication
{
    [Key]
    public int ProcurementCommunicationId { get; set; }

    public int ProcurementPackageId { get; set; }
    public ProcurementPackage? ProcurementPackage { get; set; }

    // VendorInquiry, TechnicalClarification, EngineeringQuestion, Response
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

    public string? ResponseText { get; set; }
    public string? RespondedByName { get; set; }
    public DateTime? ResponseDate { get; set; }
}
