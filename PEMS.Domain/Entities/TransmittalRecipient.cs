using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق بخش ۷ ممیزی حاکمیت Transmittal: گیرندگان به‌صورت ساختاریافته (نه رشته‌ی آزاد
/// comma-separated یا JSON) ذخیره می‌شوند تا هم گزارش‌گیری واقعی ممکن باشد، هم به‌ازای هر
/// گیرنده بتوان ResponseRequired جدا تعیین کرد. فیلد آزاد Transmittal.To برای سازگاری با
/// نمایش/فیلترهای قدیمی نگه داشته شده و از روی همین گیرندگان خلاصه‌سازی می‌شود.
/// </summary>
public class TransmittalRecipient
{
    [Key]
    public int TransmittalRecipientId { get; set; }

    public int TransmittalId { get; set; }
    public Transmittal? Transmittal { get; set; }

    public string Name { get; set; } = string.Empty;

    // Internal, External, Vendor, Client
    public string RecipientType { get; set; } = "External";

    public string? Organization { get; set; }

    // طبق بخش ۵: می‌تواند مستقل از سطح کلی Transmittal.ResponseRequired برای یک گیرنده‌ی
    // خاص هم تنظیم شود (مثلاً فقط از یک گیرنده پاسخ لازم است، نه بقیه)
    public bool ResponseRequired { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
