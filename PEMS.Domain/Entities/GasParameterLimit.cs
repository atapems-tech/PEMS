using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10d: منبع واحد حد پذیرش برای هر پارامتر گاز - داده‌ی قابل‌مدیریت در دیتابیس، نه
/// Hardcode در UI/کد. PermitType=null یعنی برای همه‌ی انواع Permit اعمال می‌شود.
/// مقادیر Seed اولیه، پیش‌فرض‌های متعارف صنعتی هستند و منتظر تایید نهایی HSE - نه یک قاعده‌ی
/// نهایی تایید‌شده (طبق تصمیم باز شماره ۲ پلن).
/// </summary>
public class GasParameterLimit : BaseEntity
{
    public int GasParameterLimitId { get; set; }

    public string Parameter { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }

    public string? PermitType { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
