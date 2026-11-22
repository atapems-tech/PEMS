using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// ویزارد Import اکسل VDR: آپلود → Staging خودکار (پارس + Validate + Duplicate Check) →
/// بازبینی توسط کاربر → Commit یا Discard. طبق الزام "Approval Before Final Storage".
/// </summary>
public class VDRImportController : Controller
{
    private readonly IVDRImportService _importService;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;

    public VDRImportController(IVDRImportService importService, PEMSDbContext context, ICurrentUserService currentUser,
        IPermissionService permissionService)
    {
        _importService = importService;
        _context = context;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    // طبق «PEMS — Global Engineering Management UI/UX, Navigation & RBAC Hardening» بند ۱۰:
    // فقط Commit این Pipeline گیت داشت (RequirePermission("VDR","Approve")) - Index/Upload/
    // Review/ToggleRow/Discard برای هر کاربر احرازهویت‌شده‌ای باز بودند. با همون ماژول "VDR"
    // (نه ماژول جدید) و همون الگوی سایر Pipeline های Import هم‌راستا شد.
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "VDR", "View"))
            return Forbid();

        ViewData["Title"] = "VDR Import";
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
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "VDR", "Create"))
            return Forbid();

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select a VDR Excel file.";
            return RedirectToAction(nameof(Index));
        }

        using var stream = file.OpenReadStream();

        // طبق باگ واقعی که در Equipment Import پیدا شد (همان کلاس باگ): ImportedById باید
        // EmployeeId باشد، نه شناسه‌ی خام کاربر (UserId) - این‌جا هنوز FK اعمال نشده بود پس
        // خطا نمی‌داد، ولی مقدار غلط ذخیره می‌شد
        var importedByEmployeeId = await GetCurrentEmployeeIdAsync();

        var batchId = await _importService.StageFromExcelAsync(stream, file.FileName, projectId, importedByEmployeeId);

        return RedirectToAction(nameof(Review), new { batchId });
    }

    [HttpGet]
    public async Task<IActionResult> Review(int batchId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "VDR", "View"))
            return Forbid();

        ViewData["Title"] = "Review VDR Import";
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
