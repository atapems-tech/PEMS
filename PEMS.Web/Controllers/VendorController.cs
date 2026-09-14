using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

public class VendorController : Controller
{
    private readonly IVendorService _service;
    private readonly IVendorRiskService _riskService;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUser;

    public VendorController(IVendorService service, IVendorRiskService riskService,
        IPermissionService permissionService, ICurrentUserService currentUser)
    {
        _service = service;
        _riskService = riskService;
        _permissionService = permissionService;
        _currentUser = currentUser;
    }

    // طبق «PEMS Phase 2B»: بررسی مجوز مشترک برای اکشن‌های تغییردهنده - داده‌های Permission
    // از قبل برای Vendors seed شده بودند (ContractManager: Create/Edit) ولی هرگز به کنترلر
    // وصل نشده بودند. صفحات فقط-خواندنی (Index/Details) دست‌نخورده می‌مانند.
    private async Task<bool> EnsurePermissionAsync(string action, string friendlyMessage)
    {
        if (await _permissionService.HasPermissionAsync(_currentUser.Roles, "Vendors", action))
            return true;
        TempData["Error"] = friendlyMessage;
        return false;
    }

    // طبق «PEMS — Global Grid Hardening» P1: این Grid فیلتری نداشت؛ فقط صفحه‌بندی واقعی اضافه شد.
    public async Task<IActionResult> Index(int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Vendors";
        ViewData["Section"] = "Contract Management";

        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var result = await _service.GetPagedAsync(page, normalizedPageSize);
        return View(result);
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
    public async Task<IActionResult> Create()
    {
        if (!await EnsurePermissionAsync("Create", "شما مجاز به ثبت وندور جدید نیستید."))
            return RedirectToAction(nameof(Index));

        ViewData["Title"] = "New Vendor";
        ViewData["Section"] = "Contract Management";
        return View(new Vendor());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Vendor vendor)
    {
        if (!await EnsurePermissionAsync("Create", "شما مجاز به ثبت وندور جدید نیستید."))
            return RedirectToAction(nameof(Index));

        await _service.CreateAsync(vendor);
        TempData["Message"] = "Vendor created successfully";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!await EnsurePermissionAsync("Edit", "شما مجاز به ویرایش این وندور نیستید."))
            return RedirectToAction(nameof(Details), new { id });

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
        if (!await EnsurePermissionAsync("Edit", "شما مجاز به ویرایش این وندور نیستید."))
            return RedirectToAction(nameof(Details), new { id = vendor.VendorId });

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
        if (!await EnsurePermissionAsync("Edit", "شما مجاز به تغییر وضعیت فعال‌بودن این وندور نیستید."))
            return RedirectToAction(nameof(Index));

        await _service.ToggleActiveAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecalculateRisk(int id)
    {
        if (!await EnsurePermissionAsync("Edit", "شما مجاز به محاسبه‌ی مجدد ریسک این وندور نیستید."))
            return RedirectToAction(nameof(Details), new { id });

        await _riskService.RecalculateAsync(id);
        TempData["Message"] = "Risk score recalculated";
        return RedirectToAction(nameof(Details), new { id });
    }
}
