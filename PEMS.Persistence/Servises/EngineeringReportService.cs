using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class EngineeringReportService : IEngineeringReportService
{
    private readonly PEMSDbContext _context;

    public EngineeringReportService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<EngineeringReportDto> GetReportAsync(int? projectId = null, int? disciplineId = null,
        DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.EngineeringDocuments
            .AsNoTracking()
            .Include(x => x.Discipline)
            .Include(x => x.Project)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);
        if (disciplineId.HasValue) query = query.Where(x => x.DisciplineId == disciplineId.Value);
        if (fromDate.HasValue) query = query.Where(x => x.CreatedDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.CreatedDate <= toDate.Value);

        var documents = await query.ToListAsync();
        var documentIds = documents.Select(x => x.DocumentId).ToList();

        var revisions = await _context.DocumentRevisions
            .Where(x => documentIds.Contains(x.DocumentId))
            .ToListAsync();

        var report = new EngineeringReportDto
        {
            TotalDocuments = documents.Count,
            ApprovedCount = documents.Count(x => x.Status == "Approved" || x.Status == "Issued"),
            PendingCount = documents.Count(x => x.Status == "Submitted" || x.Status == "Checked"),
            RejectedCount = documents.Count(x => x.Status == "Rejected"),
            IssuedCount = documents.Count(x => x.Status == "Issued"),
            TotalRevisions = revisions.Count,
            OverdueCount = documents.Count(x => x.PlannedDate.HasValue && !x.ActualDate.HasValue && x.PlannedDate.Value < DateTime.Now)
        };

        var approvedWithDates = documents.Where(x => x.ApprovedDate.HasValue).ToList();
        report.AvgDaysToApprove = approvedWithDates.Any()
            ? approvedWithDates.Average(x => (x.ApprovedDate!.Value - x.CreatedDate).TotalDays)
            : null;

        var months = Enumerable.Range(0, 12)
            .Select(i => DateTime.Now.AddMonths(-11 + i))
            .Select(d => new DateTime(d.Year, d.Month, 1))
            .ToList();

        report.VolumeByMonth = months.Select(m => (
            Month: m.ToString("yyyy-MM"),
            Created: documents.Count(x => x.CreatedDate.Year == m.Year && x.CreatedDate.Month == m.Month),
            Issued: revisions.Count(x => x.IssuedDate.HasValue && x.IssuedDate.Value.Year == m.Year && x.IssuedDate.Value.Month == m.Month)
        )).ToList();

        report.ByDiscipline = documents
            .Where(x => x.Discipline != null)
            .GroupBy(x => x.Discipline!.Name)
            .Select(g => (Discipline: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        report.ByStatus = documents
            .GroupBy(x => x.Status)
            .Select(g => (Status: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        report.ByProject = documents
            .Where(x => x.Project != null)
            .GroupBy(x => x.Project!.ProjectName)
            .Select(g => (ProjectName: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        return report;
    }
}
