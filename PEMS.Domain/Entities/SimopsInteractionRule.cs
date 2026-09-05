using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10e: منبع واحد شدت تعارض بر اساس جفت PermitType - داده‌ی قابل‌مدیریت در دیتابیس،
/// نه Hardcode در UI/کد (دقیقاً همون الگوی GasParameterLimit در CMMS-10d). مقادیر Seed اولیه
/// پیش‌فرض‌های متعارف صنعتی هستند و منتظر تایید نهایی HSE.
/// </summary>
public class SimopsInteractionRule : BaseEntity
{
    public int SimopsInteractionRuleId { get; set; }

    public string PermitTypeA { get; set; } = string.Empty;
    public string PermitTypeB { get; set; } = string.Empty;

    // Low, Medium, High
    public string Severity { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
