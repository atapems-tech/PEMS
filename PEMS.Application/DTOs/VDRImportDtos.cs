namespace PEMS.Application.DTOs;

/// <summary>
/// ساختار کامل یک ردیف پارس‌شده از اکسل VDR، پیش از ذخیره؛ به‌صورت JSON در
/// VDRImportRow.PayloadJson نگه‌داری می‌شود تا نیازی به جدول ستون‌به‌ستون نباشد.
/// </summary>
public class VDRImportRowPayload
{
    public int SequenceNo { get; set; }
    public string? DisciplineCodeRaw { get; set; }
    public string? MRNumber { get; set; }
    public string? CommercialExpeditorName { get; set; }
    public string? EngineeringExpeditorName { get; set; }
    public string? VendorName { get; set; } = "";
    public string? FolderNoOnFtp { get; set; }
    public string? PoNumber { get; set; }
    public string? PackageDescription { get; set; }
    public string DocumentTypeCode { get; set; } = "";
    public string DocumentNumber { get; set; } = "";
    public string? DocumentDescription { get; set; }
    public string? ResponsiblePartyCode { get; set; }
    public string? DocumentClassRaw { get; set; }
    public string? WrapUpStatusImported { get; set; }
    public DateTime? PlannedFirstIssueDate { get; set; }

    public List<VDRImportSubmissionPayload> Submissions { get; set; } = new();
}

public class VDRImportSubmissionPayload
{
    public int RevisionNo { get; set; }
    public string? VendorTransmittalNo { get; set; }
    public string? ClientTransmittalNo { get; set; }
    public DateTime? VendorActualIssuedDate { get; set; }
    public string? PurposeOfIssueCode { get; set; }
    public string? ClientCommentSheetNo { get; set; }
    public DateTime? ClientCommentDate { get; set; }
    public string? ReviewCategory { get; set; }
    public string? ReplySheetNo { get; set; }
    public DateTime? ReplySheetDate { get; set; }
}

public class VDRImportRowDto
{
    public int VDRImportRowId { get; set; }
    public int RowNumber { get; set; }
    public string DocumentNumber { get; set; } = "";
    public string? VendorNameRaw { get; set; }
    public string ValidationStatus { get; set; } = "Valid";
    public string? ValidationMessage { get; set; }
    public bool IsDuplicate { get; set; }
    public bool IncludeInCommit { get; set; }
    public VDRImportRowPayload? Payload { get; set; }
}

public class VDRImportBatchDto
{
    public int VDRImportBatchId { get; set; }
    public string FileName { get; set; } = "";
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public DateTime ImportDate { get; set; }
    public int RowsTotal { get; set; }
    public int RowsValid { get; set; }
    public int RowsWarning { get; set; }
    public int RowsError { get; set; }
    public int RowsDuplicate { get; set; }
    public string Status { get; set; } = "";
    public List<VDRImportRowDto> Rows { get; set; } = new();
}
