using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» فاز ۱: داشبورد سراسری تولید -
/// سفارش‌های فعال، اجراهای فعال/معلق، خروجی/افت امروز، آخرین بچ‌ها. کنترلر نازک - همه‌ی منطق
/// در IProductionOrderService.GetDashboardAsync.
/// </summary>
public class ProductionController : Controller
{
    private readonly IProductionOrderService _orderService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public ProductionController(IProductionOrderService orderService, ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _orderService = orderService;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Dashboard()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "View"))
            return Forbid();

        ViewData["Title"] = "Production Dashboard";
        ViewData["Section"] = "Operations & Production";

        var dashboard = await _orderService.GetDashboardAsync();
        return View(dashboard);
    }
}
