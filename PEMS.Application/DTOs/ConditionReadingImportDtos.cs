namespace PEMS.Application.DTOs;

/// <summary>
/// مقادیر خام یک ردیف اکسل قبل از Resolve - همون الگوی MaintenanceHistoryImportRowPayload.
/// نقطه‌ی پایش از ترکیب EquipmentTag + PointTag تشخیص داده می‌شود (نه یک ID خام در اکسل).
/// </summary>
public class ConditionReadingImportRowPayload
{
    public string EquipmentTagRaw { get; set; } = "";
    public string PointTagRaw { get; set; } = "";
    public string ReadingDateRaw { get; set; } = "";
    public string ValueRaw { get; set; } = "";
    public string? SourceRaw { get; set; }
    public string? NotesRaw { get; set; }
}

public class ConditionReadingImportRowDto
{
    public int ConditionReadingImportRowId { get; set; }
    public int RowNumber { get; set; }
    public int SourceRowNumber { get; set; }

    public string EquipmentTag { get; set; } = "";
    public string PointTag { get; set; } = "";
    public string? VariableType { get; set; }
    public DateTime? ReadingDate { get; set; }
    public decimal? Value { get; set; }
    public string? Source { get; set; }

    public string ValidationStatus { get; set; } = "Valid";
    public string? ValidationMessage { get; set; }

    public bool IncludeInCommit { get; set; } = true;

    // New, Duplicate, UnresolvedPoint, ConflictPoint, ValidationError
    public string Status { get; set; } = "";

    public int? DuplicateOfConditionReadingId { get; set; }
    public int? CommittedConditionReadingId { get; set; }
}

public class ConditionReadingImportBatchSummaryDto
{
    public int ConditionReadingImportBatchId { get; set; }
    public string FileName { get; set; } = "";
    public DateTime ImportDate { get; set; }
    public string Status { get; set; } = "Staging";
    public string? ImportedByName { get; set; }

    public int TotalRows { get; set; }
    public int NewCount { get; set; }
    public int DuplicateCount { get; set; }
    public int UnresolvedCount { get; set; }
    public int ConflictCount { get; set; }
    public int ValidationErrorCount { get; set; }
}

public class ConditionReadingImportBatchDto : ConditionReadingImportBatchSummaryDto
{
    public PagedResult<ConditionReadingImportRowDto> Rows { get; set; } = new();
}

public class ConditionReadingImportCommitResultDto
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Unresolved { get; set; }
    public int Conflicts { get; set; }
    public int ValidationErrors { get; set; }
}
