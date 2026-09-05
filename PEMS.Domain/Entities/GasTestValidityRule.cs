using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10d: منبع واحد مدت اعتبار زمانی تست گاز - PermitType=null یعنی پیش‌فرض/همه‌ی انواع.
/// یک تست گاز قبلی نباید به‌طور خودکار برای کل چرخه‌ی عمر Permit معتبر بماند (طبق سند پایه).
/// طبق تصمیم کاربر: مقدار پیش‌فرض ۳۰ دقیقه برای همه‌ی انواع Permit Seed می‌شود.
/// </summary>
public class GasTestValidityRule : BaseEntity
{
    public int GasTestValidityRuleId { get; set; }

    public string? PermitType { get; set; }

    public int ValidityMinutes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
