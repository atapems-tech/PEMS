using PEMS.UI.Abstractions;

namespace PEMS.UI.Components;

// SCRUM-841: the list of the bell dropdown. The component writes the header, the list element and the footer; pems-notification-list.js reads the items (GET {Source}, a list of
// { notificationId, title, message, created, isRead, openUrl }) and fills the list from the notif-item templates. The mark-all and dismiss buttons are the ones notifications-hub.js already serves (data-notif-action).

/// <summary>The texts of the notification list (inputs, ADR-0008).</summary>
public sealed record NotificationListTexts
{
    private static TranslatableText T(string en, string fa) => TranslatableText.Of(("en", en), ("fa", fa));
    public TranslatableText Title { get; init; } = T("Notifications", "اعلان‌ها");
    public TranslatableText MarkAll { get; init; } = T("Mark all as read", "خواندن همه");
    public TranslatableText Dismiss { get; init; } = T("Dismiss", "حذف");
    public TranslatableText NoNew { get; init; } = T("No new notifications", "اعلان جدیدی نیست");
    public TranslatableText ViewAll { get; init; } = T("View all", "مشاهده‌ی همه");
    public TranslatableText Loading { get; init; } = T("Loading...", "در حال بارگذاری...");
    public TranslatableText Error { get; init; } = T("The notifications could not be loaded.", "بارگذاری اعلان‌ها ممکن نشد.");
}

/// <summary>Inputs of <see cref="PemsNotificationList"/>.</summary>
public sealed record NotificationListOptions : FrameOptions
{
    public string Source { get; init; } = "/x/notification-dropdown";
    public string ViewAllUrl { get; init; } = "/p/notifications";
    public NotificationListTexts Texts { get; init; } = new();
}
