using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// نسخه برنامه‌ریزی تولید که طبق تصمیم معماری ADR-001 همان MPP (Master Production Plan) است.
/// یک نسخه تصویب‌شده MPP غیرقابل تغییر (Immutable) است و هرگونه بازنگری به عنوان نسخه جدید ایجاد می‌شود.
/// </summary>
public class PlanVersion : BaseEntity
{
    public int PlanVersionId { get; set; }

    public int ProductionPlanId { get; set; }
    public ProductionPlan? ProductionPlan { get; set; }

    public int VersionNumber { get; set; } = 1;

    // Draft, Published, Superseded, Frozen
    public string Status { get; set; } = "Draft";

    // Monthly, Weekly, Daily
    public string PlanningHorizon { get; set; } = "Monthly";

    public DateTime HorizonStartDate { get; set; }

    public DateTime HorizonEndDate { get; set; }

    public string? ChangeSummary { get; set; }

    public int? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<PlanItem> Items { get; set; } = new List<PlanItem>();

    public ICollection<PlanningRecommendation> Recommendations { get; set; } = new List<PlanningRecommendation>();
}
