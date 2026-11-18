using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» فاز ۱: کنترلر نازک - همه‌ی منطق
/// گذار وضعیت در IProductionOrderService (WorkOrderLifecycleService pattern). بررسی SoD روی
/// Release/Close دقیقاً هم‌الگو با DocumentWorkflowController.Approve.
/// </summary>
public class ProductionOrderController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly IProductionOrderService _orderService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly IAccessControlService _accessControlService;

    public ProductionOrderController(
        PEMSDbContext context, IProductionOrderService orderService,
        ICurrentUserService currentUser, IPermissionService permissionService, IAccessControlService accessControlService)
    {
        _context = context;
        _orderService = orderService;
        _currentUser = currentUser;
        _permissionService = permissionService;
        _accessControlService = accessControlService;
    }

    public async Task<IActionResult> Index(int page = 1, int? pageSize = null, string? status = null, string? search = null)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "View"))
            return Forbid();

        ViewData["Title"] = "Production Orders";
        ViewData["Section"] = "Operations & Production";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var result = await _orderService.GetListAsync(page, normalizedPageSize, status, search);

        ViewBag.StatusFilter = status;
        ViewBag.Search = search;
        return View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "View"))
            return Forbid();

        var order = await _orderService.GetByIdAsync(id);
        if (order == null) return NotFound();

        ViewData["Title"] = $"Production Order {order.OrderNumber}";
        ViewData["Section"] = "Operations & Production";

        return View(order);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Create"))
            return Forbid();

        ViewData["Title"] = "New Production Order";
        ViewData["Section"] = "Operations & Production";

        await PopulateDropdownsAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int materialItemId, decimal plannedQuantity, int? uomId, int productionWorkCenterId,
        DateTime? plannedStartDate, DateTime? plannedFinishDate, int? equipmentId,
        int? responsibleEmployeeId, string? notes)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "Production", "Create"))
            return Forbid();

        var employeeId = await GetCurrentEmployeeIdAsync();
        var (success, error, orderId) = await _orderService.CreateAsync(
            materialItemId, plannedQuantity, uomId, productionWorkCenterId,
            plannedStartDate, plannedFinishDate, equipmentId, responsibleEmployeeId, notes, employeeId);

        if (!success)
        {
            TempData["Error"] = error;
            await PopulateDropdownsAsync();
            return View();
        }

        TempData["Message"] = "سفارش تولید ایجاد شد.";
        return RedirectToAction(nameof(Details), new { id = orderId });
    }

    // SCRUM-437: Plan, Hold, Resume, Cancel, Release, Complete and Close are action definitions (production-order-*) over the ProductionOrder operations;
    // Release, Complete and Close carry the separation-of-duties access decision as the guard setting of operation.invoke.

    private async Task PopulateDropdownsAsync()
    {
        ViewBag.MaterialItems = await _context.MaterialItems.Where(x => x.IsActive)
            .OrderBy(x => x.MaterialCode)
            .Select(x => new { x.MaterialItemId, x.MaterialCode, x.Description, x.UomId }).ToListAsync();

        ViewBag.WorkCenters = await _context.ProductionWorkCenters.Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .Select(x => new { x.ProductionWorkCenterId, x.Code, x.Name, x.EquipmentId }).ToListAsync();

        ViewBag.Uoms = await _context.MaterialUOMs.Where(x => x.IsActive)
            .OrderBy(x => x.Code).Select(x => new { x.MaterialUOMId, x.Code }).ToListAsync();

        ViewBag.Equipments = await _context.Equipments.Where(x => x.IsActive)
            .OrderBy(x => x.TagNumber)
            .Select(x => new { x.EquipmentId, x.TagNumber, x.Name }).ToListAsync();

        ViewBag.Employees = await _context.Employees.Where(x => x.IsActive)
            .OrderBy(x => x.FirstName)
            .Select(x => new { x.EmployeeId, Name = x.FirstName + " " + x.LastName }).ToListAsync();
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }
}
