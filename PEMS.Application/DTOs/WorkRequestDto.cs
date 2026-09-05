namespace PEMS.Application.DTOs;

public class WorkRequestListDto
{
    public int WorkRequestId { get; set; }
    public string RequestNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public string RequestedByName { get; set; } = "";
    public DateTime RequestedDate { get; set; }
    public string? EquipmentTagNumber { get; set; }
}

/// <summary>
/// طبق CMMS Production Baseline v1 بند ۸: جزئیات کامل یک Work Request - شامل تاریخچه‌ی
/// تغییر وضعیت (Append-only) برای ردیابی کامل مسیر Draft تا Converted/Rejected/Cancelled.
/// </summary>
public class WorkRequestDetailDto
{
    public int WorkRequestId { get; set; }
    public string RequestNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string RequestedByName { get; set; } = "";
    public DateTime RequestedDate { get; set; }
    public int? EquipmentId { get; set; }
    public string? EquipmentTagNumber { get; set; }
    public int? AssetId { get; set; }
    public string? AssetNumber { get; set; }
    public int? FunctionalLocationId { get; set; }
    public string? FunctionalLocationBreadcrumb { get; set; }
    public string? ReportedCondition { get; set; }
    public string Priority { get; set; } = "";
    public string? EquipmentCriticalityClass { get; set; }
    public int? SuggestedDisciplineId { get; set; }
    public string? SuggestedDisciplineName { get; set; }
    public string Status { get; set; } = "";
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? RejectionReason { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public int? LinkedWorkOrderId { get; set; }
    public string? LinkedWorkOrderNumber { get; set; }

    public List<WorkRequestStatusHistoryDto> StatusHistory { get; set; } = new();
}

public class WorkRequestStatusHistoryDto
{
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string? ChangedByName { get; set; }
    public DateTime ChangedDate { get; set; }
    public string? Reason { get; set; }
}
