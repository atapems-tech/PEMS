namespace PEMS.Domain.Entities;

/// <summary>
/// طبق بخش ۱۷ سند: مهارت مورد نیاز برای انجام یک Work Order - چون Entity مستقل
/// MaintenanceTask در این پروژه وجود ندارد، مستقیماً به WorkOrder وصل می‌شود.
/// </summary>
public class RequiredSkill
{
    public int RequiredSkillId { get; set; }

    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public int SkillId { get; set; }
    public Skill? Skill { get; set; }

    public string? MinimumProficiencyLevel { get; set; }

    // طبق CMMS Production Baseline v1 بند ۱۱: «Planned Hours» - جدا از ساعت واقعی که در
    // WorkOrderLabor بعد از انجام کار ثبت می‌شود.
    public decimal? PlannedHours { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
