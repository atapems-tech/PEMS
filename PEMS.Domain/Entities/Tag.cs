using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// برچسب دلخواه و قابل‌ساخت توسط کاربر (طبق نمونه‌ی کارتابل مرجع Pargar/Fonix) - برخلاف
/// میانبرهای دیسیپلین که ثابت و از پیش تعریف‌شده بودند، این‌ها را خود کاربران می‌سازند.
/// </summary>
public class Tag : BaseEntity
{
    [Key]
    public int TagId { get; set; }

    public string Name { get; set; } = string.Empty;

    // کد رنگ Bootstrap badge: primary, secondary, success, danger, warning, info, dark
    public string Color { get; set; } = "secondary";

    public int? CreatedById { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}
