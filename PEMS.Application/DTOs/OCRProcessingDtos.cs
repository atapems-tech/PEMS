namespace PEMS.Application.DTOs;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - P2): ردیف Grid صفحه‌ی
/// OCRProcessing/Index - قبلاً کل Entity (DocumentAttachment) با Include(Document) و Take(200)
/// ثابت (بدون Skip/صفحه‌بندی واقعی) برگردانده می‌شد. این DTO همون ستون‌هایی است که View واقعاً
/// نمایش می‌دهد - Include(Document) هم دیگر لازم نیست چون فقط DocumentNo از آن استفاده می‌شود.
/// </summary>
public class OCRProcessingAttachmentListDto
{
    public int AttachmentId { get; set; }
    public string FileName { get; set; } = "";
    public string? DocumentNo { get; set; }
    public string FileExtension { get; set; } = "";
    public long FileSize { get; set; }
    public string AIStatus { get; set; } = "";
    public string? AISummary { get; set; }
    public string? AIError { get; set; }
    public DateTime UploadDate { get; set; }
}
