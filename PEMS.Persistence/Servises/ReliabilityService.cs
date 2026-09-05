using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق CMMS Production Baseline v1 - CMMS-06 (Reliability): FailureRecord منبع حقیقت واحد
/// خرابی‌هاست (همون الگویی که WorkOrderLifecycleService با SyncFailureRecordAsync برقرار می‌کنه).
/// این سرویس فقط محاسبه می‌کنه، هیچ داده‌ی جدیدی تولید نمی‌کنه.
/// </summary>
public class ReliabilityService : IReliabilityService
{
    private readonly PEMSDbContext _context;

    public ReliabilityService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<EquipmentReliabilityDto>> GetFleetReliabilityAsync()
    {
        var equipmentIds = await _context.FailureRecords
            .Where(x => x.IsActive)
            .Select(x => x.EquipmentId)
            .Distinct()
            .ToListAsync();

        var result = new List<EquipmentReliabilityDto>();
        foreach (var equipmentId in equipmentIds)
        {
            var dto = await GetEquipmentReliabilityAsync(equipmentId);
            if (dto != null) result.Add(dto);
        }

        // بدترین‌ها اول: کمترین Availability، بعد بیشترین تعداد خرابی
        return result
            .OrderBy(x => x.AvailabilityPercent ?? double.MaxValue)
            .ThenByDescending(x => x.FailureCount)
            .ToList();
    }

    public async Task<EquipmentReliabilityDto?> GetEquipmentReliabilityAsync(int equipmentId)
    {
        var equipment = await _context.Equipments
            .Where(x => x.EquipmentId == equipmentId)
            .Select(x => new { x.EquipmentId, x.TagNumber, x.EquipmentType })
            .FirstOrDefaultAsync();

        if (equipment == null) return null;

        var failures = await _context.FailureRecords
            .Where(x => x.EquipmentId == equipmentId && x.IsActive)
            .Include(x => x.WorkOrder)
            .OrderBy(x => x.FailureDate)
            .ToListAsync();

        var dto = new EquipmentReliabilityDto
        {
            EquipmentId = equipment.EquipmentId,
            TagNumber = equipment.TagNumber,
            EquipmentType = equipment.EquipmentType,
            FailureCount = failures.Count,
            FirstFailureDate = failures.Count > 0 ? failures[0].FailureDate : null,
            LastFailureDate = failures.Count > 0 ? failures[^1].FailureDate : null,
            TotalDowntimeHours = failures.Sum(x => x.DowntimeHours ?? 0),
            FailureHistory = failures.Select(x => new FailureHistoryItemDto
            {
                FailureRecordId = x.FailureRecordId,
                FailureDate = x.FailureDate,
                FailureMode = x.FailureMode,
                FailureCause = x.FailureCause,
                FailureEffect = x.FailureEffect,
                RootCause = x.RootCause,
                CorrectiveAction = x.CorrectiveAction,
                DowntimeHours = x.DowntimeHours,
                WorkOrderId = x.WorkOrderId,
                WorkOrderNumber = x.WorkOrder?.WorkOrderNumber
            }).ToList()
        };

        // MTBF: نیاز به حداقل ۲ خرابی برای فاصله‌ی زمانی معنادار
        if (failures.Count >= 2)
        {
            var totalSpanHours = (dto.LastFailureDate!.Value - dto.FirstFailureDate!.Value).TotalHours;
            dto.MTBFHours = totalSpanHours / (failures.Count - 1);
        }

        // MTTR: میانگین مدت تعمیر واقعی (StartDate تا CompletionDate) دستورکارهای مرتبط
        var repairDurations = failures
            .Where(x => x.WorkOrder?.StartDate != null && x.WorkOrder.CompletionDate != null)
            .Select(x => (x.WorkOrder!.CompletionDate!.Value - x.WorkOrder.StartDate!.Value).TotalHours)
            .ToList();

        if (repairDurations.Count > 0)
            dto.MTTRHours = repairDurations.Average();

        // Availability = MTBF / (MTBF + MTTR) - فرمول استاندارد Inherent Availability
        if (dto.MTBFHours.HasValue && dto.MTTRHours.HasValue && (dto.MTBFHours + dto.MTTRHours) > 0)
            dto.AvailabilityPercent = dto.MTBFHours.Value / (dto.MTBFHours.Value + dto.MTTRHours.Value) * 100;

        return dto;
    }
}
