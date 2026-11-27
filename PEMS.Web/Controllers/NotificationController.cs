using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// همه‌ی اکشن‌ها فقط روی اعلان‌های کاربر احرازهویت‌شده کار می‌کنند؛ شناسه‌ی کاربر هرگز از ورودی
/// گرفته نمی‌شود. اعلانِ متعلق به دیگری مثل «وجود ندارد» رد می‌شود (404).
/// </summary>
public class NotificationController : Controller
{
    private readonly INotificationService _service;
    private readonly ICurrentUserService _currentUser;

    public NotificationController(INotificationService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Dropdown()
    {
        if (_currentUser.UserId == null) return PartialView("_NotificationDropdown", new List<NotificationDto>());

        var items = await _service.GetForUserAsync(_currentUser.UserId.Value);
        return PartialView("_NotificationDropdown", items.Take(15).ToList());
    }
}
