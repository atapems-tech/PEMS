using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Controllers;

/// <summary>
/// «کارهای من»: کارهای منتظر اقدام کاربر از کارتابل مشترک گردش‌کار + خلاصه‌ی اعلان‌ها/وظایف باز. هویت فقط از Principal سمت سرور
/// (ICurrentUserContext) گرفته می‌شود؛ هیچ employeeId/userId از ورودی پذیرفته نمی‌شود. این صفحه هیچ اقدامی روی گردش‌کار انجام نمی‌دهد
/// (فقط خواندن؛ اقدام در صفحه‌ی خود رکورد و با کنترل‌های موتور انجام می‌شود) و اعلان‌ها را جدا از کارهای قابل اقدام نگه می‌دارد.
/// </summary>
public class MyWorkController : Controller
{
    private readonly IMyWorkService _myWork;
    private readonly ICurrentUserContext _user;
    private readonly INotificationService _notifications;
    private readonly ITaskService _tasks;

    public MyWorkController(IMyWorkService myWork, ICurrentUserContext user, INotificationService notifications, ITaskService tasks)
    {
        _myWork = myWork;
        _user = user;
        _notifications = notifications;
        _tasks = tasks;
    }

    public async Task<IActionResult> Index(string? module, bool overdueOnly = false, bool personalOnly = false, int page = 1, int pageSize = 0)
    {
        ViewData["Title"] = "My Work";

        // کارمند ناشناخته (مثلاً SystemAdmin بدون پرونده‌ی کارمندی): فقط مراحل مبتنی بر نقش قابل نمایش‌اند؛ 0 با هیچ کاندیدایی تطابق ندارد
        var result = await _myWork.GetApprovalsAsync(_user.EmployeeId ?? 0, _user.Roles, module, overdueOnly, personalOnly, page, pageSize);

        if (_user.UserId.HasValue)
        {
            ViewBag.UnreadNotifications = await _notifications.GetUnreadCountAsync(_user.UserId.Value);
            ViewBag.OpenActionItems = await _tasks.GetOpenActionItemCountAsync(_user.UserId.Value);
        }

        return View(result);
    }
}
