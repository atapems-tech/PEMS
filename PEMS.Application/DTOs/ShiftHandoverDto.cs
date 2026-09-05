namespace PEMS.Application.DTOs;

public class ShiftHandoverListDto
{
    public int ShiftHandoverId { get; set; }
    public string? OutgoingName { get; set; }
    public string? IncomingName { get; set; }
    public string? ShiftLabel { get; set; }
    public DateTime HandoverDate { get; set; }
    public string Status { get; set; } = "";
    public DateTime? PreparedDate { get; set; }
    public DateTime? AcknowledgedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int ItemCount { get; set; }
}

public class ShiftHandoverDetailDto
{
    public int ShiftHandoverId { get; set; }
    public int OutgoingEmployeeId { get; set; }
    public string? OutgoingName { get; set; }
    public int IncomingEmployeeId { get; set; }
    public string? IncomingName { get; set; }
    public string? ShiftLabel { get; set; }
    public DateTime HandoverDate { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "";
    public DateTime? PreparedDate { get; set; }
    public DateTime? AcknowledgedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public List<ShiftHandoverItemDto> Items { get; set; } = new();
    public List<ShiftHandoverStatusHistoryDto> StatusHistory { get; set; } = new();
}

public class ShiftHandoverItemDto
{
    public string ItemType { get; set; } = "";
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string Description { get; set; } = "";
}

public class ShiftHandoverStatusHistoryDto
{
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string? ChangedByName { get; set; }
    public DateTime ChangedDate { get; set; }
    public string? Reason { get; set; }
}
