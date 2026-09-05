namespace PEMS.Application.Interfaces;

/// <summary>
/// انتزاع پخش اعلان زنده (SignalR) - طبق معماری Clean Architecture، لایه‌ی Persistence نباید
/// مستقیم به PEMS.Web (جایی که Hub واقعی تعریف شده) وابسته باشد؛ این Interface اینجا تعریف
/// می‌شود و پیاده‌سازی واقعی‌اش در PEMS.Web است.
/// </summary>
public interface INotificationBroadcaster
{
    Task BroadcastToUserAsync(int userId, string title, string? linkUrl);
}
