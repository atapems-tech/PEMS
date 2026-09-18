using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Bilingual Help & Knowledge System»: گروه‌بندی سطح بالای مقالات راهنما
/// (General/Engineering/CMMS/Operations/Administration/Troubleshooting). عمداً خیلی ساده - فقط
/// یک Lookup برای مرور/فیلتر در Help Center، دقیقاً مثل MaterialCategory/EquipmentClass موجود.
/// </summary>
public class HelpCategory : BaseEntity
{
    public int HelpCategoryId { get; set; }

    // شناسه‌ی پایدار و بدون‌زبان - مثلاً "engineering"، "cmms" - در کد/Seed استفاده می‌شود
    public string Code { get; set; } = string.Empty;

    // نام نمایشی دوزبانه - چون تعداد دسته‌ها کم و ثابت است (طبق بخش ۴ سند)، بر خلاف مقالات
    // (که ترجمه‌ی جدا دارند)، این‌جا صرفاً دو فیلد ساده کافی است - Entity جدا اضافه نمی‌کند
    public string NameEn { get; set; } = string.Empty;
    public string NameFa { get; set; } = string.Empty;

    public string? IconCssClass { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
