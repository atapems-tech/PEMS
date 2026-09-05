namespace PEMS.Application.DTOs;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - P2): ردیف Grid صفحه‌ی
/// TableExtraction/Index - قبلاً کل Entity (DocumentAttachment) با Take(50) ثابت (بدون
/// Skip/صفحه‌بندی واقعی) برگردانده می‌شد. این DTO فقط همون ۴ ستونی است که View واقعاً نمایش می‌دهد.
/// </summary>
public class TableExtractionAttachmentListDto
{
    public int AttachmentId { get; set; }
    public string FileName { get; set; } = "";
    public string FileExtension { get; set; } = "";
    public DateTime UploadDate { get; set; }
    public string AIStatus { get; set; } = "";
}

/// <summary>
/// شکل ساده‌ی یک ردیف استخراج‌شده برای Stage کردن - عمداً از نوع AI (ExtractedTableRow در PEMS.AI)
/// جدا نگه داشته شده چون PEMS.Application نباید به PEMS.AI وابسته شود (طبق چینش لایه‌ها در پروژه).
/// Controller مسئول map کردن ExtractedTableRow به این DTO است.
/// </summary>
public class TableExtractionRowInput
{
    public string? TagNumber { get; set; }
    public string? Description { get; set; }
    public string? JunctionBox { get; set; }
    public string? Terminal { get; set; }
    public string? CableNo { get; set; }
    public string? MarshallingCabinet { get; set; }
    public string? MarshallingTerminal { get; set; }
    public string? SystemCabinet { get; set; }
    public string? IoCard { get; set; }
    public string? IoChannel { get; set; }
    public string? IoAddress { get; set; }
    public string? DcsTagNo { get; set; }
    public string? LoopNo { get; set; }
    public string? System { get; set; }
    public string? SignalClass { get; set; }
    public bool? SpareOrHotSpare { get; set; }
    public string? Remarks { get; set; }
    public Dictionary<string, string>? RawExtra { get; set; }
}
