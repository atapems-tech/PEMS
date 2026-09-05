using Microsoft.AspNetCore.SignalR;
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

    public async Task BroadcastToUserAsync(int userId, string title, string? linkUrl)
    {
        await _hubContext.Clients.Group($"user-{userId}")
            .SendAsync("ReceiveNotification", title, linkUrl);
    }
}
