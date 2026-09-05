using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق CMMS Production Baseline v1 - CMMS-08 (Cost &amp; KPI): از روی داده‌ی واقعی موجود
/// (MaintenanceHistory.Cost که خودش از WorkOrderLabor/MaintenanceSpareUsage/WorkOrder.ContractorCost
/// مشتق شده) محاسبه می‌شود - هیچ منبع داده‌ی جدید موازی ساخته نمی‌شود.
/// </summary>
public class KpiService : IKpiService
{
    private readonly PEMSDbContext _context;

    private static readonly string[] BacklogStatuses = { "Open", "Assigned", "InProgress", "OnHold" };

    public KpiService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<CostKpiDto> GetCostKpisAsync(DateTime? fromDate, DateTime? toDate)
    {
        var query = _context.MaintenanceHistories
            .Where(x => x.IsActive)
            .Include(x => x.WorkOrder)
            .Include(x => x.Equipment).ThenInclude(e => e!.FunctionalLocationRef)
            .AsQueryable();

        if (fromDate.HasValue) query = query.Where(x => x.ExecutionDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.ExecutionDate <= toDate.Value);

        var histories = await query.ToListAsync();
        var workOrderIds = histories.Where(x => x.WorkOrderId.HasValue).Select(x => x.WorkOrderId!.Value).ToList();

        var laborCost = await _context.WorkOrderLabors
            .Where(x => x.IsActive && workOrderIds.Contains(x.WorkOrderId))
            .SumAsync(x => (decimal?)x.LaborCost) ?? 0;

        var materialCost = await _context.MaintenanceSpareUsages
            .Where(x => x.IsActive && workOrderIds.Contains(x.WorkOrderId))
            .SumAsync(x => (decimal?)x.TotalCost) ?? 0;

        var contractorCost = histories.Sum(x => x.WorkOrder?.ContractorCost ?? 0);

        return new CostKpiDto
        {
            TotalCost = histories.Sum(x => x.Cost ?? 0),
            LaborCost = laborCost,
            MaterialCost = materialCost,
            ContractorCost = contractorCost,
            TopEquipmentByCost = histories
                .GroupBy(x => new { x.EquipmentId, TagNumber = x.Equipment?.TagNumber ?? "?" })
                .Select(g => new CostByEquipmentItem { EquipmentId = g.Key.EquipmentId, TagNumber = g.Key.TagNumber, Cost = g.Sum(x => x.Cost ?? 0) })
                .OrderByDescending(x => x.Cost)
                .Take(10)
                .ToList(),
            CostByFunctionalLocation = histories
                .GroupBy(x => x.Equipment?.FunctionalLocationRef?.Code ?? "Unassigned")
                .Select(g => new CostByGroupItem { GroupName = g.Key, Cost = g.Sum(x => x.Cost ?? 0) })
                .OrderByDescending(x => x.Cost)
                .ToList(),
            CostByMaintenanceType = histories
                .GroupBy(x => x.WorkOrder?.WorkOrderType ?? "Unknown")
                .Select(g => new CostByGroupItem { GroupName = g.Key, Cost = g.Sum(x => x.Cost ?? 0) })
                .OrderByDescending(x => x.Cost)
                .ToList()
        };
    }

    public async Task<MaintenanceKpiDto> GetMaintenanceKpisAsync(DateTime? fromDate, DateTime? toDate)
    {
        var activePlans = await _context.MaintenancePlans
            .Where(x => x.IsActive && x.NextDueDate.HasValue)
            .Select(x => x.NextDueDate!.Value)
            .ToListAsync();

        var pmOverdue = activePlans.Count(d => d.Date < DateTime.Today);
        var pmActive = activePlans.Count;

        var backlogWorkOrders = await _context.WorkOrders
            .Where(x => BacklogStatuses.Contains(x.Status))
            .Select(x => new { x.WorkOrderId, x.CreatedDate })
            .ToListAsync();

        var backlogIds = backlogWorkOrders.Select(x => x.WorkOrderId).ToList();

        var backlogHours = await _context.RequiredSkills
            .Where(x => backlogIds.Contains(x.WorkOrderId))
            .SumAsync(x => (decimal?)x.PlannedHours) ?? 0;

        var aging = backlogWorkOrders.Count > 0
            ? backlogWorkOrders.Average(x => (DateTime.Today - x.CreatedDate.Date).TotalDays)
            : (double?)null;

        var periodQuery = _context.WorkOrders.AsQueryable();
        if (fromDate.HasValue) periodQuery = periodQuery.Where(x => x.CreatedDate >= fromDate.Value);
        if (toDate.HasValue) periodQuery = periodQuery.Where(x => x.CreatedDate <= toDate.Value);

        var periodWorkOrders = await periodQuery.Select(x => new { x.WorkOrderType, x.Status }).ToListAsync();
        var totalInPeriod = periodWorkOrders.Count;

        double PercentOf(Func<string, bool> typePredicate) =>
            totalInPeriod == 0 ? 0 : periodWorkOrders.Count(x => typePredicate(x.WorkOrderType)) * 100.0 / totalInPeriod;

        var completionRate = totalInPeriod == 0
            ? (double?)null
            : periodWorkOrders.Count(x => x.Status is "Completed" or "Closed") * 100.0 / totalInPeriod;

        return new MaintenanceKpiDto
        {
            PmActivePlanCount = pmActive,
            PmOverdueCount = pmOverdue,
            PmCompliancePercent = pmActive == 0 ? null : (pmActive - pmOverdue) * 100.0 / pmActive,
            BacklogCount = backlogWorkOrders.Count,
            BacklogHours = backlogHours,
            PlannedPercent = PercentOf(t => t == "Preventive"),
            UnplannedPercent = PercentOf(t => t == "Corrective"),
            EmergencyPercent = PercentOf(t => t == "Emergency"),
            WorkOrderAgingDays = aging,
            WorkOrderCompletionRatePercent = completionRate,
            TotalWorkOrdersInPeriod = totalInPeriod
        };
    }
}
