using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class ManufacturerDataBookService : IManufacturerDataBookService
{
    private readonly PEMSDbContext _context;

    public ManufacturerDataBookService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<ManufacturerDataBookDto>> GetAllAsync(int? projectId = null, int? vendorId = null)
    {
        var query = _context.ManufacturerDataBooks
            .Include(x => x.Equipment)
            .Include(x => x.Vendor)
            .Include(x => x.Po)
            .Include(x => x.Project)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);
        if (vendorId.HasValue) query = query.Where(x => x.VendorId == vendorId.Value);

        var list = await query.OrderByDescending(x => x.CreatedDate).ToListAsync();

        return list.Select(MapHeader).ToList();
    }

    // طبق «PEMS — Global Grid Hardening» P1: جایگزین بارگذاری کامل GetAllAsync برای Index -
    // شمارش/مرتب‌سازی/صفحه‌بندی روی IQueryable قبل از Select انجام می‌شود، نه Include+ToListAsync
    // کامل. IndexOfContentsDueDate/IsIndexOverdue همان دو خاصیت [NotMapped] موجودند - EF Core
    // بدنه‌ی این Expression-bodied Property ها را داخل Select() به SQL ترجمه می‌کند (دقیقاً
    // همان الگوی اثبات‌شده در Inspection/MaintenancePlan این نشست).
    public async Task<PagedResult<ManufacturerDataBookDto>> GetPagedAsync(int page, int pageSize, int? projectId = null, int? vendorId = null)
    {
        var query = _context.ManufacturerDataBooks
            .AsNoTracking()
            .Where(x => x.IsActive)
            .AsQueryable();

        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);
        if (vendorId.HasValue) query = query.Where(x => x.VendorId == vendorId.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.MDBId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ManufacturerDataBookDto
            {
                MDBId = x.MDBId,
                EquipmentId = x.EquipmentId,
                EquipmentTagNumber = x.Equipment != null ? x.Equipment.TagNumber : "",
                VendorId = x.VendorId,
                VendorName = x.Vendor != null ? x.Vendor.Name : "",
                PoId = x.PoId,
                PoNumber = x.Po != null ? x.Po.PoNumber : null,
                ProjectId = x.ProjectId,
                ProjectName = x.Project != null ? x.Project.ProjectName : "",
                PlannedFinalIssueDate = x.PlannedFinalIssueDate,
                IndexOfContentsDueDate = x.IndexOfContentsDueDate,
                IndexOfContentsSubmittedDate = x.IndexOfContentsSubmittedDate,
                IsIndexOverdue = x.IsIndexOverdue,
                Status = x.Status,
                HardCopyCount = x.HardCopyCount,
                HardCopyShippedDate = x.HardCopyShippedDate,
                SoftCopyDeliveredDate = x.SoftCopyDeliveredDate,
                Notes = x.Notes
            })
            .ToListAsync();

        return new PagedResult<ManufacturerDataBookDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ManufacturerDataBookDto?> GetByIdAsync(int id)
    {
        var mdb = await _context.ManufacturerDataBooks
            .Include(x => x.Equipment)
            .Include(x => x.Vendor)
            .Include(x => x.Po)
            .Include(x => x.Project)
            .Include(x => x.Documents).ThenInclude(d => d.Document)
            .FirstOrDefaultAsync(x => x.MDBId == id);

        if (mdb == null) return null;

        var dto = MapHeader(mdb);

        dto.DocumentsPartA = mdb.Documents.Where(d => d.Part == "A").Select(MapDocument).ToList();
        dto.DocumentsPartB = mdb.Documents.Where(d => d.Part == "B").Select(MapDocument).ToList();
        dto.DocumentsPartC = mdb.Documents.Where(d => d.Part == "C").Select(MapDocument).ToList();

        return dto;
    }

    public async Task<int> CreateAsync(int equipmentId, int vendorId, int? poId, int projectId, DateTime? plannedFinalIssueDate)
    {
        var mdb = new ManufacturerDataBook
        {
            EquipmentId = equipmentId,
            VendorId = vendorId,
            PoId = poId,
            ProjectId = projectId,
            PlannedFinalIssueDate = plannedFinalIssueDate,
            Status = "Planned",
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.ManufacturerDataBooks.AddAsync(mdb);
        await _context.SaveChangesAsync();

        return mdb.MDBId;
    }

    public async Task AddDocumentAsync(int mdbId, string part, int? documentId, string title, int? dvdVolumeNo, bool isNotApplicable, string? notes)
    {
        string resolvedTitle = title;

        if (documentId.HasValue)
        {
            var doc = await _context.Documents.FirstOrDefaultAsync(x => x.DocumentId == documentId.Value);
            if (doc != null && string.IsNullOrWhiteSpace(title)) resolvedTitle = $"{doc.DocumentNo} - {doc.Title}";
        }

        await _context.MDBDocuments.AddAsync(new MDBDocument
        {
            MDBId = mdbId,
            Part = part,
            DocumentId = documentId,
            Title = resolvedTitle,
            DvdVolumeNo = dvdVolumeNo,
            IsNotApplicable = isNotApplicable,
            Notes = notes,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
    }

    public async Task RemoveDocumentAsync(int mdbDocumentId)
    {
        var doc = await _context.MDBDocuments.FirstOrDefaultAsync(x => x.MDBDocumentId == mdbDocumentId);
        if (doc == null) return;

        _context.MDBDocuments.Remove(doc);
        await _context.SaveChangesAsync();
    }

    public async Task MarkIndexSubmittedAsync(int mdbId)
    {
        var mdb = await _context.ManufacturerDataBooks.FirstOrDefaultAsync(x => x.MDBId == mdbId);
        if (mdb == null) return;

        mdb.IndexOfContentsSubmittedDate = DateTime.Now;
        if (mdb.Status == "Planned") mdb.Status = "IndexSubmitted";

        await _context.SaveChangesAsync();
    }

    public async Task MarkFinalIssuedAsync(int mdbId)
    {
        var mdb = await _context.ManufacturerDataBooks.FirstOrDefaultAsync(x => x.MDBId == mdbId);
        if (mdb == null) return;

        mdb.Status = "FinalIssued";
        await _context.SaveChangesAsync();
    }

    public async Task MarkHardCopyShippedAsync(int mdbId, int? hardCopyCount)
    {
        var mdb = await _context.ManufacturerDataBooks.FirstOrDefaultAsync(x => x.MDBId == mdbId);
        if (mdb == null) return;

        mdb.HardCopyShippedDate = DateTime.Now;
        if (hardCopyCount.HasValue) mdb.HardCopyCount = hardCopyCount.Value;

        await _context.SaveChangesAsync();
    }

    public async Task MarkSoftCopyDeliveredAsync(int mdbId)
    {
        var mdb = await _context.ManufacturerDataBooks.FirstOrDefaultAsync(x => x.MDBId == mdbId);
        if (mdb == null) return;

        mdb.SoftCopyDeliveredDate = DateTime.Now;
        if (mdb.HardCopyShippedDate.HasValue) mdb.Status = "Delivered";

        await _context.SaveChangesAsync();
    }

    private static ManufacturerDataBookDto MapHeader(ManufacturerDataBook mdb) => new()
    {
        MDBId = mdb.MDBId,
        EquipmentId = mdb.EquipmentId,
        EquipmentTagNumber = mdb.Equipment?.TagNumber ?? "",
        VendorId = mdb.VendorId,
        VendorName = mdb.Vendor?.Name ?? "",
        PoId = mdb.PoId,
        PoNumber = mdb.Po?.PoNumber,
        ProjectId = mdb.ProjectId,
        ProjectName = mdb.Project?.ProjectName ?? "",
        PlannedFinalIssueDate = mdb.PlannedFinalIssueDate,
        IndexOfContentsDueDate = mdb.IndexOfContentsDueDate,
        IndexOfContentsSubmittedDate = mdb.IndexOfContentsSubmittedDate,
        IsIndexOverdue = mdb.IsIndexOverdue,
        Status = mdb.Status,
        HardCopyCount = mdb.HardCopyCount,
        HardCopyShippedDate = mdb.HardCopyShippedDate,
        SoftCopyDeliveredDate = mdb.SoftCopyDeliveredDate,
        Notes = mdb.Notes
    };

    private static MDBDocumentDto MapDocument(MDBDocument d) => new()
    {
        MDBDocumentId = d.MDBDocumentId,
        Part = d.Part,
        DocumentId = d.DocumentId,
        DocumentNo = d.Document?.DocumentNo,
        Title = d.Title,
        DvdVolumeNo = d.DvdVolumeNo,
        IsNotApplicable = d.IsNotApplicable,
        Notes = d.Notes
    };
}
