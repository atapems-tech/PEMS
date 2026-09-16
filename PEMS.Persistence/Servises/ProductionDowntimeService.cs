using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۷: رویداد واقعی توقف - جدا از
/// WorkOrder.DowntimeHours/FailureRecord.DowntimeHours (که این سرویس هرگز آن‌ها را
/// نمی‌خواند/بازنویسی نمی‌کند). Duration همیشه از StartTime/EndTime مشتق می‌شود، هرگز ذخیره
/// نمی‌شود (طبق کامنت Entity).
/// </summary>
public class ProductionDowntimeService : IProductionDowntimeService
{
    private readonly PEMSDbContext _context;

    // Equipment, Process, Material, Utility, Planned, Other - فهرست کنترل‌شده (هم‌الگو با
    // ProductionRunService.LossCategories)
    public static readonly string[] CauseCategories = { "Equipment", "Process", "Material", "Utility", "Planned", "Other" };

    public ProductionDowntimeService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductionDowntimeEventDto>> GetForRunAsync(int productionRunId)
    {
        return await _context.ProductionDowntimeEvents
            .Include(x => x.ProductionRun)
            .Include(x => x.ProductionWorkCenter)
            .Include(x => x.Equipment)
            .Where(x => x.ProductionRunId == productionRunId)
            .OrderByDescending(x => x.StartTime)
            .Select(x => new ProductionDowntimeEventDto
            {
                ProductionDowntimeEventId = x.ProductionDowntimeEventId,
                ProductionRunId = x.ProductionRunId,
                RunNumber = x.ProductionRun != null ? x.ProductionRun.RunNumber : "",
                ProductionWorkCenterId = x.ProductionWorkCenterId,
                WorkCenterCode = x.ProductionWorkCenter != null ? x.ProductionWorkCenter.Code : "",
                EquipmentId = x.EquipmentId,
                EquipmentTagNumber = x.Equipment != null ? x.Equipment.TagNumber : null,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                CauseCategory = x.CauseCategory,
                Description = x.Description,
                SourceWorkRequestId = x.SourceWorkRequestId,
                SourceWorkOrderId = x.SourceWorkOrderId
            }).ToListAsync();
    }

    public async Task<(bool Success, string? Error, int? ProductionDowntimeEventId)> StartAsync(
        int productionRunId, int productionWorkCenterId, int? equipmentId, DateTime startTime,
        string causeCategory, string? description, int? sourceWorkRequestId, int? sourceWorkOrderId,
        int? createdByEmployeeId)
    {
        if (!CauseCategories.Contains(causeCategory)) return (false, "دسته‌بندی علت توقف نامعتبر است.", null);

        var runExists = await _context.ProductionRuns.AnyAsync(x => x.ProductionRunId == productionRunId);
        if (!runExists) return (false, "اجرای تولید یافت نشد.", null);

        var workCenterExists = await _context.ProductionWorkCenters.AnyAsync(x => x.ProductionWorkCenterId == productionWorkCenterId && x.IsActive);
        if (!workCenterExists) return (false, "مرکز کاری انتخاب‌شده معتبر نیست.", null);

        // طبق بخش ۷ سند فاز: EquipmentId عمداً اختیاری است - اعتبارسنجی فقط وقتی داده شده باشد
        if (equipmentId.HasValue)
        {
            var equipmentExists = await _context.Equipments.AnyAsync(x => x.EquipmentId == equipmentId.Value && x.IsActive);
            if (!equipmentExists) return (false, "تجهیز انتخاب‌شده معتبر نیست.", null);
        }

        if (sourceWorkRequestId.HasValue)
        {
            var wrExists = await _context.WorkRequests.AnyAsync(x => x.WorkRequestId == sourceWorkRequestId.Value);
            if (!wrExists) return (false, "درخواست کار انتخاب‌شده معتبر نیست.", null);
        }

        if (sourceWorkOrderId.HasValue)
        {
            var woExists = await _context.WorkOrders.AnyAsync(x => x.WorkOrderId == sourceWorkOrderId.Value);
            if (!woExists) return (false, "دستور کار انتخاب‌شده معتبر نیست.", null);
        }

        var downtimeEvent = new ProductionDowntimeEvent
        {
            ProductionRunId = productionRunId,
            ProductionWorkCenterId = productionWorkCenterId,
            EquipmentId = equipmentId,
            StartTime = startTime,
            CauseCategory = causeCategory,
            Description = description,
            SourceWorkRequestId = sourceWorkRequestId,
            SourceWorkOrderId = sourceWorkOrderId,
            CreatedDate = DateTime.Now
        };

        await _context.ProductionDowntimeEvents.AddAsync(downtimeEvent);
        await _context.SaveChangesAsync();

        return (true, null, downtimeEvent.ProductionDowntimeEventId);
    }

    public async Task<(bool Success, string? Error)> CloseAsync(int productionDowntimeEventId, DateTime endTime)
    {
        var downtimeEvent = await _context.ProductionDowntimeEvents.FirstOrDefaultAsync(x => x.ProductionDowntimeEventId == productionDowntimeEventId);
        if (downtimeEvent == null) return (false, "رویداد توقف یافت نشد.");
        if (downtimeEvent.EndTime.HasValue) return (false, "این رویداد توقف قبلاً بسته شده است.");
        if (endTime <= downtimeEvent.StartTime) return (false, "زمان پایان باید بعد از زمان شروع باشد.");

        downtimeEvent.EndTime = endTime;
        await _context.SaveChangesAsync();
        return (true, null);
    }
}
