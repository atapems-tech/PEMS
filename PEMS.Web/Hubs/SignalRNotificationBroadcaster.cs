using Microsoft.AspNetCore.SignalR;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Hubs;

/// <summary>
/// پیاده‌سازی واقعی INotificationBroadcaster با استفاده از IHubContext - این کلاس در PEMS.Web
/// است (نه Persistence) تا وابستگی معماری درست‌جهت بماند.
/// </summary>
public class SignalRNotificationBroadcaster : INotificationBroadcaster
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotificationBroadcaster(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task BroadcastNotificationAsync(int userId, NotificationDto notification, int unreadCount)
    {
        await _hubContext.Clients.Group(NotificationHub.GroupFor(userId))
            .SendAsync("ReceiveNotification", new
            {
                notification.NotificationId,
                notification.Title,
                notification.Message,
                notification.LinkUrl,
                notification.RelatedType,
                notification.RelatedId,
                notification.CreatedDate,
                notification.IsRead
            }, unreadCount);
    }

    public async Task BroadcastUnreadCountAsync(int userId, int unreadCount)
    {
        await _hubContext.Clients.Group(NotificationHub.GroupFor(userId))
            .SendAsync("UnreadCountChanged", unreadCount);
    }
}
