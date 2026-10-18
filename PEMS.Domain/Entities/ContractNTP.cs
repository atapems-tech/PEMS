using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// اخطار شروع به کار (Notice to Proceed) - طبق ممیزی §C/§I (قاعده‌ی ۵): پیش از این هیچ
/// نمایندگی‌ای برای NTP وجود نداشت. گیت "NTP نباید پیش از تایید/امضای قرارداد صادر شود" در
/// ContractWorkflowService پیاده می‌شود (Contract.Status باید Signed یا Effective باشد)، نه اینجا.
/// نام این Entity عمداً "ContractNTP" است، نه "IRN" - طبق هشدار صریح ممیزی درباره‌ی برخورد نام با
/// IrnReceipt موجود در ماژول Material Management (Incoming Receipt Note).
/// </summary>
public class ContractNTP : BaseEntity
{
    [Key]
    public int ContractNTPId { get; set; }

    public int ContractId { get; set; }
    public Contract? Contract { get; set; }

    public string NTPNumber { get; set; } = string.Empty;

    public int? IssuedById { get; set; }
    public Employee? IssuedBy { get; set; }

    public DateTime IssuedDate { get; set; } = DateTime.Now;

    public DateTime? EffectiveStartDate { get; set; }

    // Issued, Acknowledged, Revoked
    public string Status { get; set; } = "Issued";

    public string? Reference { get; set; }

    // پیوست اختیاری - از همان جدول DocumentAttachment موجود
    public int? AttachmentId { get; set; }
    public DocumentAttachment? Attachment { get; set; }

    public bool IsActive { get; set; } = true;
}
