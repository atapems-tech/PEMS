using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// ویزارد Import اکسل MDR واقعی: آپلود → Staging خودکار (پارس چند-شیتی + Validate + Duplicate) →
/// بازبینی توسط کاربر → Commit یا Discard. دقیقاً همان الگوی VDRImportController.
/// </summary>
public class MDRImportController : Controller
{
    private readonly IMDRImportService _importService;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public MDRImportController(IMDRImportService importService, PEMSDbContext context, ICurrentUserService currentUser,
        IPermissionService permissionService)
    {
        _importService = importService;
        _context = context;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    // طبق «PEMS — Global Engineering Management UI/UX, Navigation & RBAC Hardening» بند ۹: این
    // Pipeline قبلاً هیچ بررسی مجوزی نداشت - Commit (که EngineeringDocument/DocumentRevision/
    // DocumentWorkflow معتبر می‌ساخت) برای هر کاربر احرازهویت‌شده‌ای باز بود.
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MDRImport", "View"))
            return Forbid();

        ViewData["Title"] = "MDR Import";
        ViewData["Section"] = "Engineering Management";
        ViewData["HelpKey"] = "Imports.StagedBatchOverview";

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        var recentBatches = await _importService.GetRecentBatchesAsync();

        return View(recentBatches);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PEMS.Web.Security.Uploads.UploadPolicy(PEMS.Application.Uploads.UploadProfile.Spreadsheet)]
    public async Task<IActionResult> Upload(IFormFile file, int projectId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MDRImport", "Create"))
            return Forbid();

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select an MDR Excel file.";
            return RedirectToAction(nameof(Index));
        }

        using var stream = file.OpenReadStream();

        // طبق باگ واقعی که در Equipment Import پیدا شد (همان کلاس باگ): ImportedById باید
        // EmployeeId باشد، نه شناسه‌ی خام کاربر (UserId)
        var importedByEmployeeId = await GetCurrentEmployeeIdAsync();

        var batchId = await _importService.StageFromExcelAsync(stream, file.FileName, projectId, importedByEmployeeId);

        return RedirectToAction(nameof(Review), new { batchId });
    }

    [HttpGet]
    public async Task<IActionResult> Review(int batchId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "MDRImport", "View"))
            return Forbid();

        ViewData["Title"] = "Review MDR Import";
        ViewData["Section"] = "Engineering Management";

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
