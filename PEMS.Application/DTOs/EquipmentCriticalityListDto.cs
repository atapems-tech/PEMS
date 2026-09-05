namespace PEMS.Application.DTOs;

public class EquipmentCriticalityListDto
{
    public int EquipmentCriticalityId { get; set; }
    public int EquipmentId { get; set; }
    public string? EquipmentTagNumber { get; set; }
    public string CriticalityClass { get; set; } = string.Empty;
    public string? RiskLevel { get; set; }
    public string? SafetyImpact { get; set; }
    public string? ProductionImpact { get; set; }
    public string? EnvironmentImpact { get; set; }
}
