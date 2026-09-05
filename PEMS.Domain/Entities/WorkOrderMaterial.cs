namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند Inventory بخش ۱۱: متریال مورد نیاز یک WorkOrder (برنامه‌ریزی مصرف) - جدا از
/// Reservation/Transaction واقعی، مثل RequiredSkill برای WorkOrder+Skill.
/// </summary>
public class WorkOrderMaterial
{
    public int WorkOrderMaterialId { get; set; }

    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    public decimal RequiredQuantity { get; set; }

    public int? UomId { get; set; }
    public MaterialUOM? Uom { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
