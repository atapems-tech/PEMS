using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS — Enterprise Maintenance Master Data &amp; Standards Implementation» بند ۸/۱۷: طبقه‌ی
/// تجهیز (مثلاً "Centrifugal Pump") - جدا از Equipment.EquipmentType (رشته‌ی آزاد امروز، دست‌نخورده
/// می‌ماند برای Backward Compatibility با Equipment Import و همه‌ی کدهای موجود). این طبقه‌بندی
/// پایدار همان چیزی است که AI/Recommendation Engine آینده برای نگاشت Equipment → توصیه‌ی
/// Maintenance Activity به آن نیاز دارد (بند ۱۷: «Equipment → Equipment Class → Maintenance
/// Group → Maintenance Activity»). Equipment.EquipmentClassId (Nullable، پایین‌تر در
/// Equipment.cs) فقط یک ارتباط افزودنی/اختیاری است.
/// </summary>
public class EquipmentClass : BaseEntity
{
    public int EquipmentClassId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NameFa { get; set; }

    public string? Description { get; set; }

    public bool IsSystemDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
