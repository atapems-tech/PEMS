using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// اعلان درون‌برنامه‌ای عمومی - طراحی‌شده برای استفاده‌ی مجدد توسط هر ماژول آینده، نه فقط
/// مکاتبات (طبق همان اصل "از سرویس‌های موجود استفاده کن" که در این ماژول رعایت شده).
/// </summary>
public class Notification : BaseEntity
{
    [Key]
    public int NotificationId { get; set; }

    public int UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    // مسیر داخلی برنامه برای کلیک روی اعلان (مثلاً /Letter/Details/12)
    public string? LinkUrl { get; set; }

    // Letter, LetterReferral, VDR, Document, ...
    public string? RelatedType { get; set; }

    public int? RelatedId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadDate { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
