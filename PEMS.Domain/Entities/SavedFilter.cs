using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// فیلتر ذخیره‌شده‌ی یک کاربر برای یک صفحه‌ی مشخص (طبق بند «Saved Filters» بازبینی معماری).
/// PageKey صفحه را مشخص می‌کند (مثلاً "DocumentRegister")؛ QueryString دقیقاً همان رشته‌ی
/// Query موجود در URL آن لحظه است، پس اعمال دوباره‌ی فیلتر فقط یک Redirect ساده است -
/// بدون نیاز به یک مدل پیچیده‌ی جدا به‌ازای هر نوع فیلتر.
/// </summary>
public class SavedFilter : BaseEntity
{
    [Key]
    public int SavedFilterId { get; set; }

    public int ApplicationUserId { get; set; }

    public string PageKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string QueryString { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
