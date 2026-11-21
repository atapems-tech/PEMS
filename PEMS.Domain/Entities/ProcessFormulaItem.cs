using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// قلم ماده اولیه یا ترکیب درون فرمول تولید فرآیندی (Process Formula Item).
/// نسبت مصرف بر حسب اندازه بچ مرجع فرمول محاسبه و کنترل می‌شود.
/// فیلد IsCriticalQC نشان‌دهنده لزوم گذراندن گیت کیفی و داشتن وضعیت Released برای بچ مصرفی است.
/// </summary>
public class ProcessFormulaItem : BaseEntity
{
    public int ProcessFormulaItemId { get; set; }

    public int ProcessFormulaId { get; set; }
    public ProcessFormula? ProcessFormula { get; set; }

    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    public decimal QuantityRatio { get; set; }

    public int? UomId { get; set; }
    public MaterialUOM? Uom { get; set; }

    public decimal ScrapTolerancePercent { get; set; } = 0m;

    public int SequenceOrder { get; set; } = 1;

    public bool IsCriticalQC { get; set; } = true;

    public string? PhaseName { get; set; }

    public string? AdditionInstructions { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
