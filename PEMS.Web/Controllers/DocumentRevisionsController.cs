using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Web.Controllers;

/// <summary>
/// مدیریت تاریخچه‌ی Revision های یک مدرک مهندسی (Document Revision).
/// </summary>
public class DocumentRevisionsController : Controller
{
    private readonly PEMSDbContext _context;

    public DocumentRevisionsController(PEMSDbContext context)
    {
        _context = context;
    }

    // طبق «PEMS — Global Grid Hardening» P1: قبلاً وقتی documentId انتخاب نشده بود (پیش‌فرض
    // ورود به صفحه)، کل جدول فعال DocumentRevisions با ۴ Include بدون Skip/Take بارگذاری
    // می‌شد. فیلتر documentId موجود حفظ شد؛ فقط شمارش/مرتب‌سازی/صفحه‌بندی/Projection در SQL
    // اضافه شدند.
    public async Task<IActionResult> Index(int? documentId, int page = 1, int? pageSize = null)
    {
        ViewData["Title"] = "Document Revision";
        ViewData["Section"] = "Engineering Management";

        var query = _context.DocumentRevisions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .AsQueryable();

        if (documentId.HasValue)
            query = query.Where(x => x.DocumentId == documentId.Value);

        var totalCount = await query.CountAsync();
        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);
        var normalizedPage = page < 1 ? 1 : page;

        var items = await query
            .OrderByDescending(x => x.RevisionDate)
            .ThenByDescending(x => x.RevisionId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new DocumentRevisionDto
            {
                RevisionId = x.RevisionId,
                DocumentId = x.DocumentId,
                DocumentNo = x.Document != null ? x.Document.DocumentNo : null,
                RevisionNo = x.RevisionNo,
                PreviousRevisionNo = x.PreviousRevisionNo,
                Status = x.Status,
                IssuePurpose = x.IssuePurpose,
                RevisionDate = x.RevisionDate,
                PreparedBy = x.PreparedBy != null ? x.PreparedBy.FirstName + " " + x.PreparedBy.LastName : "",
                CheckedBy = x.CheckedBy != null ? x.CheckedBy.FirstName + " " + x.CheckedBy.LastName : "",
                ApprovedBy = x.ApprovedBy != null ? x.ApprovedBy.FirstName + " " + x.ApprovedBy.LastName : ""
            })
            .ToListAsync();

        var result = new PagedResult<DocumentRevisionDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };

        ViewBag.DocumentId = documentId;
        ViewBag.Documents = await _context.EngineeringDocuments
            .OrderBy(x => x.DocumentNo)
            .Select(x => new { x.DocumentId, x.DocumentNo, x.Title })
            .ToListAsync();

        return View(result);
    }

    // طبق بررسی: این صفحه‌ی مستقل هیچ لینکی از جایی به آن اشاره نمی‌کرد (کاملاً یتیم بود) و
    // کل موتور گردش‌کار سه‌مرحله‌ای (Check/Approve/Superseded/CreatedBy) را دور می‌زد - کاربر
    // می‌توانست مستقیم Status را "Approved" بگذارد و RevisionNo دلخواه تایپ کند. مسیر درست و
    // امن همان مودال "Create New Revision" در صفحه‌ی جزئیات مدرک است.
    [HttpGet]
    public IActionResult Create(int documentId)
    {
        TempData["Message"] = "Use \"Create New Revision\" on the document's Details page - it properly numbers, tracks who created it, and supersedes the previous revision.";
        return RedirectToAction("Details", "EngineeringDocuments", new { id = documentId });
    }
}
