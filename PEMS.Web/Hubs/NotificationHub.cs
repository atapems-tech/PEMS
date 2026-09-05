using Microsoft.AspNetCore.SignalR;

namespace PEMS.Web.Hubs;

/// <summary>
/// طبق درخواست: اعلان‌های لحظه‌ای بدون نیاز به Refresh صفحه - روی همان سرور فعلی کار می‌کند،
/// نیاز به هیچ زیرساخت جدا (Redis Backplane و مشابه) در این مقیاس تک‌سازمانی ندارد.
/// هر کاربر با شناسه‌ی خودش عضو یک گروه می‌شود تا اعلان فقط برای همان کاربر ارسال شود.
/// </summary>
public class NotificationHub : Hub
{
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
    }
}
