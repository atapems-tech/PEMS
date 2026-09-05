namespace PEMS.Application.DTOs;

/// <summary>
/// ردیف Grid استراتژی‌های نگهداری (RCM) - طبق سند «PEMS — Global Data Grid Standard»: فقط
/// فیلدهای نمایش‌داده‌شده، نه Include(Equipment) کامل.
/// </summary>
public class MaintenanceStrategyListDto
{
    public int MaintenanceStrategyId { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentTagNumber { get; set; } = "";
    public string StrategyType { get; set; } = "";
    public string? FailureModeCode { get; set; }
    public string ConsequenceCategory { get; set; } = "";
    public string? Justification { get; set; }
}
