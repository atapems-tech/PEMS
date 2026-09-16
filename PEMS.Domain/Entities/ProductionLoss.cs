namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» بخش AG: افت/ضایعات تولید. دسته‌بندی
/// از یک فهرست کنترل‌شده‌ی ثابت می‌آید (اعتبارسنجی سمت سرور در ProductionRunService) - نه متن
/// آزاد کنترل‌نشده، طبق دستور صریح بند ۱۲ سند فاز.
/// </summary>
public class ProductionLoss
{
    public int ProductionLossId { get; set; }

    public int ProductionRunId { get; set; }
    public ProductionRun? ProductionRun { get; set; }

    public decimal Quantity { get; set; }

    public int? UomId { get; set; }
    public MaterialUOM? Uom { get; set; }

    // ProcessLoss, MaterialLoss, Reject, Waste, Rework, Other - فهرست کنترل‌شده،
    // ProductionLossCategories.All در لایه‌ی Application/Persistence اعتبارسنجی می‌شود
    public string LossCategory { get; set; } = string.Empty;

    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateTime TransactionDate { get; set; } = DateTime.Now;

    public string? Notes { get; set; }
}
