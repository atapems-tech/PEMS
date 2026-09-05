namespace PEMS.Application.DTOs;

/// <summary>
/// طبق CMMS-10c: IsolationPoint همیشه زیرمجموعه‌ی یک Permit است - Index مستقل ندارد، همون
/// الگوی PermitStatusHistory.
/// </summary>
public class IsolationPointListDto
{
    public int IsolationPointId { get; set; }
    public int PermitId { get; set; }
    public string EnergyType { get; set; } = "";
    public string Location { get; set; } = "";
    public string? NormalState { get; set; }
    public string? RequiredIsolationState { get; set; }
    public string? IsolationMethod { get; set; }
    public string? LockNumber { get; set; }
    public string? TagNumber { get; set; }
    public string Status { get; set; } = "";
    public string? EquipmentTagNumber { get; set; }

    public string? IsolatedByName { get; set; }
    public DateTime? IsolatedDate { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public string? DeisolatedByName { get; set; }
    public DateTime? DeisolatedDate { get; set; }

    public List<IsolationStatusHistoryDto> StatusHistory { get; set; } = new();
}

public class IsolationStatusHistoryDto
{
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string? ChangedByName { get; set; }
    public DateTime ChangedDate { get; set; }
    public string? Reason { get; set; }
}
