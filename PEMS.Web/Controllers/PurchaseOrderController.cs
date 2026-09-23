using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق ممیزی Package Procurement Contract Finalization (§C/§H، Phase 7.5): PurchaseOrder
/// Entity/Service/DTO از قبل کامل بودند - این کنترلر همان اتصال گمشده است، نه یک معماری تازه.
/// </summary>
public class PurchaseOrderController : Controller
{
    private readonly IPurchaseOrderService _service;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUser;
    private readonly PEMSDbContext _context;

    public PurchaseOrderController(IPurchaseOrderService service, IPermissionService permissionService,
        ICurrentUserService currentUser, PEMSDbContext context)
    {
        _service = service;
        _permissionService = permissionService;
        _currentUser = currentUser;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "PurchaseOrder", "View"))
            return Forbid();

        ViewData["Title"] = "Purchase Orders";
        ViewData["Section"] = "Contract Management";

        var list = await _service.GetAllAsync();
        return View(list);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "PurchaseOrder", "View"))
            return Forbid();

        ViewData["Title"] = "Purchase Order Details";
        ViewData["Section"] = "Contract Management";

        var po = await _service.GetByIdAsync(id);
        if (po == null) return NotFound();
        return View(po);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "PurchaseOrder", "Create"))
            return Forbid();

        ViewData["Title"] = "New Purchase Order";
        ViewData["Section"] = "Contract Management";

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();
        ViewBag.Vendors = await _context.Vendors.Where(x => x.IsActive)
            .Select(x => new { x.VendorId, x.Name }).ToListAsync();
        ViewBag.Contracts = await _context.Contracts
            .Select(x => new { x.ContractId, x.ContractNo }).ToListAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string poNumber, int projectId, int vendorId, int? contractId,
        string? description, decimal? value)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "PurchaseOrder", "Create"))
            return Forbid();

        var id = await _service.CreateAsync(poNumber, projectId, vendorId, contractId, description, value);
        TempData["Message"] = "Purchase Order created";
        return RedirectToAction(nameof(Details), new { id });
    }
}
