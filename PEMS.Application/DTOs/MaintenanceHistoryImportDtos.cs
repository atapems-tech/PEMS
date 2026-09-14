namespace PEMS.Application.DTOs;

/// <summary>
/// مقادیر خام یک ردیف اکسل قبل از Resolve - طبق همون الگوی
/// EquipmentFunctionalLocationAssignmentRowPayload. WorkOrderNumber اختیاری است (طبق بند
/// «WorkOrder Soft Reference»).
/// </summary>
public class MaintenanceHistoryImportRowPayload
{
    public string EquipmentTagRaw { get; set; } = "";
    public string ExecutionDateRaw { get; set; } = "";
    public string? WorkOrderNumberRaw { get; set; }
    public string? FindingRaw { get; set; }
    public string? ActionTakenRaw { get; set; }
    public string? TechnicianRaw { get; set; }
    public string? DurationHoursRaw { get; set; }
    public string? CostRaw { get; set; }
    public string? DescriptionRaw { get; set; }
}

public class MaintenanceHistoryImportRowDto
{
    public int MaintenanceHistoryImportRowId { get; set; }
    public int RowNumber { get; set; }
    public int SourceRowNumber { get; set; }

    public string EquipmentTag { get; set; } = "";
    public string? EquipmentDescription { get; set; }
    public DateTime? ExecutionDate { get; set; }
    public string? WorkOrderNumber { get; set; }
    public string? ActionTaken { get; set; }
    public string? Technician { get; set; }

    public string ValidationStatus { get; set; } = "Valid";
    public string? ValidationMessage { get; set; }

    public bool IncludeInCommit { get; set; } = true;

    // New, Duplicate, UnresolvedEquipment, ConflictEquipment, ValidationError
    public string Status { get; set; } = "";

    public int? DuplicateOfMaintenanceHistoryId { get; set; }
    public int? CommittedMaintenanceHistoryId { get; set; }
}

public class MaintenanceHistoryImportBatchSummaryDto
{
    public int MaintenanceHistoryImportBatchId { get; set; }
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

public class MaintenanceHistoryImportBatchDto : MaintenanceHistoryImportBatchSummaryDto
{
    // طبق بند «Performance and Scale»: صفحه‌بندی Server-Side روی ردیف‌های همین Batch
    public PagedResult<MaintenanceHistoryImportRowDto> Rows { get; set; } = new();
}

public class MaintenanceHistoryImportCommitResultDto
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Unresolved { get; set; }
    public int Conflicts { get; set; }
    public int ValidationErrors { get; set; }
}
