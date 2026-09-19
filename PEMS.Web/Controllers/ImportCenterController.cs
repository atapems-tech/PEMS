using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// مرکز Import - فقط لایه‌ی کشف/ناوبری روی Importهای موجود (ImportCatalog). هیچ منطق Import،
/// آپلود، اعتبارسنجی یا نوشتنی این‌جا نیست؛ هر کارت به Controller/Action خودِ همان ماژول لینک
/// می‌دهد که مجوز خودش را مستقل بررسی می‌کند. نمایش کارت‌ها علاوه بر نقش، مجوز View ماژول را هم
/// (برای Importهای دارای ماژول مجوز) می‌پرسد تا لینک مرده/نمایان‌شدنِ بیش از قبل نداشته باشیم.
/// </summary>
public class ImportCenterController : Controller
{
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUser;

    public ImportCenterController(IPermissionService permissionService, ICurrentUserService currentUser)
    {
        _permissionService = permissionService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Import Center";

        var visible = await ImportCatalog.VisibleAsync(User, _permissionService, _currentUser.Roles);
        return View(visible);
    }
}
