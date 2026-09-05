using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Controllers;

public class ContractController : Controller
{
    private readonly IContractService _service;
    private readonly PEMSDbContext _context;

    public ContractController(IContractService service, PEMSDbContext context)
    {
        _service = service;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Contracts";
        ViewData["Section"] = "Contract Management";

        var contracts = await _service.GetAllAsync();

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Vendors = await _context.Vendors.Where(x => x.IsActive)
            .Select(x => new { x.VendorId, x.Name }).ToListAsync();

        return View(contracts);
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "Contract Details";
        ViewData["Section"] = "Contract Management";

        var contract = await _service.GetByIdAsync(id);
        if (contract == null) return NotFound();
        return View(contract);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "New Contract";
        ViewData["Section"] = "Contract Management";

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Vendors = await _context.Vendors.Where(x => x.IsActive)
            .Select(x => new { x.VendorId, x.Name }).ToListAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string contractNo, int projectId, int vendorId, string? scope,
        decimal? value, DateTime? startDate, DateTime? endDate)
    {
        var id = await _service.CreateAsync(contractNo, projectId, vendorId, scope, value, startDate, endDate);
        TempData["Message"] = "Contract created successfully";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PEMS.Web.Security.RequirePermission("Contracts", "Approve")]
    public async Task<IActionResult> ChangeStatus(int contractId, string status)
    {
        await _service.ChangeStatusAsync(contractId, status);
        return RedirectToAction(nameof(Details), new { id = contractId });
    }
}
