using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface INotificationBroadcaster
{
    /// <summary>اعلان جدید (با شناسه‌ی رکورد) + شمارنده‌ی نخوانده‌ی مرجع از دیتابیس.</summary>
    Task BroadcastNotificationAsync(int userId, NotificationDto notification, int unreadCount);

    /// <summary>همگام‌سازی شمارنده در همه‌ی تب‌های همان کاربر (Read/Dismiss/MarkAll).</summary>
    Task BroadcastUnreadCountAsync(int userId, int unreadCount);
}
