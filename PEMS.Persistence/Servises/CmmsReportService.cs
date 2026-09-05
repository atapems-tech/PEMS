using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// گزارش CMMS - طبق درخواست: هر بخش گزارش‌گیری با فیلترهای گوناگون مخصوص خودش.
/// </summary>
public class CmmsReportService : ICmmsReportService
{
    private readonly PEMSDbContext _context;

    public CmmsReportService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<CmmsReportDto> GetReportAsync(string? status = null, string? priority = null,
        DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.WorkOrders
            .AsNoTracking()
            .Include(x => x.Equipment)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(priority)) query = query.Where(x => x.Priority == priority);
        if (fromDate.HasValue) query = query.Where(x => x.CreatedDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.CreatedDate <= toDate.Value);

        var workOrders = await query.ToListAsync();
        var workOrderIds = workOrders.Select(x => x.WorkOrderId).ToList();

        var spareUsages = await _context.MaintenanceSpareUsages
            .Where(x => workOrderIds.Contains(x.WorkOrderId) && x.IsActive)
            .ToListAsync();

        var report = new CmmsReportDto
        {
            TotalWorkOrders = workOrders.Count,
            OpenCount = workOrders.Count(x => x.Status == "Open"),
            InProgressCount = workOrders.Count(x => x.Status == "InProgress" || x.Status == "Assigned"),
            CompletedCount = workOrders.Count(x => x.Status == "Completed" || x.Status == "Closed"),
            CriticalCount = workOrders.Count(x => x.Priority == "Critical"),
            TotalSpareCost = spareUsages.Sum(x => x.TotalCost ?? 0)
        };

        var completedWithDates = workOrders.Where(x => x.CompletionDate.HasValue).ToList();
        report.AvgCompletionDays = completedWithDates.Any()
            ? completedWithDates.Average(x => (x.CompletionDate!.Value - x.CreatedDate).TotalDays)
            : null;

        var months = Enumerable.Range(0, 12)
            .Select(i => DateTime.Now.AddMonths(-11 + i))
            .Select(d => new DateTime(d.Year, d.Month, 1))
            .ToList();

        report.VolumeByMonth = months.Select(m => (
            Month: m.ToString("yyyy-MM"),
            Count: workOrders.Count(x => x.CreatedDate.Year == m.Year && x.CreatedDate.Month == m.Month)
        )).ToList();

        report.ByType = workOrders
            .GroupBy(x => x.WorkOrderType)
            .Select(g => (Type: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        report.ByPriority = workOrders
            .GroupBy(x => x.Priority)
            .Select(g => (Priority: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        report.TopEquipment = workOrders
            .Where(x => x.Equipment != null)
            .GroupBy(x => x.Equipment!.TagNumber)
            .Select(g => (Equipment: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        return report;
    }
}
