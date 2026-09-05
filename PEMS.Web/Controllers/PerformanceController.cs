using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PEMS.Web.Controllers;

[Authorize]
public class PerformanceController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
