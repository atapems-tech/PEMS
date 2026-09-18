using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

// ==============================================================================
// TQ / TSR / RFI - طبق فاز ۱ نقشه‌راه معماری PEMS (تکامل از سند Asset Lifecycle).
// موجودیت مستقل با گردش‌کار اختصاصی روی موتور عمومی Workflow Platform.
// ==============================================================================
public class TechnicalQueryController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly ITechnicalQueryService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;

    public TechnicalQueryController(PEMSDbContext context, ITechnicalQueryService service, ICurrentUserService currentUser, IFileStorageService fileStorage)
    {
        _context = context;
        _service = service;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
    }


    public async Task<IActionResult> Index(string? queryType, string? status, int? projectId, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "TQ / TSR / RFI";
        ViewData["Section"] = "Engineering Communication";
        ViewData["HelpKey"] = "TechnicalQuery.Index";

        var query = _context.TechnicalQueries
            .AsNoTracking()
            .Where(x => x.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(queryType)) query = query.Where(x => x.QueryType == queryType);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);

        // طبق سند Global Data Grid Standard فاز ۲ بند ۱۰: قبلاً کل جدول (بعد از اعمال همون
        // فیلترهای QueryType/Status/ProjectId انتخابی کاربر) با ToListAsync بارگذاری می‌شد و
        // OpenCount/OverdueCount در حافظه‌ی C# روی همون فهرست فیلترشده محاسبه می‌شدند - رفتار
        // دقیقاً حفظ شده (شمارنده‌ها هنوز به فیلتر انتخابی احترام می‌گذارند)، فقط حالا COUNT
        // سمت SQL است، نه بعد از بارگذاری کامل ردیف‌ها در حافظه
        ViewBag.OpenCount = await query.CountAsync(x => x.Status == "Open" || x.Status == "Reopened" || x.Status == "PendingConfirmation");
        ViewBag.OverdueCount = await query.CountAsync(x =>
            (x.Status == "Open" || x.Status == "Reopened") &&
            x.RequiredResponseDate.HasValue &&
            x.RequiredResponseDate.Value < DateTime.Now);

        var totalCount = await query.CountAsync();
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);

        var items = await query
            .OrderByDescending(x => x.RaisedDate)
            .ThenByDescending(x => x.TechnicalQueryId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new TechnicalQueryListItemDto
            {
                TechnicalQueryId = x.TechnicalQueryId,
                QueryNumber = x.QueryNumber,
                QueryType = x.QueryType,
                Subject = x.Subject,
                DisciplineName = x.Discipline != null ? x.Discipline.Name : "",
                Priority = x.Priority,
                RequiredResponseDate = x.RequiredResponseDate,
                Status = x.Status,
                IsOverdue = (x.Status == "Open" || x.Status == "Reopened") &&
                    x.RequiredResponseDate.HasValue &&
                    x.RequiredResponseDate.Value < DateTime.Now
            })
            .ToListAsync();

        ViewBag.Projects = await _context.Projects.Where(x => x.IsActive)
            .Select(x => new { x.ProjectId, x.ProjectName }).ToListAsync();

        ViewBag.SelectedQueryType = queryType;
        ViewBag.SelectedStatus = status;
        ViewBag.SelectedProjectId = projectId;

        return View(new PagedResult<TechnicalQueryListItemDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        });
    }


    [HttpGet]
    public async Task<IActionResult> Raise()
    {
        ViewData["Title"] = "New TQ / TSR / RFI";
        ViewData["Section"] = "Engineering Communication";

        await LoadDropdownsAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Raise(
        string queryType, string subject, string? description, int projectId, int disciplineId,
        int? relatedDocumentId, int? relatedEquipmentId, DateTime? requiredResponseDate, string priority)
    {
        try
        {
            var id = await _service.RaiseAsync(
                queryType, subject, description, projectId, disciplineId,
                relatedDocumentId, relatedEquipmentId, requiredResponseDate, priority,
                await GetCurrentEmployeeIdAsync(), _currentUser.Roles, GetClientIp(), GetUserAgent());

            TempData["Message"] = "ثبت شد.";
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
        ViewData["Title"] = "TQ / TSR / RFI Details";
        ViewData["Section"] = "Engineering Communication";

        var tq = await _context.TechnicalQueries
            .Include(x => x.Discipline)
            .Include(x => x.Project)
            .Include(x => x.RelatedDocument)
            .Include(x => x.RelatedEquipment)
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.TechnicalQueryId == id);

        if (tq == null) return NotFound();

        var instance = await _context.WorkflowInstances
            .Where(x => x.Module == "TechnicalQuery" && x.BusinessObjectType == "TechnicalQuery" && x.BusinessObjectId == id)
            .OrderByDescending(x => x.StartDate)
            .Select(x => new { x.WorkflowInstanceId, x.Status, StageName = x.CurrentStage != null ? x.CurrentStage.Name : null })
            .FirstOrDefaultAsync();

        ViewBag.WorkflowRunning = instance != null && instance.Status == "Running";
        ViewBag.CurrentStageName = instance?.StageName;
        ViewBag.MyDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();

        return View(tq);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(int technicalQueryId, string responseText)
    {
        var myDisciplineId = await GetCurrentEmployeeDisciplineIdAsync();
        var myEmployeeId = await GetCurrentEmployeeIdAsync();

        if (!myDisciplineId.HasValue || !myEmployeeId.HasValue)
        {
            TempData["Error"] = "کارمند مرتبط با این کاربر یا دیسیپلینش مشخص نیست.";
            return RedirectToAction(nameof(Details), new { id = technicalQueryId });
        }

        try
        {
            await _service.RespondAsync(technicalQueryId, responseText, myEmployeeId.Value, myDisciplineId.Value, GetClientIp(), GetUserAgent());
            TempData["Message"] = "پاسخ ثبت شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = technicalQueryId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int technicalQueryId)
    {
        try
        {
            await _service.ConfirmAsync(technicalQueryId, await GetCurrentEmployeeIdAsync(), _currentUser.Roles, GetClientIp(), GetUserAgent());
            TempData["Message"] = "بسته شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = technicalQueryId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(int technicalQueryId, string reason)
    {
        try
        {
            await _service.ReopenAsync(technicalQueryId, reason, await GetCurrentEmployeeIdAsync(), _currentUser.Roles, GetClientIp(), GetUserAgent());
            TempData["Message"] = "دوباره باز شد و به دیسیپلین مسئول برگشت.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = technicalQueryId });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadAttachment(int technicalQueryId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "فایلی انتخاب نشده.";
            return RedirectToAction(nameof(Details), new { id = technicalQueryId });
        }

        string storageKey;
        await using (var uploadStream = file.OpenReadStream())
        {
            storageKey = await _fileStorage.SaveAsync("technical-queries", file.FileName, uploadStream);
        }

        await _context.DocumentAttachments.AddAsync(new PEMS.Domain.Entities.DocumentAttachment
        {
            TechnicalQueryId = technicalQueryId,
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
        return RedirectToAction(nameof(Details), new { id = technicalQueryId });
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
