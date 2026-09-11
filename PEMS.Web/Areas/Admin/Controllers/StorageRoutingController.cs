using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Identity;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۵: مدیریت ادمین قوانین مسیریابی
/// (کدام ماژول/نوع سند روی کدام Storage Provider ذخیره شود).
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class StorageRoutingController : Controller
{
    private readonly IStorageRouteService _routeService;
    private readonly IStorageProviderService _providerService;

    public StorageRoutingController(IStorageRouteService routeService, IStorageProviderService providerService)
    {
        _routeService = routeService;
        _providerService = providerService;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Storage Routing";
        ViewData["Section"] = "Administration";

        var routes = await _routeService.GetAllAsync();
        return View(routes);
    }

    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "New Storage Route";
        await LoadProvidersAsync();
        return View(new StorageRouteUpsertRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StorageRouteUpsertRequest request)
    {
        var (success, error, id) = await _routeService.CreateAsync(request, User.Identity?.Name);
        if (!success)
        {
            TempData["Error"] = error;
            await LoadProvidersAsync();
            return View(request);
        }

        TempData["Message"] = $"Storage route for «{request.Module}» created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var routes = await _routeService.GetAllAsync();
        var route = routes.FirstOrDefault(x => x.StorageRouteId == id);
        if (route == null) return NotFound();

        ViewData["Title"] = $"Edit Route — {route.Module}";
        ViewBag.StorageRouteId = id;
        await LoadProvidersAsync();

        return View(new StorageRouteUpsertRequest
        {
            Module = route.Module,
            DocumentType = route.DocumentType,
            StorageProviderId = route.StorageProviderId,
            RelativeRootPath = route.RelativeRootPath,
            IsEnabled = route.IsEnabled,
            IsDefault = route.IsDefault,
            Priority = route.Priority
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StorageRouteUpsertRequest request)
    {
        var (success, error) = await _routeService.UpdateAsync(id, request, User.Identity?.Name);
        if (!success)
        {
            TempData["Error"] = error;
            ViewBag.StorageRouteId = id;
            await LoadProvidersAsync();
            return View(request);
        }

        TempData["Message"] = $"Storage route for «{request.Module}» updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _routeService.DeleteAsync(id);
        TempData[success ? "Message" : "Error"] = success ? "Storage route removed." : error;
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadProvidersAsync()
    {
        var providers = await _providerService.GetAllAsync();
        ViewBag.Providers = providers;
    }
}
