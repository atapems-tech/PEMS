namespace PEMS.Application.DTOs;

/// <summary>
/// طبق CMMS-10a Execution Baseline: Core Permit Lifecycle - همون الگوی List/Detail/StatusHistory
/// که برای WorkRequest در CMMS-03 ساخته شد.
/// </summary>
public class PermitListDto
{
    public int PermitId { get; set; }
    public string PermitNumber { get; set; } = "";
    public string PermitType { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public string? EquipmentTagNumber { get; set; }
    public string? WorkOrderNumber { get; set; }
    public string? IssuerName { get; set; }
    public string? ReceiverName { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class PermitDetailDto
{
    public int PermitId { get; set; }
    public string PermitNumber { get; set; } = "";
    public string PermitType { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int? EquipmentId { get; set; }
    public string? EquipmentTagNumber { get; set; }
    public int? FunctionalLocationId { get; set; }
    public string? FunctionalLocationBreadcrumb { get; set; }
    public int? WorkOrderId { get; set; }
    public string? WorkOrderNumber { get; set; }
    public string RequestedByName { get; set; } = "";
    public string? IssuerName { get; set; }
    public string? ReceiverName { get; set; }
    public string? ReceiverPersonnelType { get; set; }
    public string? ReceiverVendorName { get; set; }
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public DateTime? ClosedDate { get; set; }
    public string? CancellationReason { get; set; }
    public string? RevocationReason { get; set; }
    public DateTime CreatedDate { get; set; }

    public List<PermitStatusHistoryDto> StatusHistory { get; set; } = new();
}

public class PermitStatusHistoryDto
{
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string? ChangedByName { get; set; }
    public DateTime ChangedDate { get; set; }
    public string? Reason { get; set; }
}
