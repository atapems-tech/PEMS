using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// گزارش تدارکات - طبق درخواست: هر بخش گزارش‌گیری با فیلترهای گوناگون مخصوص خودش.
/// </summary>
public class ProcurementReportService : IProcurementReportService
{
    private readonly PEMSDbContext _context;

    public ProcurementReportService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<ProcurementReportDto> GetReportAsync(int? projectId = null, int? disciplineId = null,
        DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.ProcurementPackages
            .AsNoTracking()
            .Include(x => x.Discipline)
            .Include(x => x.MaterialRequisitions)
            .Include(x => x.Quotations)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);
        if (disciplineId.HasValue) query = query.Where(x => x.DisciplineId == disciplineId.Value);
        if (fromDate.HasValue) query = query.Where(x => x.CreatedDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.CreatedDate <= toDate.Value);

        var packages = await query.ToListAsync();

        var report = new ProcurementReportDto
        {
            TotalPackages = packages.Count,
            TotalMRs = packages.Sum(x => x.MaterialRequisitions.Count),
            TotalMRQs = packages.Sum(x => x.Quotations.Count),
            OpenCount = packages.Count(x => x.Status != "Closed"),
            ClosedCount = packages.Count(x => x.Status == "Closed")
        };

        var months = Enumerable.Range(0, 12)
            .Select(i => DateTime.Now.AddMonths(-11 + i))
            .Select(d => new DateTime(d.Year, d.Month, 1))
            .ToList();

        report.VolumeByMonth = months.Select(m => (
            Month: m.ToString("yyyy-MM"),
            Count: packages.Count(x => x.CreatedDate.Year == m.Year && x.CreatedDate.Month == m.Month)
        )).ToList();

        report.ByStatus = packages
            .GroupBy(x => x.Status)
            .Select(g => (Status: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        report.ByVendor = packages
            .SelectMany(x => x.Quotations)
            .Where(q => !string.IsNullOrWhiteSpace(q.VendorName))
            .GroupBy(q => q.VendorName!)
            .Select(g => (Vendor: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        report.ByDiscipline = packages
            .Where(x => x.Discipline != null)
            .GroupBy(x => x.Discipline!.Name)
            .Select(g => (Discipline: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        return report;
    }
}
