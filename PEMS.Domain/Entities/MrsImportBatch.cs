namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند Inventory بخش ۲۰: خط لوله‌ی Import برای فایل‌های واقعی MRS - دقیقاً همان الگوی
/// Stage-سپس-تایید TableExtractionBatch/Row. هیچ ردیفی مستقیم در MaterialItem/InventoryTransaction
/// نمی‌نشیند؛ فقط بعد از تایید صریح کاربر در صفحه‌ی Review.
/// </summary>
public class MrsImportBatch
{
    public int MrsImportBatchId { get; set; }

    public string SourceFileName { get; set; } = string.Empty;

    public string? SourceSheet { get; set; }

    public int? ImportedById { get; set; }
    public Employee? ImportedBy { get; set; }

    public DateTime ImportDate { get; set; } = DateTime.Now;

    // طبق سند Metadata Extraction بخش ۶ (Do Not Lose Document Context): خودِ فایل Excel آپلودشده
    // به‌عنوان یک DocumentAttachment واقعی ذخیره می‌شود (نه فقط نام رشته‌ای SourceFileName) -
    // با همان زیرساخت آپلود موجود پروژه (نه جدول جدید).
    public int? SourceAttachmentId { get; set; }
    public DocumentAttachment? SourceAttachment { get; set; }

    public int TotalRows { get; set; }

    // Staged, InReview, Committed, Cancelled
    public string Status { get; set; } = "Staged";

    public ICollection<MrsImportRow> Rows { get; set; } = new List<MrsImportRow>();
}
