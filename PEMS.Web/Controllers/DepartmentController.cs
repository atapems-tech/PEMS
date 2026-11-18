using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Grids;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Web.Helpers;

namespace PEMS.Web.Controllers;

/// <summary>
/// طبق درخواست: بخش دپارتمان قبلاً فقط یک لینک "Coming Soon" بود، هیچ صفحه‌ای پشتش نبود.
/// </summary>
[Authorize(Roles = SystemRoles.SystemAdmin + "," + SystemRoles.EngineeringManager)]
public class DepartmentController : Controller
{
    private readonly PEMSDbContext _context;

    public DepartmentController(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Departments";
        ViewData["Section"] = "Administration";
        ViewData["HelpKey"] = "Departments.Index";

        var departments = await _context.Departments
            .OrderBy(x => x.Name)
            .ToListAsync();

        var employeeCounts = await _context.Employees
            .Where(x => x.IsActive)
            .GroupBy(x => x.DepartmentId)
            .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
            .ToListAsync();

        ViewBag.EmployeeCounts = employeeCounts.ToDictionary(x => x.DepartmentId, x => x.Count);

        return View(departments);
    }

    /// <summary>SCRUM-342 (W1 pilot): the rows of the department grid of the page definition <c>departments</c>: paging, sorting, filtering and quick search by the shared grid helpers; same roles as the page.</summary>
    [HttpGet]
    public async Task<IActionResult> Grid(CancellationToken ct)
    {
        GridRequest request;
        try { request = GridRequest.FromQuery(Request.Query.Select(q => new KeyValuePair<string, string?>(q.Key, q.Value.ToString()))); }
        catch (GridQueryException ex) { return BadRequest(new { error = ex.Message }); }
        var counts = await _context.Employees.Where(x => x.IsActive).GroupBy(x => x.DepartmentId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var query = _context.Departments.AsNoTracking().Select(d => new DepartmentRow { Id = d.DepartmentId, Code = d.Code, Name = d.Name, Description = d.Description, Active = d.IsActive, Created = d.CreatedDate });
        try
        {
            var total = await query.ApplyGridFilters(request, DepartmentRow.Fields).CountAsync(ct);
            var rows = await query.ApplyGridFilters(request, DepartmentRow.Fields).ApplyGridSort(request, DepartmentRow.Fields).Paged(request).ToListAsync(ct);
            foreach (var r in rows) r.Employees = counts.TryGetValue(r.Id, out var n) ? n : 0;
            return Json(new { items = rows, total });
        }
        catch (GridQueryException ex) { return BadRequest(new { error = ex.Message }); }
    }

    public sealed class DepartmentRow
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public bool Active { get; set; }
        public DateTime Created { get; set; }
        public int Employees { get; set; }

        public static readonly GridFields<DepartmentRow> Fields = new GridFields<DepartmentRow>()
            .Add("name", r => r.Name, searchable: true).Add("code", r => r.Code, searchable: true).Add("description", r => r.Description!, searchable: true)
            .Add("active", r => r.Active).Add("created", r => r.Created).Add("id", r => r.Id).TieBreak("id");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string code, string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Department name is required";
            return RedirectToAction(nameof(Index));
        }

        var trimmedName = name.Trim();
        var trimmedCode = code?.Trim() ?? "";

        // طبق درخواست: در زمان ثبت، مقدار تکراری ثبت نشود
        var duplicate = await _context.Departments.AnyAsync(x =>
            x.Name.ToLower() == trimmedName.ToLower() ||
            (trimmedCode != "" && x.Code.ToLower() == trimmedCode.ToLower()));

        if (duplicate)
        {
            TempData["Error"] = "دپارتمانی با همین نام یا کد از قبل وجود دارد.";
            return RedirectToAction(nameof(Index));
        }

        await _context.Departments.AddAsync(new Department
        {
            Code = trimmedCode,
            Name = trimmedName,
            Description = description,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = "Department created";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int departmentId, string code, string name, string? description)
    {
        var dept = await _context.Departments.FirstOrDefaultAsync(x => x.DepartmentId == departmentId);
        if (dept == null) return NotFound();

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Department name is required";
            return RedirectToAction(nameof(Index));
        }

        var trimmedName = name.Trim();
        var trimmedCode = code?.Trim() ?? "";

        var duplicate = await _context.Departments.AnyAsync(x =>
            x.DepartmentId != departmentId &&
            (x.Name.ToLower() == trimmedName.ToLower() || (trimmedCode != "" && x.Code.ToLower() == trimmedCode.ToLower())));

        if (duplicate)
        {
            TempData["Error"] = "دپارتمانی با همین نام یا کد از قبل وجود دارد.";
            return RedirectToAction(nameof(Index));
        }

        dept.Code = trimmedCode;
        dept.Name = trimmedName;
        dept.Description = description;

        await _context.SaveChangesAsync();

        TempData["Message"] = "Department updated";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int departmentId)
    {
        var dept = await _context.Departments.FirstOrDefaultAsync(x => x.DepartmentId == departmentId);
        if (dept == null) return NotFound();

        dept.IsActive = !dept.IsActive;
        await _context.SaveChangesAsync();

        TempData["Message"] = dept.IsActive ? "Department reactivated" : "Department deactivated";
        return RedirectToAction(nameof(Index));
    }


    public async Task<IActionResult> Export()
    {
        var departments = await _context.Departments.OrderBy(x => x.Name).ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Departments");
        sheet.AddStyledHeaderRow("Code", "Name", "Description", "IsActive");

        int row = 2;
        foreach (var d in departments)
        {
            sheet.Cell(row, 1).Value = d.Code;
            sheet.Cell(row, 2).Value = d.Name;
            sheet.Cell(row, 3).Value = d.Description;
            sheet.Cell(row, 4).Value = d.IsActive;
            row++;
        }

        sheet.FinalizeExportSheet();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Departments_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PEMS.Web.Security.Uploads.UploadPolicy(PEMS.Application.Uploads.UploadProfile.Spreadsheet)]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "فایلی انتخاب نشده.";
            return RedirectToAction(nameof(Index));
        }

        var existingNames = new HashSet<string>(
            await _context.Departments.Select(x => x.Name).ToListAsync(), StringComparer.OrdinalIgnoreCase);

        int added = 0, skippedDuplicate = 0;

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        for (int r = 2; r <= lastRow; r++)
        {
            var name = sheet.Cell(r, 2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;

            // طبق درخواست: مقدار تکراری ثبت نشود
            if (existingNames.Contains(name))
            {
                skippedDuplicate++;
                continue;
            }

            await _context.Departments.AddAsync(new Department
            {
                Code = sheet.Cell(r, 1).GetString().Trim(),
                Name = name,
                Description = sheet.Cell(r, 3).GetString().Trim(),
                IsActive = true,
                CreatedDate = DateTime.Now
            });

            existingNames.Add(name);
            added++;
        }

        await _context.SaveChangesAsync();

        TempData["Message"] = $"ایمپورت تمام شد: {added} اضافه شد، {skippedDuplicate} تکراری رد شد.";
        return RedirectToAction(nameof(Index));
    }
}
