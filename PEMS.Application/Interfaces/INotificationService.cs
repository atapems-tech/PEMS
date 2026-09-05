using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// اعلان درون‌برنامه‌ای عمومی - قابل استفاده‌ی مجدد توسط هر ماژول (طبق اصل "استفاده از
/// سرویس‌های موجود"، این سرویس عمداً به‌گونه‌ای طراحی شد که فقط مخصوص Letter نباشد).
/// </summary>
public interface INotificationService
{
    Task CreateAsync(int userId, string title, string message, string? linkUrl = null,
        string? relatedType = null, int? relatedId = null);

    Task<List<NotificationDto>> GetForUserAsync(int userId, bool unreadOnly = false);

    Task<int> GetUnreadCountAsync(int userId);

    Task MarkAsReadAsync(int notificationId);

    Task MarkAllAsReadAsync(int userId);
}
