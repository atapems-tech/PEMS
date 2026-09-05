using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// مدیریت برچسب‌های دلخواه (طبق نمونه‌ی کارتابل مرجع Pargar/Fonix) - قابل ساخت توسط کاربران،
/// نه فقط میانبرهای دیسیپلین ثابت.
/// </summary>
public class TagController : Controller
{
    private readonly ITagService _service;
    private readonly ICurrentUserService _currentUser;

    public TagController(ITagService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Tags";
        ViewData["Section"] = "Correspondence Management";

        var tags = await _service.GetAllAsync();
        return View(tags);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string color, string? returnUrl)
    {
        await _service.CreateAsync(name, color, _currentUser.UserId);
        return string.IsNullOrEmpty(returnUrl) ? RedirectToAction(nameof(Index)) : LocalRedirect(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int tagId)
    {
        await _service.DeleteAsync(tagId);
        return RedirectToAction(nameof(Index));
    }
}
