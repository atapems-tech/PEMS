using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// پیشنهاد عملیاتی صادرشده از موتور محاسباتی MRP (Planning Recommendation).
/// بر اساس تصمیم مالک (OD-06)، موتور برنامه‌ریزی هرگز سفارش قطعی تولید یا خرید صادر نمی‌کند
/// بلکه خروجی آن به عنوان پیشنهاد (Recommendation) ثبت شده و نیازمند تایید مدیر تولید است.
/// </summary>
public class PlanningRecommendation : BaseEntity
{
    public int PlanningRecommendationId { get; set; }

    public int PlanVersionId { get; set; }
    public PlanVersion? PlanVersion { get; set; }

    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    // Produce, Purchase, Transfer
    public string RecommendationType { get; set; } = "Produce";

    public decimal RecommendedQuantity { get; set; }

    public int? UomId { get; set; }
    public MaterialUOM? Uom { get; set; }

    public DateTime RequiredDueDate { get; set; }

    public DateTime SuggestedReleaseDate { get; set; }

    public int? SuggestedWorkCenterId { get; set; }
    public ProductionWorkCenter? SuggestedWorkCenter { get; set; }

    public int? ProcessFormulaId { get; set; }
    public ProcessFormula? ProcessFormula { get; set; }

    // Pending, Approved, Rejected, Converted
    public string ActionStatus { get; set; } = "Pending";

    // در صورت تایید و تبدیل به سفارش تولید واقعی
    public int? ConvertedProductionOrderId { get; set; }
    public ProductionOrder? ConvertedProductionOrder { get; set; }

    public string? Rationale { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
