using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS — Enterprise Maintenance Master Data &amp; Standards Implementation» بند ۲/۱۰: فقط
/// ابرداده‌ی استانداردهای صنعتی (سازمان/شماره/عنوان/دسته/توضیح کاربردی) - هرگز متن دارای Copyright
/// خودِ استاندارد ذخیره نمی‌شود. این جدول صرفاً به‌عنوان مرجع طبقه‌بندی/AI-Ready برای
/// MaintenanceActivity استفاده می‌شود (مثلاً «این فعالیت طبق ISO 14224 دسته‌بندی شده»)، نه یک
/// کتابخانه‌ی اسناد.
/// </summary>
public class StandardReference : BaseEntity
{
    public int StandardReferenceId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Organization { get; set; } = string.Empty;

    public string StandardNumber { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? TitleFa { get; set; }

    public string? Category { get; set; }

    public string? Description { get; set; }

    public bool IsSystemDefault { get; set; }

    public bool IsActive { get; set; } = true;

    // طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» بند ۱.۳/۲۲:
    // یک استاندارد (مثلاً ISO 20816-1:2016) هرگز نباید «همیشگی» فرض شود - نسخه/بازه‌ی اعتبار باید
    // صریح و ردیابی‌پذیر باشد، نه فقط یک متن ثابت در کد. این پنج فیلد Additive روی همین جدول
    // موجود اضافه شدند (بدون Migration مخرب) - هیچ مصرف‌کننده‌ی موجود (JobPlan/PMTemplate/
    // MaintenanceMasterDataController) نیازی به تغییر ندارد.
    public string? Edition { get; set; }

    public DateTime? PublishedDate { get; set; }

    public DateTime? EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    // Current, Superseded, Withdrawn, UnderRevision
    public string Status { get; set; } = "Current";

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
