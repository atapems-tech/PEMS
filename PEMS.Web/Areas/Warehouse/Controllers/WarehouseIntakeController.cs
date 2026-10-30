using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Areas.Warehouse.Controllers;

/// <summary>
/// SCRUM-472: warehouse intake of work-order materials. Thin controller: the storekeeper sees the stock lines of a work order,
/// picks the supplying warehouse (never pre-filled by inference) and sources the quantities; IWarehouseIntakeService raises the
/// linked Draft store requisition and completes the My Work task when nothing is left.
/// </summary>
[Area("Warehouse")]
public class WarehouseIntakeController : Controller
{
    private readonly IWarehouseIntakeService _service;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserService _currentUser;
    private readonly PEMSDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public WarehouseIntakeController(IWarehouseIntakeService service, IPermissionService permissions, ICurrentUserService currentUser,
        PEMSDbContext context, IStringLocalizer<SharedResource> localizer)
    {
        _service = service;
        _permissions = permissions;
        _currentUser = currentUser;
        _context = context;
        _localizer = localizer;
    }

    private Task<bool> CanViewAsync() => _permissions.HasPermissionAsync(_currentUser.Roles, "StoreRequisition", "View");

    /// <summary><paramref name="id"/> is the work order id (the business object of the warehouse task).</summary>
    public async Task<IActionResult> Details(int id)
    {
        if (!await CanViewAsync()) return Forbid();
        var intake = await _service.GetAsync(id);
        if (intake == null) return NotFound();

        ViewData["Title"] = _localizer["Whi_Title"].Value;
        ViewData["Section"] = "Inventory & Materials";
        ViewBag.CanSource = await _permissions.HasPermissionAsync(_currentUser.Roles, "StoreRequisition", "Create");
        ViewBag.Warehouses = await _context.Warehouses.AsNoTracking().Where(w => w.IsActive).OrderBy(w => w.Code)
            .Select(w => new { w.WarehouseId, w.Code, w.Name }).ToListAsync();
        return View(intake);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Source(int workOrderId, int warehouseId, Dictionary<int, decimal>? quantities)
    {
        var actor = new WarehouseIntakeActor(await GetCurrentEmployeeIdAsync(), _currentUser.Roles, _currentUser.UserName);
        var r = await _service.SourceAsync(workOrderId, warehouseId, quantities ?? new Dictionary<int, decimal>(), actor);
        if (!r.Success)
        {
            TempData["Error"] = _localizer[r.Error!].Value;
            return RedirectToAction(nameof(Details), new { id = workOrderId });
        }
        TempData["Message"] = _localizer[r.IntakeCompleted ? "Whi_SourcedAll" : "Whi_SourcedPart"].Value;
        return RedirectToAction("Details", "StoreRequisition", new { area = "Warehouse", id = r.StoreRequisitionId });
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;
        return await _context.Users.Where(u => u.Id == _currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync();
    }
}
