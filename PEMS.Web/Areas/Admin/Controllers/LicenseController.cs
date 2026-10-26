using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Licensing;
using PEMS.Licensing.Formats;
using PEMS.Persistence.Identity;
using PEMS.Web.Licensing;

namespace PEMS.Web.Areas.Admin.Controllers;

/// <summary>
/// Customer licence page (plan row #108): status, short fingerprint, request download, licence or zip upload. Every
/// licensing step of the customer is a step on this page; all logic lives in <see cref="LicenseManager"/>.
/// The page is on the always-free surface list, so it stays reachable in activation-required and restricted states.
/// </summary>
[Area("Admin")]
[Authorize(Roles = SystemRoles.SystemAdmin)]
public class LicenseController : Controller
{
    private readonly LicenseManager _manager;

    public LicenseController(LicenseManager manager) => _manager = manager;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Licence";
        ViewData["Section"] = "Administration";
        return View(new LicensePageModel(await _manager.InspectAsync(ct), _manager.Settings.Enforce));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Request(string requestType, string customerRef, string orderRef, string? reason, CancellationToken ct)
    {
        if (!RequestTypes.All.Contains(requestType) || string.IsNullOrWhiteSpace(customerRef))
        {
            TempData["Error"] = "Lic_Err_RequestFields";
            return RedirectToAction(nameof(Index));
        }

        var built = await _manager.CreateRequestAsync(requestType, customerRef.Trim(), (orderRef ?? "").Trim(), reason, ct);
        return File(System.Text.Encoding.UTF8.GetBytes(built.FileText), "application/octet-stream", $"pems-{requestType}-{built.Pending.Nonce[..8]}.pemsreq");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Error"] = "Lic_Err_NoFile";
            return RedirectToAction(nameof(Index));
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var outcome = await _manager.ApplyLicenceAsync(buffer.ToArray(), ct);
        if (outcome.Applied) TempData["Message"] = "Lic_Applied";
        else TempData["Error"] = "Lic_Fail_" + outcome.Failure;
        return RedirectToAction(nameof(Index));
    }
}
