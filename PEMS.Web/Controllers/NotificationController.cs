using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

public class NotificationController : Controller
{
    private readonly INotificationService _service;
    private readonly ICurrentUserService _currentUser;

    public NotificationController(INotificationService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Notifications";
        ViewData["Section"] = "Correspondence Management";

        if (_currentUser.UserId == null) return View(new List<PEMS.Application.DTOs.NotificationDto>());

        var items = await _service.GetForUserAsync(_currentUser.UserId.Value);
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Dropdown()
    {
        if (_currentUser.UserId == null) return PartialView("_NotificationDropdown", new List<PEMS.Application.DTOs.NotificationDto>());

        var items = await _service.GetForUserAsync(_currentUser.UserId.Value, unreadOnly: true);
        return PartialView("_NotificationDropdown", items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        await _service.MarkAsReadAsync(id);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        if (_currentUser.UserId != null)
            await _service.MarkAllAsReadAsync(_currentUser.UserId.Value);

        return RedirectToAction(nameof(Index));
    }
}
