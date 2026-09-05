using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Context;

namespace PEMS.Web.Controllers;

/// <summary>
/// جستجوی سراسری واقعی برای نوار بالای همه‌ی صفحات - طبق بند ۶.۱ گزارش ممیزی فیلترینگ
/// ("جعبه‌ی جستجوی سراسری تزئینی است و هیچ کاری نمی‌کند"). یک جستجوی چندجدولی ساده روی
/// مهم‌ترین موجودیت‌ها - نه Semantic، همان چیزی که کاربر از یک جعبه‌ی جستجوی بالای صفحه انتظار دارد.
/// </summary>
public class GlobalSearchController : Controller
{
    private readonly PEMSDbContext _context;

    public GlobalSearchController(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? q)
    {
        ViewData["Title"] = "Global Search";
        ViewData["Section"] = "";

        ViewBag.Query = q;

        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            return View(new GlobalSearchResults());
        }

        var term = q.Trim();

        var documents = await _context.EngineeringDocuments
            .Where(x => x.IsActive && (x.DocumentNo.Contains(term) || x.Title.Contains(term)))
            .OrderByDescending(x => x.CreatedDate)
            .Take(15)
            .Select(x => new GlobalSearchItem { Id = x.DocumentId, Title = x.DocumentNo + " - " + x.Title, SubTitle = x.Status })
            .ToListAsync();

        var vdrItems = await _context.VDRItems
            .Where(x => x.IsActive && (x.DocumentNumber.Contains(term) || x.DocumentDescription!.Contains(term)))
            .OrderByDescending(x => x.CreatedDate)
            .Take(15)
            .Select(x => new GlobalSearchItem { Id = x.DocumentId, Title = x.DocumentNumber + " - " + x.DocumentDescription, SubTitle = x.WorkflowStatus })
            .ToListAsync();

        var equipment = await _context.Equipments
            .Where(x => x.IsActive && (x.TagNumber.Contains(term) || x.Name.Contains(term)))
            .Take(15)
            .Select(x => new GlobalSearchItem { Id = x.EquipmentId, Title = x.TagNumber + " - " + x.Name, SubTitle = x.EquipmentType })
            .ToListAsync();

        var vendors = await _context.Vendors
            .Where(x => x.IsActive && x.Name.Contains(term))
            .Take(15)
            .Select(x => new GlobalSearchItem { Id = x.VendorId, Title = x.Name, SubTitle = x.RiskLevel })
            .ToListAsync();

        var letters = await _context.Letters
            .Where(x => x.IsActive && (x.LetterNumber.Contains(term) || x.Subject.Contains(term)))
            .OrderByDescending(x => x.LetterDate)
            .Take(15)
            .Select(x => new GlobalSearchItem { Id = x.LetterId, Title = x.LetterNumber + " - " + x.Subject, SubTitle = x.Status })
            .ToListAsync();

        var projects = await _context.Projects
            .Where(x => x.IsActive && (x.ProjectCode.Contains(term) || x.ProjectName.Contains(term)))
            .Take(15)
            .Select(x => new GlobalSearchItem { Id = x.ProjectId, Title = x.ProjectCode + " - " + x.ProjectName, SubTitle = x.Status })
            .ToListAsync();

        // طبق PEMS-UI-01 (UI01-004): جستجوی سراسری قبلاً Permit/WorkOrder را نمی‌یافت -
        // دقیقاً همون موجودیت‌هایی که این ممیزی ایمنی-محور می‌داند.
        var permits = await _context.Permits
            .Where(x => x.IsActive && (x.PermitNumber.Contains(term) || x.Title.Contains(term)))
            .OrderByDescending(x => x.CreatedDate)
            .Take(15)
            .Select(x => new GlobalSearchItem { Id = x.PermitId, Title = x.PermitNumber + " - " + x.Title, SubTitle = x.Status })
            .ToListAsync();

        var workOrders = await _context.WorkOrders
            .Where(x => x.IsActive && (x.WorkOrderNumber.Contains(term) || (x.Description != null && x.Description.Contains(term))))
            .OrderByDescending(x => x.CreatedDate)
            .Take(15)
            .Select(x => new GlobalSearchItem { Id = x.WorkOrderId, Title = x.WorkOrderNumber + " - " + x.Description, SubTitle = x.Status })
            .ToListAsync();

        return View(new GlobalSearchResults
        {
            Documents = documents,
            VendorDocuments = vdrItems,
            Equipment = equipment,
            Vendors = vendors,
            Letters = letters,
            Projects = projects,
            Permits = permits,
            WorkOrders = workOrders
        });
    }
}

public class GlobalSearchItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? SubTitle { get; set; }
}

public class GlobalSearchResults
{
    public List<GlobalSearchItem> Documents { get; set; } = new();
    public List<GlobalSearchItem> VendorDocuments { get; set; } = new();
    public List<GlobalSearchItem> Equipment { get; set; } = new();
    public List<GlobalSearchItem> Vendors { get; set; } = new();
    public List<GlobalSearchItem> Letters { get; set; } = new();
    public List<GlobalSearchItem> Projects { get; set; } = new();
    public List<GlobalSearchItem> Permits { get; set; } = new();
    public List<GlobalSearchItem> WorkOrders { get; set; } = new();

    public int TotalCount => Documents.Count + VendorDocuments.Count + Equipment.Count + Vendors.Count + Letters.Count + Projects.Count + Permits.Count + WorkOrders.Count;
}
