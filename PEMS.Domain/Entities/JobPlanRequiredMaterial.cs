namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS Production Baseline v1 بند ۱۰.۳ (Required Materials روی Job Plan) - دقیقاً هم‌شکل
/// WorkOrderMaterial، ولی زمان برنامه‌ریزی. وقتی WorkOrder از این Job Plan ساخته می‌شود، این
/// ردیف‌ها روی WorkOrderMaterial همان WorkOrder کپی می‌شوند (Snapshot، نه Join زنده).
/// </summary>
public class JobPlanRequiredMaterial
{
    public int JobPlanRequiredMaterialId { get; set; }

    public int JobPlanId { get; set; }
    public JobPlan? JobPlan { get; set; }

    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    public decimal RequiredQuantity { get; set; }

    public int? UomId { get; set; }
    public MaterialUOM? Uom { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
