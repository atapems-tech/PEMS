using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PEMS.Web.Hubs;

/// <summary>
/// اعلان‌های لحظه‌ای بدون Refresh. هویت کاربر فقط از Context.User (کوکی احراز هویت) گرفته می‌شود -
/// هرگز از پارامتر ارسالی کلاینت (قبلاً JoinUserGroup(userId) هر کسی را عضو گروه دیگران می‌کرد).
/// عضویت گروه در OnConnectedAsync انجام می‌شود تا بعد از Reconnect خودکار هم برقرار بماند.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public static string GroupFor(int userId) => $"user-{userId}";

    public override async Task OnConnectedAsync()
    {
        await JoinUserGroup();
        await base.OnConnectedAsync();
    }

    // بدون پارامتر: سازگار با فراخوانی قدیمی کلاینت، ولی گروه همیشه از هویت احرازشده ساخته می‌شود
    public async Task JoinUserGroup()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(value, out var userId)) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId));
    }
}
