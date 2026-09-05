using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق درخواست: "بخش CMMS باید بتواند تجهیزات را با فایل اکسل تشخیص دهد" - همان الگوی
/// MDR/VDR Import: آپلود → Staging خودکار (تشخیص خودکار سطر Header + چند شیت) → بازبینی → Commit.
/// </summary>
public class EquipmentImportController : Controller
{
    private readonly IEquipmentImportService _importService;
    private readonly IEquipmentPdfScanService _pdfScanService;
    private readonly PEMSDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public EquipmentImportController(IEquipmentImportService importService, IEquipmentPdfScanService pdfScanService,
        PEMSDbContext context, ICurrentUserService currentUser)
    {
        _importService = importService;
        _pdfScanService = pdfScanService;
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Equipment Import";
        ViewData["Section"] = "CMMS Integration";

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        var recentBatches = await _importService.GetRecentBatchesAsync();

        return View(recentBatches);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile file, int projectId)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select an Equipment Excel file.";
            return RedirectToAction(nameof(Index));
        }

        using var stream = file.OpenReadStream();

        // طبق خطای واقعی گزارش‌شده: ImportedById به جدول Employees وصل است (نه ApplicationUser)
        // - قبلاً شناسه‌ی خام کاربر (UserId) مستقیم پاس داده می‌شد که با کلید Employee یکی
        // نیست، پس محدودیت کلید خارجی نقض می‌شد. همان الگویی که برای ساخت User هم رفع شد.
        var importedByEmployeeId = await GetCurrentEmployeeIdAsync();

        var batchId = await _importService.StageFromExcelAsync(stream, file.FileName, projectId, importedByEmployeeId);

        return RedirectToAction(nameof(Review), new { batchId });
    }

    [HttpGet]
    public async Task<IActionResult> Review(int batchId)
    {
        ViewData["Title"] = "Review Equipment Import";
        ViewData["Section"] = "CMMS Integration";

        var batch = await _importService.GetBatchAsync(batchId);
        if (batch == null) return NotFound();

        return View(batch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRow(int rowId, bool include, int batchId)
    {
        await _importService.SetRowIncludeAsync(rowId, include);
        return RedirectToAction(nameof(Review), new { batchId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Commit(int batchId)
    {
        var (created, updated, skipped) = await _importService.CommitBatchAsync(batchId);

        TempData["Message"] = $"Equipment Import committed: {created} created, {updated} updated, {skipped} skipped.";

        return RedirectToAction("Index", "Equipment");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Discard(int batchId)
    {
        await _importService.DiscardBatchAsync(batchId);
        TempData["Message"] = "Equipment import batch discarded.";
        return RedirectToAction(nameof(Index));
    }

    // طبق درخواست: تشخیص تجهیزات از فایل PDF - یک تشخیص «بهترین تلاش» شماره‌تگ است (نه
    // استخراج کامل جدول مثل اکسل)؛ نتیجه برای بازبینی و انتخاب کاربر نشان داده می‌شود
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ScanPdf(IFormFile file, int projectId)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select a PDF file.";
            return RedirectToAction(nameof(Index));
        }

        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");

        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create))
                await file.CopyToAsync(stream);

            var candidates = await _pdfScanService.ScanForTagCandidatesAsync(tempPath);

            var existingTags = await _context.Equipments
                .Where(x => x.ProjectId == projectId)
                .Select(x => x.TagNumber.ToUpper())
                .ToListAsync();

            ViewBag.FileName = file.FileName;
            ViewBag.ProjectId = projectId;
            ViewBag.ExistingTags = existingTags;

            ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
                .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();

            return View("ScanResult", candidates);
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFromTags(int projectId, int disciplineId, List<string> tags)
    {
        if (tags == null || !tags.Any())
        {
            TempData["Error"] = "No tags were selected.";
            return RedirectToAction(nameof(Index));
        }

        var existingTags = await _context.Equipments
            .Where(x => x.ProjectId == projectId)
            .Select(x => x.TagNumber.ToUpper())
            .ToListAsync();

        var created = 0;

        foreach (var tag in tags.Distinct())
        {
            if (existingTags.Contains(tag.ToUpper())) continue;

            await _context.Equipments.AddAsync(new PEMS.Domain.Entities.Equipment
            {
                TagNumber = tag,
                Name = tag,
                EquipmentType = "Unspecified",
                ProjectId = projectId,
                DisciplineId = disciplineId,
                Status = "Active",
                IsActive = true,
                CreatedDate = DateTime.Now
            });

            created++;
        }

        await _context.SaveChangesAsync();

        TempData["Message"] = $"{created} equipment tag(s) added from PDF scan. Names and types are placeholders - please edit each one with the real details.";
        return RedirectToAction("Index", "Equipment");
    }

    // همان الگویی که در EngineeringDocumentsController برای رفع همین دسته باگ استفاده شد:
    // UserId (ApplicationUser) با EmployeeId یکی نیست، باید resolve شود
    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;

        return await _context.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();
    }
}
