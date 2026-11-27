namespace PEMS.Application.Interfaces;

/// <summary>One notification of the inbox page; <c>Status</c> is Unread, Read or Dismissed.</summary>
public sealed record NotificationRow(int NotificationId, string Title, string Message, string CreatedDate, string Status);

/// <summary>The answer of a notification action: the error is a resource key.</summary>
public sealed record NotificationActionResult(bool Success, string? ErrorKey);

/// <summary>The notification inbox page as operations (SCRUM-731): only the notifications of the signed-in user, never an id from the input for the user; the user is the context user name.</summary>
public sealed record NotificationCountAnswer(int Unread);

/// <summary>SCRUM-841: one line of the bell dropdown: the newest notifications of the signed-in user; OpenUrl marks it read and goes to its link (/x/notification-open).</summary>
public sealed record NotificationItemRow(int NotificationId, string Title, string Message, string Created, bool IsRead, string OpenUrl);

public interface INotificationOperations
{
    [PEMS.Application.Common.PemsOperation("Notification", "Inbox")]
    Task<List<NotificationRow>> InboxAsync(string userName, string? filter = null);

    [PEMS.Application.Common.PemsOperation("Notification", "MarkRead")]
    Task<NotificationActionResult> MarkReadAsync(string userName, int id);

    [PEMS.Application.Common.PemsOperation("Notification", "Dismiss")]
    Task<NotificationActionResult> DismissAsync(string userName, int id);

    [PEMS.Application.Common.PemsOperation("Notification", "MarkAllRead")]
    Task<NotificationActionResult> MarkAllReadAsync(string userName);

    [PEMS.Application.Common.PemsOperation("Notification", "DismissAll")]
    Task<NotificationActionResult> DismissAllAsync(string userName);

    /// <summary>SCRUM-831: the unread count of the signed-in user for the bell (served as JSON at /x/notification-unread-count: { unread }).</summary>
    [PEMS.Application.Common.PemsOperation("Notification", "UnreadCount")]
    Task<NotificationCountAnswer> UnreadCountAsync(string userName);

    /// <summary>SCRUM-841: the bell dropdown (the newest 15, served as JSON at /x/notification-dropdown).</summary>
    [PEMS.Application.Common.PemsOperation("Notification", "Recent")]
    Task<List<NotificationItemRow>> RecentAsync(string userName);
}
