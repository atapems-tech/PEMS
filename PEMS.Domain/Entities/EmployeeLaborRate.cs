using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند معماری Labor Cost: نرخ هزینه‌ی کار Employee به‌صورت تاریخچه‌دار، نه یک مقدار
/// ثابت روی خودِ Employee. هر بار نرخ عوض شود، رکورد قبلی با EffectiveTo بسته می‌شود و
/// رکورد جدید اضافه می‌شود - هرگز Overwrite نمی‌شود.
/// </summary>
public class EmployeeLaborRate : BaseEntity
{
    public int EmployeeLaborRateId { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    // Normal, Overtime
    public string RateType { get; set; } = "Normal";

    public decimal Rate { get; set; }

    public string Currency { get; set; } = "IRR";

    public DateTime EffectiveFrom { get; set; } = DateTime.Now;

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
