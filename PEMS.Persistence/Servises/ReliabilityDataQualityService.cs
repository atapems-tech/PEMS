using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 5» بند ۹/۳۵/۷۳: همه‌ی کوئری‌ها SQL-Aggregated هستند (COUNT/GROUP BY در
/// دیتابیس) - هرگز کل جدول FailureRecords/WorkOrders را به حافظه بارگذاری نمی‌کند.
/// </summary>
public class ReliabilityDataQualityService : IReliabilityDataQualityService
{
    private readonly PEMSDbContext _context;

    public ReliabilityDataQualityService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<ReliabilityDataQualityDto> GetDataQualityAsync()
    {
        var dto = new ReliabilityDataQualityDto();

        dto.TotalActiveEquipment = await _context.Equipments.CountAsync(x => x.IsActive);
        dto.EquipmentWithFailureHistory = await _context.FailureRecords
            .Where(x => x.IsActive)
            .Select(x => x.EquipmentId)
            .Distinct()
            .CountAsync();

        dto.TotalFailureRecords = await _context.FailureRecords.CountAsync(x => x.IsActive);
        dto.FailureRecordsWithoutFailureMode = await _context.FailureRecords.CountAsync(x => x.IsActive && x.FailureModeId == null);
        dto.FailureRecordsWithoutFailureCause = await _context.FailureRecords.CountAsync(x => x.IsActive && x.FailureCauseId == null);
        dto.FailureRecordsWithoutDowntimeHours = await _context.FailureRecords.CountAsync(x => x.IsActive && x.DowntimeHours == null);

        // طبق بند ۹: تاریخ نامعتبر = در آینده (هنوز رخ نداده) - نه یک محدوده‌ی دلبخواهی گذشته
        var now = DateTime.Now;
        dto.FailureRecordsWithInvalidDate = await _context.FailureRecords.CountAsync(x => x.IsActive && x.FailureDate > now);

        dto.FailureRecordsLinkedToWorkOrderMissingDuration = await _context.FailureRecords
            .Where(x => x.IsActive && x.WorkOrderId != null)
            .Where(x => x.WorkOrder!.StartDate == null || x.WorkOrder.CompletionDate == null)
            .CountAsync();

        dto.FailureRecordsWithNegativeDuration = await _context.FailureRecords
            .Where(x => x.IsActive && x.WorkOrderId != null && x.WorkOrder!.StartDate != null && x.WorkOrder.CompletionDate != null)
            .Where(x => x.WorkOrder!.CompletionDate!.Value < x.WorkOrder.StartDate!.Value)
            .CountAsync();

        dto.EquipmentReliabilityCoveragePercent = dto.TotalActiveEquipment == 0 ? 0
            : Math.Round(dto.EquipmentWithFailureHistory * 100.0 / dto.TotalActiveEquipment, 2);

        dto.FailureTaxonomyCoveragePercent = dto.TotalFailureRecords == 0 ? 0
            : Math.Round((dto.TotalFailureRecords - dto.FailureRecordsWithoutFailureMode) * 100.0 / dto.TotalFailureRecords, 2);

        dto.RepairDataCoveragePercent = dto.TotalFailureRecords == 0 ? 0
            : Math.Round((dto.TotalFailureRecords - dto.FailureRecordsLinkedToWorkOrderMissingDuration) * 100.0 / dto.TotalFailureRecords, 2);

        // ==============================================================================
        // طبق «PEMS Phase 7» بند ۲۱: گسترش گیت کیفیت داده - پوشش زنجیره‌ی نگهداری فراتر از
        // خودِ FailureRecord. همه SQL-Aggregated (COUNT/GROUP BY در دیتابیس).
        // ==============================================================================
        dto.TotalWorkOrders = await _context.WorkOrders.CountAsync(x => x.IsActive);
        dto.WorkOrdersMissingWorkOrderType = await _context.WorkOrders.CountAsync(x => x.IsActive && string.IsNullOrEmpty(x.WorkOrderType));
        dto.CompletedOrClosedWorkOrders = await _context.WorkOrders.CountAsync(x => x.IsActive && (x.Status == "Completed" || x.Status == "Closed"));
        dto.WorkOrdersMissingCompletionDate = await _context.WorkOrders.CountAsync(x =>
            x.IsActive && (x.Status == "Completed" || x.Status == "Closed") && x.CompletionDate == null);

        dto.EquipmentWithCriticality = await _context.EquipmentCriticalities.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.EquipmentId)
            .Distinct()
            .CountAsync();
        dto.EquipmentWithMaintenanceStrategy = await _context.MaintenanceStrategies.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.EquipmentId)
            .Distinct()
            .CountAsync();
        dto.EquipmentWithMaintenancePlan = await _context.MaintenancePlans.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.EquipmentId)
            .Distinct()
            .CountAsync();

        dto.MaintenanceTypeCompletenessPercent = dto.TotalWorkOrders == 0 ? 0
            : Math.Round((dto.TotalWorkOrders - dto.WorkOrdersMissingWorkOrderType) * 100.0 / dto.TotalWorkOrders, 2);
        dto.WorkOrderCompletionCoveragePercent = dto.CompletedOrClosedWorkOrders == 0 ? 0
            : Math.Round((dto.CompletedOrClosedWorkOrders - dto.WorkOrdersMissingCompletionDate) * 100.0 / dto.CompletedOrClosedWorkOrders, 2);
        dto.CriticalityCoveragePercent = dto.TotalActiveEquipment == 0 ? 0
            : Math.Round(dto.EquipmentWithCriticality * 100.0 / dto.TotalActiveEquipment, 2);
        dto.MaintenanceStrategyCoveragePercent = dto.TotalActiveEquipment == 0 ? 0
            : Math.Round(dto.EquipmentWithMaintenanceStrategy * 100.0 / dto.TotalActiveEquipment, 2);
        dto.MaintenancePlanCoveragePercent = dto.TotalActiveEquipment == 0 ? 0
            : Math.Round(dto.EquipmentWithMaintenancePlan * 100.0 / dto.TotalActiveEquipment, 2);

        return dto;
    }
}
