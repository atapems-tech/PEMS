namespace PEMS.Application.DTOs;

/// <summary>
/// مقادیر خام یک ردیف اکسل قبل از Resolve - طبق بند «Traceability» همون الگوی
/// FunctionalLocationImportRowPayload. ستون‌های Plant/Area/Unit اختیاری‌اند و فقط برای
/// اعتبارسنجی زمینه‌ای استفاده می‌شوند (بند «Optional contextual validation» سند وظیفه) -
/// اگر در فایل نباشند، این اعتبارسنجی به‌سادگی رد می‌شود.
/// </summary>
public class EquipmentFunctionalLocationAssignmentRowPayload
{
    public string EquipmentTagRaw { get; set; } = "";
    public string FunctionalLocationCodeRaw { get; set; } = "";
    public string? PlantRaw { get; set; }
    public string? AreaRaw { get; set; }
    public string? UnitRaw { get; set; }
}

public class EquipmentFunctionalLocationAssignmentRowDto
{
    public int EquipmentFunctionalLocationAssignmentRowId { get; set; }
    public int RowNumber { get; set; }
    public int SourceRowNumber { get; set; }

    public string EquipmentTag { get; set; } = "";
    public string FunctionalLocationCode { get; set; } = "";

    public int? ResolvedEquipmentId { get; set; }
    public string? EquipmentDescription { get; set; }

    public int? ResolvedFunctionalLocationId { get; set; }
    // طبق سند وظیفه: "FINISHING / 85A07" - نه فقط "85A07" - از IFunctionalLocationService.GetBreadcrumbAsync
    public string? CurrentFunctionalLocationBreadcrumb { get; set; }
    public string? ProposedFunctionalLocationBreadcrumb { get; set; }

    public string ValidationStatus { get; set; } = "Valid";
    public string? ValidationMessage { get; set; }

    public bool IncludeInCommit { get; set; } = true;

    // Exact, AlreadyAssigned, NoChange, UnresolvedEquipment, UnresolvedFunctionalLocation,
    // ConflictEquipment, ConflictFunctionalLocation, ValidationError
    public string Status { get; set; } = "";

    public bool ReplaceExistingAssignment { get; set; }
    public bool CommittedAssignment { get; set; }
}

public class EquipmentFunctionalLocationAssignmentBatchSummaryDto
{
    public int EquipmentFunctionalLocationAssignmentBatchId { get; set; }
    public string FileName { get; set; } = "";
    public DateTime ImportDate { get; set; }
    public string Status { get; set; } = "Staging";
    public string? ImportedByName { get; set; }

    public int TotalRows { get; set; }
    public int ExactCount { get; set; }
    public int AlreadyAssignedCount { get; set; }
    public int NoChangeCount { get; set; }
    public int UnresolvedCount { get; set; }
    public int ConflictCount { get; set; }
    public int ValidationErrorCount { get; set; }
}

public class EquipmentFunctionalLocationAssignmentBatchDto : EquipmentFunctionalLocationAssignmentBatchSummaryDto
{
    // طبق بند «Import Size and Performance» سند وظیفه: هزاران ردیف ممکن است - صفحه‌ی
    // بازبینی باید Server-Side صفحه‌بندی شود، نه رندر یک‌جای همه‌ی ردیف‌ها.
    public PagedResult<EquipmentFunctionalLocationAssignmentRowDto> Rows { get; set; } = new();
}

public class EquipmentFunctionalLocationAssignmentCommitResultDto
{
    public int Assigned { get; set; }
    public int Skipped { get; set; }
    public int Unresolved { get; set; }
    public int Conflicts { get; set; }
    public int ValidationErrors { get; set; }
}
