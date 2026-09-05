using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// یک ردیف Staging از فایل اکسل MDR واقعی، پیش از تایید و ورود نهایی به EngineeringDocuments.
/// PayloadJson شامل تمام فیلدهای پارس‌شده است (طبق ساختار واقعی MDR: Area/Stage, Discipline,
/// Document Number, Description, Rev, POI, Transmittal, Comment No/Date/Status, Freeze).
/// </summary>
public class MDRImportRow
{
    [Key]
    public int MDRImportRowId { get; set; }

    public int MDRImportBatchId { get; set; }
    public MDRImportBatch? Batch { get; set; }

    public int RowNumber { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;

    public string? SheetName { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    // Valid, Warning, Error
    public string ValidationStatus { get; set; } = "Valid";

    public string? ValidationMessage { get; set; }

    public bool IsDuplicate { get; set; }

    public bool IncludeInCommit { get; set; } = true;

    public int? CreatedDocumentId { get; set; }
}
