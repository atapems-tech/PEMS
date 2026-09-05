using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// صفحه‌ی «Customize Dashboard» - طبق بازبینی معماری (بند ۲): افزودن/حذف/تغییر ترتیب/تغییر
/// اندازه‌ی Widget های Dashboard شخصی هر کاربر.
/// </summary>
public class DashboardSettingsController : Controller
{
    private readonly IUserDashboardService _service;
    private readonly ICurrentUserService _currentUser;

    public DashboardSettingsController(IUserDashboardService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Customize Dashboard";
        ViewData["Section"] = "";

        if (_currentUser.UserId == null) return Forbid();

        var layout = await _service.GetLayoutAsync(_currentUser.UserId.Value);
        return View(layout);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(List<UserDashboardWidgetDto> widgets)
    {
        if (_currentUser.UserId == null) return Forbid();

        await _service.SaveLayoutAsync(_currentUser.UserId.Value, widgets);

        TempData["Message"] = "Dashboard preferences saved";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset()
    {
        if (_currentUser.UserId != null)
            await _service.ResetToDefaultAsync(_currentUser.UserId.Value);

        return RedirectToAction(nameof(Index));
    }
}
