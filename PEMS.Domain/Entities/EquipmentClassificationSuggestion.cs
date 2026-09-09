using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Phase 3 — Equipment Classification Completion» بند ۹/۱۰: صف بازبینی انسانی برای
/// طبقه‌بندی‌هایی که قطعیت لازم (EXACT/HIGH) برای اعمال خودکار را ندارند (MEDIUM/LOW) یا از AI
/// می‌آیند. هرگز خودش EquipmentClassId معتبر Equipment را تغییر نمی‌دهد - فقط پس از Accept یک
/// کاربر واقعی (از طریق Controller)، سرویس Equipment.EquipmentClassId را می‌نویسد؛ آن نوشتن هم
/// از طریق SaveChangesAsync موجود PEMSDbContext به‌صورت خودکار در AuditLog ثبت می‌شود (طبق بند
/// ۱۱: از یک AuditLog جدید/موازی استفاده نشد - AuditLog سراسری موجود پروژه از قبل این کار را
/// دقیقاً انجام می‌دهد).
/// </summary>
public class EquipmentClassificationSuggestion : BaseEntity
{
    public int EquipmentClassificationSuggestionId { get; set; }

    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public int SuggestedEquipmentClassId { get; set; }
    public EquipmentClass? SuggestedEquipmentClass { get; set; }

    // EXACT/HIGH هرگز این‌جا Suggestion نمی‌سازند (بند ۱۲: مستقیم اعمال می‌شوند) - این فیلد
    // همیشه MEDIUM/LOW یا (در آینده) AI است.
    public string Confidence { get; set; } = string.Empty;

    // RULE/AI - طبق بند ۵ (همان قرارداد Equipment.ClassificationSource، هرگز Enum جدید)
    public string Source { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    // PENDING/ACCEPTED/REJECTED
    public string Status { get; set; } = "PENDING";

    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
