using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۶: فقط هدف را نگه می‌دارد.
/// Actual/Variance این‌جا محاسبه نمی‌شود (در OperationsReportService).
/// </summary>
public class ProductionTargetService : IProductionTargetService
{
    private readonly PEMSDbContext _context;

    public ProductionTargetService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductionTargetDto>> GetListAsync(DateTime? fromDate = null, DateTime? toDate = null, int? productionWorkCenterId = null)
    {
        var query = _context.ProductionTargets
            .Include(x => x.ProductionOrder)
            .Include(x => x.MaterialItem)
            .Include(x => x.ProductionWorkCenter)
            .Include(x => x.ShiftDefinition)
            .Include(x => x.Uom)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (fromDate.HasValue) query = query.Where(x => x.TargetDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.TargetDate <= toDate.Value.Date);
        if (productionWorkCenterId.HasValue) query = query.Where(x => x.ProductionWorkCenterId == productionWorkCenterId);

        return await query
            .OrderByDescending(x => x.TargetDate)
            .Select(x => new ProductionTargetDto
            {
                ProductionTargetId = x.ProductionTargetId,
                TargetDate = x.TargetDate,
                ProductionOrderId = x.ProductionOrderId,
                OrderNumber = x.ProductionOrder != null ? x.ProductionOrder.OrderNumber : null,
                MaterialItemId = x.MaterialItemId,
                MaterialCode = x.MaterialItem != null ? x.MaterialItem.MaterialCode : "",
                ProductionWorkCenterId = x.ProductionWorkCenterId,
                WorkCenterCode = x.ProductionWorkCenter != null ? x.ProductionWorkCenter.Code : "",
                ShiftDefinitionId = x.ShiftDefinitionId,
                ShiftCode = x.ShiftDefinition != null ? x.ShiftDefinition.Code : null,
                TargetQuantity = x.TargetQuantity,
                UomCode = x.Uom != null ? x.Uom.Code : null,
                IsActive = x.IsActive
            }).ToListAsync();
    }

    public async Task<(bool Success, string? Error, int? ProductionTargetId)> CreateAsync(
        DateTime targetDate, int? productionOrderId, int materialItemId, int productionWorkCenterId,
        int? shiftDefinitionId, decimal targetQuantity, int? uomId, int? createdByEmployeeId)
    {
        if (targetQuantity <= 0) return (false, "مقدار هدف باید بزرگ‌تر از صفر باشد.", null);

        var materialExists = await _context.MaterialItems.AnyAsync(x => x.MaterialItemId == materialItemId && x.IsActive);
        if (!materialExists) return (false, "متریال انتخاب‌شده معتبر نیست.", null);

        var workCenterExists = await _context.ProductionWorkCenters.AnyAsync(x => x.ProductionWorkCenterId == productionWorkCenterId && x.IsActive);
        if (!workCenterExists) return (false, "مرکز کاری انتخاب‌شده معتبر نیست.", null);

        if (productionOrderId.HasValue)
        {
            var orderExists = await _context.ProductionOrders.AnyAsync(x => x.ProductionOrderId == productionOrderId.Value);
            if (!orderExists) return (false, "سفارش تولید انتخاب‌شده معتبر نیست.", null);
        }

        if (shiftDefinitionId.HasValue)
        {
            var shiftExists = await _context.ShiftDefinitions.AnyAsync(x => x.ShiftDefinitionId == shiftDefinitionId.Value && x.IsActive);
            if (!shiftExists) return (false, "شیفت انتخاب‌شده معتبر نیست.", null);
        }

        var normalizedDate = targetDate.Date;

        // طبق بخش ۶ سند فاز («Required constraints»): محافظت دوگانه - هم این بررسی سطح
        // Application، هم ایندکس یکتای دیتابیس (ProductionTargetConfiguration) - یکی به‌تنهایی
        // کافی نیست (Race Condition واقعی بین دو کاربر هم‌زمان را فقط ایندکس دیتابیس می‌گیرد).
        var duplicateExists = await _context.ProductionTargets.AnyAsync(x =>
            x.IsActive && x.TargetDate == normalizedDate && x.ProductionWorkCenterId == productionWorkCenterId &&
            x.MaterialItemId == materialItemId && x.ShiftDefinitionId == shiftDefinitionId);
        if (duplicateExists)
            return (false, "برای همین تاریخ/مرکز کاری/متریال/شیفت، هدفی از قبل ثبت شده است.", null);

        var target = new ProductionTarget
        {
            TargetDate = normalizedDate,
            ProductionOrderId = productionOrderId,
            MaterialItemId = materialItemId,
            ProductionWorkCenterId = productionWorkCenterId,
            ShiftDefinitionId = shiftDefinitionId,
            TargetQuantity = targetQuantity,
            UomId = uomId,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        try
        {
            await _context.ProductionTargets.AddAsync(target);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // طبق همون الگوی BarcodeService.GenerateAsync: اگر ایندکس یکتای دیتابیس رد شد
            // (Race Condition هم‌زمان)، خطای واضح برگردان - نه یک Exception خام.
            return (false, "برای همین تاریخ/مرکز کاری/متریال/شیفت، هدفی هم‌زمان توسط کاربر دیگری ثبت شد.", null);
        }

        return (true, null, target.ProductionTargetId);
    }

    public async Task<(bool Success, string? Error)> DeactivateAsync(int productionTargetId)
    {
        var target = await _context.ProductionTargets.FirstOrDefaultAsync(x => x.ProductionTargetId == productionTargetId);
        if (target == null) return (false, "هدف تولید یافت نشد.");

        target.IsActive = false;
        await _context.SaveChangesAsync();
        return (true, null);
    }
}
