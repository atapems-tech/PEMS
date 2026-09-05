using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق CMMS Production Baseline v1 بند ۶ (CMMS-01: Asset Foundation): FunctionalLocation از
/// قبل در مدل دیتابیس بود (با ساختار درختی Parent/Child) ولی هیچ راه مدیریتی نداشت. طبق الگوی
/// MaterialCategory: لیست تخت با ستون Parent/Breadcrumb، نه یک Tree Widget جداگانه.
/// </summary>
public class FunctionalLocationController : Controller
{
    private readonly IFunctionalLocationService _service;
    private readonly ICurrentUserService _currentUser;

    public FunctionalLocationController(IFunctionalLocationService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): قبلاً کل درخت با
    // ToListAsync (بدون Skip/Take) بارگذاری می‌شد.
    public async Task<IActionResult> Index(int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Functional Locations";
        ViewData["Section"] = "CMMS Integration";

        var items = await _service.GetPagedListAsync(page, DataGridPageSize.Normalize(pageSize));
        ViewBag.PickerList = await _service.GetPickerListAsync();

        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string code, string name, int? parentId, string? plant, string? area, string? unit, string? description)
    {
        var (success, error, _) = await _service.CreateAsync(
            code, name, parentId, plant, area, unit, description, _currentUser.UserName);

        TempData[success ? "Message" : "Error"] = success ? "Functional Location created" : error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id, string code, string name, int? parentId, string? plant, string? area, string? unit, string? description)
    {
        var (success, error) = await _service.UpdateAsync(
            id, code, name, parentId, plant, area, unit, description, _currentUser.UserName);

        TempData[success ? "Message" : "Error"] = success ? "Functional Location updated" : error;
        return RedirectToAction(nameof(Index));
    }
}
