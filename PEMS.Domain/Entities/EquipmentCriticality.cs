using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

public class EquipmentCriticality : BaseEntity
{
    [Key]
    public int EquipmentCriticalityId { get; set; }

    // ارتباط با Equipment
    public int EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    // کلاس بحرانی بودن
    // A, B, C
    public string CriticalityClass { get; set; } = string.Empty;

    // سطح ریسک
    // Low, Medium, High
    public string? RiskLevel { get; set; }

    // اثر روی ایمنی
    public string? SafetyImpact { get; set; }

    // اثر روی تولید
    public string? ProductionImpact { get; set; }

    // اثر زیست محیطی
    public string? EnvironmentImpact { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}