using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

// ==============================================================================
// MOC (Management of Change) - طبق فاز ۲ نقشه‌راه معماری PEMS. فرآیند مستقل با گردش‌کار
// اختصاصی روی موتور عمومی Workflow Platform، طبق رویه‌ی استاندارد PSM.
// ==============================================================================
public class MOCController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly IMOCService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;

    public MOCController(PEMSDbContext context, IMOCService service, ICurrentUserService currentUser, IFileStorageService fileStorage)
    {
        _context = context;
        _service = service;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
    }


    public async Task<IActionResult> Index(string? riskLevel, string? status, int? projectId, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Management of Change (MOC)";
        ViewData["Section"] = "Engineering Communication";
        ViewData["HelpKey"] = "MOC.Index";

        var query = _context.MOCs
            .AsNoTracking()
            .Where(x => x.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(riskLevel)) query = query.Where(x => x.RiskLevel == riskLevel);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);

        // طبق سند Global Data Grid Standard فاز ۲ بند ۱۰: همون فیلترهای انتخابی کاربر - فقط
        // حالا COUNT سمت SQL، نه بعد از بارگذاری کامل ردیف‌ها در حافظه
        ViewBag.OpenCount = await query.CountAsync(x => x.Status != "Closed" && x.Status != "Rejected");
        ViewBag.HighRiskOpenCount = await query.CountAsync(x => x.RiskLevel == "High" && x.Status != "Closed" && x.Status != "Rejected");

        var totalCount = await query.CountAsync();
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);

        var items = await query
            .OrderByDescending(x => x.RequestedDate)
            .ThenByDescending(x => x.MOCId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new MOCListItemDto
            {
                MOCId = x.MOCId,
                MOCNumber = x.MOCNumber,
                Title = x.Title,
                DisciplineName = x.Discipline != null ? x.Discipline.Name : "",
                ChangeType = x.ChangeType,
                RiskLevel = x.RiskLevel,
                Status = x.Status
            })
            .ToListAsync();

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.SelectedRiskLevel = riskLevel;
        ViewBag.SelectedStatus = status;
        ViewBag.SelectedProjectId = projectId;

        return View(new PagedResult<MOCListItemDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        });
    }


    [HttpGet]
    public async Task<IActionResult> Raise(int? relatedEquipmentId)
    {
        ViewData["Title"] = "New MOC";
        ViewData["Section"] = "Engineering Communication";

        await LoadDropdownsAsync();
        ViewBag.PreselectedEquipmentId = relatedEquipmentId;

        if (relatedEquipmentId.HasValue)
        {
            var eq = await _context.Equipments.FirstOrDefaultAsync(x => x.EquipmentId == relatedEquipmentId.Value);
            ViewBag.PreselectedEquipmentLabel = eq?.TagNumber;
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Raise(
        string title, string description, string? reason, string changeType, string riskLevel,
        int projectId, int disciplineId, int? relatedDocumentId, int? relatedEquipmentId,
        DateTime? temporaryExpiryDate, bool trainingRequired)
    {
        try
        {
            var id = await _service.RaiseAsync(
                title, description, reason, changeType, riskLevel, projectId, disciplineId,
                relatedDocumentId, relatedEquipmentId, temporaryExpiryDate, trainingRequired,
                await GetCurrentEmployeeIdAsync(), _currentUser.Roles, GetClientIp(), GetUserAgent());

            TempData["Message"] = "MOC ثبت شد.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Raise));
        }
    }


    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "MOC Details";
        ViewData["Section"] = "Engineering Communication";

        var moc = await _context.MOCs
            .Include(x => x.Discipline)
            .Include(x => x.Project)
            .Include(x => x.RelatedDocument)
            .Include(x => x.RelatedEquipment)
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.MOCId == id);

        if (moc == null) return NotFound();

        var instance = await _context.WorkflowInstances
            .Where(x => x.Module == "MOC" && x.BusinessObjectType == "MOC" && x.BusinessObjectId == id)
            .OrderByDescending(x => x.StartDate)
            .Select(x => new { x.Status, StageName = x.CurrentStage != null ? x.CurrentStage.Name : null })
            .FirstOrDefaultAsync();

        ViewBag.WorkflowRunning = instance != null && instance.Status == "Running";
        ViewBag.CurrentStageName = instance?.StageName;
        ViewBag.MyDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();

        return View(moc);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssessRisk(int mocId, string riskAssessmentText)
    {
        var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
        var myEmployeeId = await GetCurrentEmployeeIdAsync();

        if (!myDisciplineId.HasValue || !myEmployeeId.HasValue)
        {
            TempData["Error"] = "کارمند مرتبط با این کاربر یا دیسیپلینش مشخص نیست.";
            return RedirectToAction(nameof(Details), new { id = mocId });
        }

        try
        {
            await _service.AssessRiskAsync(mocId, riskAssessmentText, myEmployeeId.Value, myDisciplineId.Value, GetClientIp(), GetUserAgent());
            TempData["Message"] = "ارزیابی ریسک ثبت شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = mocId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int mocId)
    {
        try
        {
            await _service.ApproveAsync(mocId, await GetCurrentEmployeeIdAsync(), _currentUser.Roles, GetClientIp(), GetUserAgent());
            TempData["Message"] = "تایید شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = mocId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int mocId, string reason)
    {
        try
        {
            await _service.RejectAsync(mocId, reason, await GetCurrentEmployeeIdAsync(), _currentUser.Roles, GetClientIp(), GetUserAgent());
            TempData["Message"] = "رد شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = mocId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordImplementation(int mocId, string implementationPlan)
    {
        var myEmployeeId = await GetCurrentEmployeeIdAsync();
        if (!myEmployeeId.HasValue) return NotFound();

        try
        {
            await _service.RecordImplementationAsync(mocId, implementationPlan, myEmployeeId.Value, GetClientIp(), GetUserAgent());
            TempData["Message"] = "اجرا ثبت شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = mocId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkDocumentsUpdated(int mocId, string? note)
    {
        var myEmployeeId = await GetCurrentEmployeeIdAsync();
        if (!myEmployeeId.HasValue) return NotFound();

        await _service.MarkDocumentsUpdatedAsync(mocId, note, myEmployeeId.Value);
        TempData["Message"] = "به‌روزرسانی مدارک ثبت شد.";
        return RedirectToAction(nameof(Details), new { id = mocId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkTrainingCompleted(int mocId)
    {
        var myEmployeeId = await GetCurrentEmployeeIdAsync();
        if (!myEmployeeId.HasValue) return NotFound();

        await _service.MarkTrainingCompletedAsync(mocId, myEmployeeId.Value);
        TempData["Message"] = "آموزش تکمیل‌شده ثبت شد.";
        return RedirectToAction(nameof(Details), new { id = mocId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int mocId)
    {
        try
        {
            await _service.CloseAsync(mocId, await GetCurrentEmployeeIdAsync(), _currentUser.Roles, GetClientIp(), GetUserAgent());
            TempData["Message"] = "MOC بسته شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = mocId });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadAttachment(int mocId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "فایلی انتخاب نشده.";
            return RedirectToAction(nameof(Details), new { id = mocId });
        }

        string storageKey;
        await using (var uploadStream = file.OpenReadStream())
        {
            storageKey = await _fileStorage.SaveAsync("moc", file.FileName, uploadStream);
        }

        await _context.DocumentAttachments.AddAsync(new PEMS.Domain.Entities.DocumentAttachment
        {
            MOCId = mocId,
            FileName = file.FileName,
            FileExtension = Path.GetExtension(file.FileName),
            FilePath = storageKey,
            FileSize = file.Length,
            MimeType = file.ContentType,
            UploadedById = await GetCurrentEmployeeIdAsync(),
            UploadDate = DateTime.Now,
            IsActive = true,
            AIStatus = "NotApplicable"
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = "پیوست اضافه شد.";
        return RedirectToAction(nameof(Details), new { id = mocId });
    }


    // =========================
    // Helpers
    // =========================

    private async Task LoadDropdownsAsync()
    {
        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.Disciplines = await _context.Disciplines.Where(x => x.IsActive)
            .Select(x => new { x.DisciplineId, x.Name }).ToListAsync();
    }

    private async Task<int?> GetCurrentEmployeeIdAsync()
    {
        if (!_currentUser.UserId.HasValue) return null;

        return await _context.Users
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.EmployeeId)
            .FirstOrDefaultAsync();
    }

    private async Task<int?> GetCurrentEmployeeDisciplineIdAsync()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (!employeeId.HasValue) return null;

        return await _context.Employees
            .Where(e => e.EmployeeId == employeeId.Value)
            .Select(e => (int?)e.DisciplineId)
            .FirstOrDefaultAsync();
    }

    private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetUserAgent() => Request.Headers["User-Agent"].ToString();
}
