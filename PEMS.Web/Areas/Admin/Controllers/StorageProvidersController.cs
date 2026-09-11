using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Identity;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۱۴/۱۵: مدیریت ادمین Storage Provider ها.
/// رمز عبور هرگز به View برنمی‌گردد - فقط HasPassword برای الگوی "Change Password" (بند ۱۲).
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class StorageProvidersController : Controller
{
    private readonly IStorageProviderService _service;

    public StorageProvidersController(IStorageProviderService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Storage Providers";
        ViewData["Section"] = "Administration";

        var providers = await _service.GetAllAsync();
        return View(providers);
    }

    public IActionResult Create()
    {
        ViewData["Title"] = "New Storage Provider";
        ViewBag.ProviderTypes = Domain.Entities.StorageProviderTypes.All;
        return View(new StorageProviderUpsertRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StorageProviderUpsertRequest request)
    {
        var (success, error, id) = await _service.CreateAsync(request, User.Identity?.Name);
        if (!success)
        {
            TempData["Error"] = error;
            ViewBag.ProviderTypes = Domain.Entities.StorageProviderTypes.All;
            return View(request);
        }

        TempData["Message"] = $"Storage Provider «{request.Name}» created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var provider = await _service.GetByIdAsync(id);
        if (provider == null) return NotFound();

        ViewData["Title"] = $"Edit — {provider.Name}";
        ViewBag.ProviderTypes = Domain.Entities.StorageProviderTypes.All;
        ViewBag.StorageProviderId = id;

        var request = new StorageProviderUpsertRequest
        {
            Name = provider.Name,
            Code = provider.Code,
            ProviderType = provider.ProviderType,
            Host = provider.Host,
            Port = provider.Port,
            Username = provider.Username,
            RootPath = provider.RootPath,
            BaseUrl = provider.BaseUrl,
            UseSsl = provider.UseSsl,
            UsePassiveMode = provider.UsePassiveMode,
            AllowRead = provider.AllowRead,
            AllowWrite = provider.AllowWrite,
            AllowDelete = provider.AllowDelete,
            IsEnabled = provider.IsEnabled
        };
        ViewBag.HasPassword = provider.HasPassword;

        return View(request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StorageProviderUpsertRequest request)
    {
        var (success, error) = await _service.UpdateAsync(id, request, User.Identity?.Name);
        if (!success)
        {
            TempData["Error"] = error;
            ViewBag.ProviderTypes = Domain.Entities.StorageProviderTypes.All;
            ViewBag.StorageProviderId = id;
            var current = await _service.GetByIdAsync(id);
            ViewBag.HasPassword = current?.HasPassword ?? false;
            return View(request);
        }

        TempData["Message"] = $"Storage Provider «{request.Name}» updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection(int id)
    {
        var result = await _service.TestConnectionAsync(id);
        TempData["Message"] = result.Message ?? $"Overall status: {result.OverallStatus}";
        TempData["TestSteps"] = System.Text.Json.JsonSerializer.Serialize(result.Steps);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(int id)
    {
        var (success, error) = await _service.SetDefaultAsync(id, User.Identity?.Name);
        TempData[success ? "Message" : "Error"] = success ? "Default Storage Provider updated." : error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEnabled(int id, bool enabled)
    {
        var (success, error) = await _service.SetEnabledAsync(id, enabled, User.Identity?.Name);
        TempData[success ? "Message" : "Error"] = success ? (enabled ? "Provider enabled." : "Provider disabled.") : error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _service.DeleteAsync(id);
        TempData[success ? "Message" : "Error"] = success ? "Storage Provider removed." : error;
        return RedirectToAction(nameof(Index));
    }
}
