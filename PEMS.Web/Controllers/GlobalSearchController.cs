using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Controllers;

/// <summary>
/// جستجوی سراسری واقعی برای نوار بالای همه‌ی صفحات - طبق بند ۶.۱ گزارش ممیزی فیلترینگ
/// ("جعبه‌ی جستجوی سراسری تزئینی است و هیچ کاری نمی‌کند"). یک جستجوی چندجدولی ساده روی
/// مهم‌ترین موجودیت‌ها - نه Semantic، همان چیزی که کاربر از یک جعبه‌ی جستجوی بالای صفحه انتظار دارد.
///
/// طبق «PEMS Help System — Phase 2» بخش ۹: بخش راهنما به‌جای نوشتن یک Query خام جدید مثل
/// بقیه‌ی بخش‌ها، مستقیماً از IHelpService.SearchAsync موجود (همان سرویسی که خودِ Help Center
/// استفاده می‌کند) استفاده می‌کند - تا منطق جستجوی دوزبانه/Published-only یک‌بار پیاده‌سازی
/// شده باقی بماند، نه دو بار.
/// </summary>
public class GlobalSearchController : Controller
{
    private readonly PEMSDbContext _context;
    private readonly IHelpService _helpService;

    public GlobalSearchController(PEMSDbContext context, IHelpService helpService)
    {
        _context = context;
        _helpService = helpService;
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

        var currentLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa" ? "fa" : "en";
        var helpResults = await _helpService.SearchAsync(term, currentLanguage, page: 1, pageSize: 10);
        var helpItems = helpResults
            .Select(h => new GlobalSearchItem
            {
                Id = h.HelpArticleId,
                Title = h.Title,
                SubTitle = h.CategoryName,
                Url = Url.Action("Article", "Help", new { code = h.Code })
            })
            .ToList();

        return View(new GlobalSearchResults
        {
            Documents = documents,
            VendorDocuments = vdrItems,
            Equipment = equipment,
            Vendors = vendors,
            Letters = letters,
            Projects = projects,
            Permits = permits,
            WorkOrders = workOrders,
            HelpArticles = helpItems
        });
    }
}

public class GlobalSearchItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? SubTitle { get; set; }

    // طبق «PEMS Help System — Phase 2»: مقالات راهنما با Code (رشته‌ای) مسیریابی می‌شوند نه Id
    // عددی - این فیلد اختیاری، فقط وقتی پر شود، جایگزین لینک پیش‌فرض asp-controller/asp-action
    // می‌شود؛ برای بقیه‌ی موجودیت‌ها Null می‌ماند و رفتار موجود کاملاً دست‌نخورده می‌ماند.
    public string? Url { get; set; }
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
    public List<GlobalSearchItem> HelpArticles { get; set; } = new();

    public int TotalCount => Documents.Count + VendorDocuments.Count + Equipment.Count + Vendors.Count + Letters.Count + Projects.Count + Permits.Count + WorkOrders.Count + HelpArticles.Count;
}
