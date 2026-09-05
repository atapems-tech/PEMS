using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق سند Inventory بخش ۷: واحد اندازه‌گیری به‌عنوان Master Data مستقل - جلوگیری از
/// تکرار بی‌دلیل (مثلاً EA/Each/PCS/Piece نباید ۴ مفهوم مستقل باشند؛ داده‌ی خام MRS دقیقاً
/// همین ناسازگاری رو داشت: UNT/unit/UOM به‌عنوان نام ستون، LIT/LT به‌عنوان مقدار).
/// </summary>
public class MaterialUOM : BaseEntity
{
    public int MaterialUOMId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    // برای تبدیل واحد در آینده (مثلاً ROLL -> M) - اختیاری، فعلاً استفاده نمی‌شود
    public decimal? ConversionFactor { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
