using Microsoft.AspNetCore.Mvc;

namespace PEMS.Web.Areas.Admin.Controllers;

[Area("Admin")]
public sealed class SettingsController : Controller
{
    public IActionResult Index()
        => RedirectToAction("Index", "Settings", new { area = "" });
}
