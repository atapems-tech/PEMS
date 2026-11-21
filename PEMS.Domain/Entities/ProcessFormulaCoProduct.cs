using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// محصول جانبی، محصول مشترک یا حلال قابل بازیابی ناشی از فرآیند تولید (Co-Product / By-Product).
/// درصد تخصیص بازده خروجی کل را بر روی محصولات جانبی مشخص می‌کند.
/// </summary>
public class ProcessFormulaCoProduct : BaseEntity
{
    public int ProcessFormulaCoProductId { get; set; }

    public int ProcessFormulaId { get; set; }
    public ProcessFormula? ProcessFormula { get; set; }

    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    public decimal AllocationPercent { get; set; }

    // CoProduct, ByProduct, RecycledSolvent, Waste
    public string Kind { get; set; } = "CoProduct";

    public int? UomId { get; set; }
    public MaterialUOM? Uom { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
