using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// قلم برنامه‌ریزی‌شده در یک بازه زمانی مشخص (Plan Item).
/// مشخص‌کننده مقدار هدف تولید برای یک محصول یا خانواده محصول در یک دوره (ماهانه/هفتگی/روزانه) است.
/// فیلد IsFrozen تعیین می‌کند که آیا رکورد در منطقه منجمد (Frozen Zone) قرار دارد یا خیر.
/// </summary>
public class PlanItem : BaseEntity
{
    public int PlanItemId { get; set; }

    public int PlanVersionId { get; set; }
    public PlanVersion? PlanVersion { get; set; }

    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    public DateTime PeriodStartDate { get; set; }

    public DateTime PeriodEndDate { get; set; }

    public decimal PlannedQuantity { get; set; }

    public int? UomId { get; set; }
    public MaterialUOM? Uom { get; set; }

    public decimal ForecastQuantity { get; set; } = 0m;

    public decimal CustomerOrderQuantity { get; set; } = 0m;

    public decimal NetRequiredQuantity { get; set; } = 0m;

    public bool IsFrozen { get; set; } = false;

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
