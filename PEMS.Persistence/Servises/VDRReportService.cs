using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class VDRReportService : IVDRReportService
{
    private readonly PEMSDbContext _context;

    public VDRReportService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<VDRReportDto> GetReportAsync(int? projectId = null, int? disciplineId = null,
        DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.VDRItems
            .AsNoTracking()
            .Include(x => x.Vendor)
            .Include(x => x.Submissions)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);
        if (disciplineId.HasValue) query = query.Where(x => x.DisciplineId == disciplineId.Value);
        if (fromDate.HasValue) query = query.Where(x => x.CreatedDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.CreatedDate <= toDate.Value);

        var items = await query.ToListAsync();
        var allSubmissions = items.SelectMany(x => x.Submissions).ToList();

        var report = new VDRReportDto
        {
            TotalItems = items.Count,
            ClosedCount = items.Count(x => x.WorkflowStatus == "Closed"),
            UnderReviewCount = items.Count(x => x.WorkflowStatus == "UnderReview"),
            PendingResubmissionCount = items.Count(x => x.WorkflowStatus == "PendingResubmission"),
            OverdueCount = items.Count(x => VDRService.IsOverdue(x)),
            TotalSubmissions = allSubmissions.Count
        };

        var months = Enumerable.Range(0, 12)
            .Select(i => DateTime.Now.AddMonths(-11 + i))
            .Select(d => new DateTime(d.Year, d.Month, 1))
            .ToList();

        report.SubmissionsByMonth = months.Select(m => (
            Month: m.ToString("yyyy-MM"),
            Count: allSubmissions.Count(s => s.VendorActualIssuedDate.HasValue
                && s.VendorActualIssuedDate.Value.Year == m.Year && s.VendorActualIssuedDate.Value.Month == m.Month)
        )).ToList();

        report.ByVendor = items
            .Where(x => x.Vendor != null)
            .GroupBy(x => x.Vendor!.Name)
            .Select(g => (Vendor: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        report.ByReviewCategory = allSubmissions
            .Where(x => !string.IsNullOrWhiteSpace(x.ReviewCategory))
            .GroupBy(x => x.ReviewCategory!)
            .Select(g => (Category: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        report.ByWorkflowStatus = items
            .GroupBy(x => x.WorkflowStatus ?? "Unknown")
            .Select(g => (Status: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        return report;
    }
}
