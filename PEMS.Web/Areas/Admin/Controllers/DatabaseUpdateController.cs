using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Identity;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// Simple UI over <see cref="IDatabaseUpdateService"/>: shows whether the database needs an install or an
/// update, lists pending migrations and past runs, and applies them on demand. All logic lives in the service.
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class DatabaseUpdateController : Controller
{
    private readonly IDatabaseUpdateService _updates;

    public DatabaseUpdateController(IDatabaseUpdateService updates)
    {
        _updates = updates;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Database Update";
        ViewData["Section"] = "Administration";
        return View(await _updates.GetStatusAsync(ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(bool backupConfirmed, CancellationToken ct)
    {
        if (!backupConfirmed)
        {
            TempData["Error"] = "Confirm that a database backup has been taken before applying.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _updates.ApplyAsync("Admin", User.Identity?.Name, ct);
        TempData[result.Succeeded ? "Message" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
