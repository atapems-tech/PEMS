namespace PEMS.Application.DTOs;

public class SimopsCandidateDto
{
    public int PermitId { get; set; }
    public string PermitNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public string PermitType { get; set; } = "";
    public string Status { get; set; } = "";
    public string? EquipmentTagNumber { get; set; }
    public string? FunctionalLocationName { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string MatchBasis { get; set; } = "";
    public bool TimeWindowKnown { get; set; }
    public bool TimeOverlapConfirmed { get; set; }
    public int? ExistingConflictId { get; set; }
    public string? ExistingConflictStatus { get; set; }
}

public class SimopsCandidateWorkOrderDto
{
    public int WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = "";
    public string? EquipmentTagNumber { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? CompletionDate { get; set; }
}

public class SimopsConflictListDto
{
    public int SimopsConflictId { get; set; }
    public int PermitAId { get; set; }
    public string PermitANumber { get; set; } = "";
    public int PermitBId { get; set; }
    public string PermitBNumber { get; set; } = "";
    public string ConflictBasis { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Status { get; set; } = "";
    public string? RaisedByName { get; set; }
    public string? AssessedByName { get; set; }
    public DateTime? AssessedDate { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? ResolutionNotes { get; set; }
    public string? AcceptedByName { get; set; }
    public DateTime? AcceptedDate { get; set; }
    public string? AcceptanceJustification { get; set; }
    public List<SimopsConflictStatusHistoryDto> StatusHistory { get; set; } = new();
}

public class SimopsConflictStatusHistoryDto
{
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string? ChangedByName { get; set; }
    public DateTime ChangedDate { get; set; }
    public string? Reason { get; set; }
}
