using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// موجودیت ریشه برای طرح‌های تولید در سطوح کلان و تفصیلی (Production Plan).
/// شامل برنامه‌ریزی کلان خانواده محصولات (APP) و برنامه تفصیلی محصولات (MPS).
/// </summary>
public class ProductionPlan : BaseEntity
{
    public int ProductionPlanId { get; set; }

    public string PlanCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    // APP (Aggregate Production Planning) یا MPS (Master Production Schedule)
    public string PlanType { get; set; } = "APP";

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    // Draft, UnderReview, Approved, Closed
    public string Status { get; set; } = "Draft";

    public int? ActiveVersionId { get; set; }
    public PlanVersion? ActiveVersion { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<PlanVersion> Versions { get; set; } = new List<PlanVersion>();
}
