namespace PEMS.Application.DTOs;

/// <summary>
/// طبق CMMS-10d: GasTest همیشه زیرمجموعه‌ی یک Permit است - Index مستقل ندارد، همون الگوی
/// IsolationPointListDto.
/// </summary>
public class GasTestListDto
{
    public int GasTestId { get; set; }
    public int PermitId { get; set; }
    public string Location { get; set; } = "";
    public DateTime TestedAt { get; set; }
    public string? TesterName { get; set; }
    public string? InstrumentTag { get; set; }
    public DateTime? InstrumentCalibrationDueDate { get; set; }
    public string InstrumentCalibrationStatus { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? ValidUntil { get; set; }
    public bool IsExpired { get; set; }
    public string? InvalidationReason { get; set; }

    public List<GasReadingDto> Readings { get; set; } = new();
    public List<GasTestStatusHistoryDto> StatusHistory { get; set; } = new();
}

public class GasReadingDto
{
    public string Parameter { get; set; } = "";
    public decimal Value { get; set; }
    public string Unit { get; set; } = "";
    public string Result { get; set; } = "";
}

public class GasReadingInputDto
{
    public string Parameter { get; set; } = "";
    public decimal Value { get; set; }
    public string Unit { get; set; } = "";
}

public class GasTestStatusHistoryDto
{
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string? ChangedByName { get; set; }
    public DateTime ChangedDate { get; set; }
    public string? Reason { get; set; }
}
