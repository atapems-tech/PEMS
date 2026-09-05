namespace PEMS.Application.DTOs;

/// <summary>
/// طبق CMMS Production Baseline v1 - CMMS-07 (Inspection): بازرسی دوره‌ای/کمی تجهیز
/// (Condition Monitoring) - جدا از بازرسی پذیرش ITP که در QCController مدیریت می‌شود.
/// </summary>
public class InspectionListItemDto
{
    public int InspectionRecordId { get; set; }
    public int EquipmentId { get; set; }
    public string TagNumber { get; set; } = string.Empty;

    public string InspectionType { get; set; } = string.Empty;
    public DateTime InspectionDate { get; set; }
    public string? Inspector { get; set; }

    public decimal? MeasurementValue { get; set; }
    public string? MeasurementUnit { get; set; }
    public decimal? LowerLimit { get; set; }
    public decimal? UpperLimit { get; set; }

    // Normal, Alert, Unknown (بدون آستانه‌ی تعریف‌شده) - محاسبه‌شده، ذخیره نمی‌شود
    public string ConditionStatus { get; set; } = "Unknown";

    public DateTime? NextInspectionDate { get; set; }

    // Overdue, DueSoon, OK, Unknown - همون منطق GetCalibrationStatus
    public string DueStatus { get; set; } = "Unknown";

    public string? Recommendation { get; set; }
    public string? Description { get; set; }
}
