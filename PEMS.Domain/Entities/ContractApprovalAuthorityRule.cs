using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق ممیزی Package Procurement Contract Finalization (§D/§I قاعده‌ی ۸، Phase 4.4): معماری
/// ماتریس تایید بر مبنای ارزش/بحرانی‌بودن قرارداد - عمداً بدون هیچ آستانه‌ی پولی از پیش Seed شده
/// (طبق دستور صریح: هیچ محدودیت مالی خاص شرکت اختراع نشود). تا وقتی هیچ ردیفی برای این جدول
/// پیکربندی نشده، ContractWorkflowService.ApproveAsync از همان گیت مجوز نقشی موجود استفاده
/// می‌کند (بدون تغییر رفتار) - این ماتریس فقط وقتی حداقل یک قاعده تعریف شده باشد فعال می‌شود، و
/// از آن نقطه به بعد برای هر ترکیب ارزش/بحرانی‌بودنی که هیچ قاعده‌ای با آن مطابقت ندارد،
/// Fail-Closed عمل می‌کند (رد صریح، نه تایید ضمنی).
/// </summary>
public class ContractApprovalAuthorityRule : BaseEntity
{
    [Key]
    public int ContractApprovalAuthorityRuleId { get; set; }

    // null = بدون کف
    public decimal? MinValue { get; set; }

    // null = بدون سقف
    public decimal? MaxValue { get; set; }

    // null = برای هر بحرانی‌بودنی اعمال می‌شود؛ در غیر این صورت باید با Contract.CriticalityClass
    // یکی باشد (همان واژگان EquipmentCriticality: A/B/C)
    public string? CriticalityClass { get; set; }

    // نقش سیستمی الزامی برای تایید نهایی این بازه (یکی از SystemRoles)
    public string RequiredApproverRole { get; set; } = string.Empty;

    public string? Description { get; set; }

    // طبق گزارش سخت‌افزاری نهایی (Gap ۳): وقتی چند قاعده‌ی فعال روی یک ترکیب ارزش/بحرانی‌بودن
    // مطابقت دارند، این عدد تعیین‌کننده است - کوچک‌تر یعنی اولویت بالاتر (اول بررسی می‌شود).
    // پیش از این جلسه هیچ فیلدی برای رفع این ابهام وجود نداشت.
    public int Priority { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
