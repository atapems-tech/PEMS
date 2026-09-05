using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;

namespace PEMS.Web.Controllers;

public class VendorController : Controller
{
    private readonly IVendorService _service;
    private readonly IVendorRiskService _riskService;

    public VendorController(IVendorService service, IVendorRiskService riskService)
    {
        _service = service;
        _riskService = riskService;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Vendors";
        ViewData["Section"] = "Contract Management";

        var vendors = await _service.GetAllAsync();
        return View(vendors);
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "Vendor Details";
        ViewData["Section"] = "Contract Management";

        var vendor = await _service.GetByIdAsync(id);
        if (vendor == null) return NotFound();
        return View(vendor);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "New Vendor";
        ViewData["Section"] = "Contract Management";
        return View(new Vendor());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Vendor vendor)
    {
        await _service.CreateAsync(vendor);
        TempData["Message"] = "Vendor created successfully";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "Edit Vendor";
        ViewData["Section"] = "Contract Management";

        var vendor = await _service.GetEntityByIdAsync(id);
        if (vendor == null) return NotFound();
        return View(vendor);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Vendor vendor)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edit Vendor";
            ViewData["Section"] = "Contract Management";
            return View(vendor);
        }

        await _service.UpdateAsync(vendor);
        TempData["Message"] = "Vendor updated successfully";
        return RedirectToAction(nameof(Details), new { id = vendor.VendorId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        await _service.ToggleActiveAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecalculateRisk(int id)
    {
        await _riskService.RecalculateAsync(id);
        TempData["Message"] = "Risk score recalculated";
        return RedirectToAction(nameof(Details), new { id });
    }
}
