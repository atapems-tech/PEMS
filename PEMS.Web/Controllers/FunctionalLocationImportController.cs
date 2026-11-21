using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق «Functional Location Master Data Import»: همان الگوی EquipmentImportController
/// (Upload → Stage → Review/Toggle → Commit/Discard) - ماژول مجوز مستقل
/// "FunctionalLocationImport" (مثل EquipmentImport/MDRImport/MrsImport)، نه یک زیرساخت جدید.
/// </summary>
public class FunctionalLocationImportController : Controller
{
    private readonly IFunctionalLocationImportService _importService;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public FunctionalLocationImportController(IFunctionalLocationImportService importService, PEMSDbContext context,
        ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _importService = importService;
        _context = context;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "FunctionalLocationImport", "View"))
            return Forbid();

        ViewData["Title"] = "Functional Location Import";
        ViewData["Section"] = "CMMS Integration";
        ViewData["HelpKey"] = "Imports.StagedBatchOverview";

        var recentBatches = await _importService.GetRecentBatchesAsync();
        return View(recentBatches);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PEMS.Web.Security.Uploads.UploadPolicy(PEMS.Application.Uploads.UploadProfile.Spreadsheet)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "FunctionalLocationImport", "Create"))
            return Forbid();

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select a Functional Location Excel file.";
            return RedirectToAction(nameof(Index));
        }

        using var stream = file.OpenReadStream();

        var importedByEmployeeId = await GetCurrentEmployeeIdAsync();
        var batchId = await _importService.StageFromExcelAsync(stream, file.FileName, importedByEmployeeId);

        return RedirectToAction(nameof(Review), new { batchId });
    }

    [HttpGet]
    public async Task<IActionResult> Review(int batchId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "FunctionalLocationImport", "View"))
            return Forbid();

        ViewData["Title"] = "Review Functional Location Import";
        ViewData["Section"] = "CMMS Integration";

        var batch = await _importService.GetBatchAsync(batchId);
        if (batch == null) return NotFound();

        return View(batch);
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;

        return await _context.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();
    }
}
