namespace PEMS.Application.DTOs;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۵: زیرساخت نرم‌افزاری همیشه آماده است (FoundationStatus=READY) -
/// آنچه واقعاً به داده نیاز دارد LiveTelemetryStatus است که فقط با قرائت واقعی READY می‌شود.
/// </summary>
public class ConditionMonitoringFoundationStatusDto
{
    public string FoundationStatus { get; set; } = "READY";
    public string LiveTelemetryStatus { get; set; } = "NOT_READY";
    public int MonitoringPointCount { get; set; }
    public int ReadingCount { get; set; }
    public List<string> SupportedVariableTypes { get; set; } = new();
}

public class ConditionMonitoringPointDto
{
    public int ConditionMonitoringPointId { get; set; }
    public int EquipmentId { get; set; }
    public string? EquipmentTagNumber { get; set; }
    public string VariableType { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public decimal? AlarmThresholdLow { get; set; }
    public decimal? AlarmThresholdHigh { get; set; }
    public int ReadingCount { get; set; }
    public DateTime? LastReadingDate { get; set; }
    public decimal? LastValue { get; set; }
    public bool LastReadingIsAlarm { get; set; }
}
