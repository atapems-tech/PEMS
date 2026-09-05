using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Persistence.Identity;

namespace PEMS.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin + "," + SystemRoles.EngineeringManager)]
public sealed class EmployeeController : Controller
{
    public IActionResult Index()
        => RedirectToAction("Index", "Employee", new { area = "" });
}
