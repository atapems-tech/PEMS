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
    private readonly IPermissionService _permissionService;

    public EquipmentImportController(IEquipmentImportService importService, IEquipmentPdfScanService pdfScanService,
        PEMSDbContext context, ICurrentUserService currentUser, IPermissionService permissionService)
    {
        _importService = importService;
        _pdfScanService = pdfScanService;
        _context = context;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    [PEMS.Web.Security.Uploads.UploadPolicy(PEMS.Application.Uploads.UploadProfile.Spreadsheet)]

    /// <summary>SCRUM-695: the upload form of the PDF scan (the page of the batches links here); the result page stays code.</summary>
    [HttpGet]
    public async Task<IActionResult> ScanPdfForm()
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "EquipmentImport", "Create"))
            return Forbid();
        ViewData["Title"] = "Scan a PDF";
        ViewData["Section"] = "CMMS Integration";
        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive).Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();
        return View();
    }

    // طبق درخواست: تشخیص تجهیزات از فایل PDF - یک تشخیص «بهترین تلاش» شماره‌تگ است (نه
    // استخراج کامل جدول مثل اکسل)؛ نتیجه برای بازبینی و انتخاب کاربر نشان داده می‌شود
    [HttpPost]
    [ValidateAntiForgeryToken]
    [PEMS.Web.Security.Uploads.UploadPolicy(PEMS.Application.Uploads.UploadProfile.Pdf)]
    public async Task<IActionResult> ScanPdf(IFormFile file, int projectId)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "EquipmentImport", "Create"))
            return Forbid();

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select a PDF file.";
            return Redirect("/p/equipment-import");
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

    // طبق بند ۱۱ ممیزی: این اکشن حتی از Commit پرریسک‌تر است - مستقیم و بدون هیچ مرحله‌ی
    // Staging/بازبینی به جدول Equipments می‌نویسد. همون سطح مجوز Commit (Approve) لازم است.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFromTags(int projectId, int disciplineId, List<string> tags)
    {
        if (!await _permissionService.HasPermissionAsync(_currentUser.Roles, "EquipmentImport", "Approve"))
            return Forbid();

        if (tags == null || !tags.Any())
        {
            TempData["Error"] = "No tags were selected.";
            return Redirect("/p/equipment-import");
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
        return Redirect("/p/equipments");
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
