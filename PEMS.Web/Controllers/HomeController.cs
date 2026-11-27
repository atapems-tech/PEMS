using Microsoft.AspNetCore.Mvc;
using PEMS.Web.Models;

namespace PEMS.Web.Controllers
{
    public class HomeController : Controller
    {
        // SCRUM-705: the dashboard is the definition page /p/home (IHomeOperations); the entry address only forwards to it
        public IActionResult Index() => Redirect("/p/home");
    }
}
