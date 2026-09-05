using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق CMMS-10f بند ۴ پلن: منبع واحد اینکه کدام کنترل‌های ایمنیِ از‌قبل‌ساخته‌شده (Isolation/
/// GasTest) برای هر نوع Permit در لحظه‌ی اجرا الزامی‌اند - نسخه‌ی کوچک و محدود، نه "موتور
/// تعیین Permit" کامل سند پایه (که در CMMS-10d عمداً خارج از محدوده اعلام شد). مقادیر Seed
/// پیش‌فرض‌های متعارف صنعتی هستند و منتظر تایید نهایی HSE.
/// </summary>
public class PermitExecutionRequirement : BaseEntity
{
    public int PermitExecutionRequirementId { get; set; }

    public string PermitType { get; set; } = string.Empty;

    public bool RequireIsolationVerified { get; set; }
    public bool RequireValidGasTest { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
