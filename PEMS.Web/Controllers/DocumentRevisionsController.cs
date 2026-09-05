using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public async Task<IActionResult> Index(int? documentId)
    {
        ViewData["Title"] = "Document Revision";
        ViewData["Section"] = "Engineering Management";

        var query = _context.DocumentRevisions
            .Include(x => x.Document)
            .Include(x => x.PreparedBy)
            .Include(x => x.CheckedBy)
            .Include(x => x.ApprovedBy)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (documentId.HasValue)
            query = query.Where(x => x.DocumentId == documentId.Value);

        var revisions = await query.OrderByDescending(x => x.RevisionDate).ToListAsync();

        ViewBag.DocumentId = documentId;
        ViewBag.Documents = await _context.EngineeringDocuments
            .OrderBy(x => x.DocumentNo)
            .Select(x => new { x.DocumentId, x.DocumentNo, x.Title })
            .ToListAsync();

        return View(revisions);
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
