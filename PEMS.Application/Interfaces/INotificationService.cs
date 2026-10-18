using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// اعلان درون‌برنامه‌ای عمومی - قابل استفاده‌ی مجدد توسط هر ماژول (طبق اصل "استفاده از
/// سرویس‌های موجود"، این سرویس عمداً به‌گونه‌ای طراحی شد که فقط مخصوص Letter نباشد).
/// چرخه‌ی عمر: Unread → Read → Dismissed/Resolved (DismissedDate؛ رکورد هرگز حذف نمی‌شود).
/// همه‌ی عملیات کاربرمحور، با userId احراز‌هویت‌شده - هرگز شناسه‌ی ارسالی از مرورگر.
/// </summary>
public interface INotificationService
{
    Task CreateAsync(int userId, string title, string message, string? linkUrl = null,
        string? relatedType = null, int? relatedId = null);

    /// <summary>فقط اعلان‌های فعال (Dismiss نشده)، حداکثر ۵۰ مورد.</summary>
    Task<List<NotificationDto>> GetForUserAsync(int userId, bool unreadOnly = false);

    /// <summary>filter: all (فعال) | unread | read | dismissed.</summary>
    Task<PagedResult<NotificationDto>> GetInboxAsync(int userId, string? filter, int page, int pageSize);

    /// <summary>شمارنده‌ی مرجع نخوانده‌ها (بدون Dismiss شده‌ها).</summary>
    Task<int> GetUnreadCountAsync(int userId);

    /// <summary>false اگر اعلان وجود ندارد یا متعلق به این کاربر نیست.</summary>
    Task<bool> MarkAsReadAsync(int userId, int notificationId);

    Task MarkAllAsReadAsync(int userId);

    /// <summary>Read + برگرداندن LinkUrl برای ناوبری؛ null اگر مالک نیست/وجود ندارد.</summary>
    Task<string?> OpenAsync(int userId, int notificationId);

    Task<bool> DismissAsync(int userId, int notificationId);

    Task DismissAllAsync(int userId);

    /// <summary>کاربر شیء مرتبط را باز کرد → اعلان‌های فعال مرتبط Read می‌شوند (Dismiss نمی‌شوند).</summary>
    Task MarkRelatedReadAsync(int userId, string relatedType, int relatedId);

    /// <summary>کار مرتبط انجام شد → اعلان‌های فعال مرتبط Read+Resolved. userId=null یعنی همه‌ی گیرندگان.</summary>
    Task ResolveRelatedAsync(int? userId, string relatedType, int relatedId);
}
