namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS Production Baseline v1 بند ۱۰.۳ (Required Skills روی Job Plan) - دقیقاً هم‌شکل
/// RequiredSkill (که به WorkOrder وصل است)، ولی این‌جا زمان برنامه‌ریزی است نه اجرا. وقتی از
/// یک Plan دارای این Job Plan یک WorkOrder ساخته می‌شود، این ردیف‌ها روی RequiredSkill همان
/// WorkOrder کپی می‌شوند (Snapshot، نه Join زنده).
/// </summary>
public class JobPlanRequiredSkill
{
    public int JobPlanRequiredSkillId { get; set; }

    public int JobPlanId { get; set; }
    public JobPlan? JobPlan { get; set; }

    public int SkillId { get; set; }
    public Skill? Skill { get; set; }

    public string? MinimumProficiencyLevel { get; set; }

    // طبق CMMS Production Baseline v1 بند ۱۱: هنگام ساخت WorkOrder از این Job Plan، روی
    // RequiredSkill.PlannedHours همان WorkOrder کپی می‌شود (نگاه کنید GenerateWorkOrder).
    public decimal? PlannedHours { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
