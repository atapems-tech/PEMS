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

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
