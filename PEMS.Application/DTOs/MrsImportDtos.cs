namespace PEMS.Application.DTOs;

public class MrsImportBatchSummaryDto
{
    public int MrsImportBatchId { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public string? SourceSheet { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int NewCount { get; set; }
    public int MatchedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int InvalidCount { get; set; }
    public int ApprovedCount { get; set; }
    public DateTime ImportDate { get; set; }

    // طبق سند Metadata Extraction بخش ۶ و ۱۰: ردیابی تا فایل مبدأ واقعی + دیدنش در UI
    public int? SourceAttachmentId { get; set; }
    public string? SourceFileDownloadUrl { get; set; }
}

public class MrsImportRowDto
{
    public int MrsImportRowId { get; set; }
    public string? RawMrsNo { get; set; }
    public string? RawItemCode { get; set; }
    public string? RawSupplier { get; set; }
    public string? RawPoNo { get; set; }
    public string? RawPackingNo { get; set; }
    public string? RawDescription { get; set; }
    public string? RawUom { get; set; }
    public decimal? RawQtyReceived { get; set; }
    public decimal? RawQtyIssued { get; set; }
    public decimal? RawInventory { get; set; }
    public DateTime? RawDateReceived { get; set; }
    public string? RawMivNo { get; set; }
    public string? RawMrvNo { get; set; }
    public string? RawLocation { get; set; }
    public string? RawDiscipline { get; set; }
    public string? RawSize { get; set; }
    public string? RawEquipmentTag { get; set; }
    public string? ResolvedWarehouseCode { get; set; }
    public string MatchStatus { get; set; } = string.Empty;
    public string? MatchedMaterialCode { get; set; }
    public string? MatchedMaterialDescription { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    // طبق سند Metadata Extraction بخش ۱-۴: هر ستون واقعی این ردیف، شناخته‌شده یا نه
    public List<MrsImportRowMetadataDto> Metadata { get; set; } = new();
}

public class MrsImportRowMetadataDto
{
    public string OriginalHeader { get; set; } = string.Empty;
    public string? CanonicalField { get; set; }
    public string? Value { get; set; }
}

public class MrsImportBatchDetailDto
{
    public MrsImportBatchSummaryDto Batch { get; set; } = new();

    // طبق ممیزی «PEMS Import Center — Global 500-Row Batch Limitation Remediation»: قبلاً
    // Take(500) ثابت و بی‌صدا بود - ردیف‌های بعد از ۵۰۰ برای همیشه نامرئی و غیرقابل‌عمل می‌ماندند
    // (نه Commit، نه Reject ممکن بود). حالا از همون الگوی استاندارد PagedResult<T> استفاده
    // می‌شود که «PEMS — Global Data Grid Standard» برای هر Grid بزرگ الزامی کرده.
    public PagedResult<MrsImportRowDto> Rows { get; set; } = new();
}
