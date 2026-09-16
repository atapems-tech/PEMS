using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Operations &amp; Production Architecture Spec v1» فاز ۱ - پیاده‌سازی گذارهای
/// ProductionOrder. طبق همان الگوی WorkOrderLifecycleService: دیکشنری گذارهای مجاز صریح،
/// نه یک متد عمومی AdvanceStatus که هر nextStatus ای را بدون چک بپذیرد.
/// </summary>
public class ProductionOrderService : IProductionOrderService
{
    private readonly PEMSDbContext _context;
    private readonly INumberingService _numberingService;

    private static readonly Dictionary<string, string[]> LegalTransitions = new()
    {
        ["Draft"] = new[] { "Planned", "Cancelled" },
        ["Planned"] = new[] { "Released", "Cancelled" },
        ["Released"] = new[] { "InProgress", "OnHold", "Cancelled" },
        ["InProgress"] = new[] { "Completed", "OnHold" },
        ["OnHold"] = new[] { "Released", "InProgress", "Cancelled" },
        ["Completed"] = new[] { "Closed" },
    };

    public ProductionOrderService(PEMSDbContext context, INumberingService numberingService)
    {
        _context = context;
        _numberingService = numberingService;
    }

    public async Task<PagedResult<ProductionOrderListDto>> GetListAsync(int page, int pageSize, string? statusFilter = null, string? search = null)
    {
        var query = _context.ProductionOrders
            .Include(x => x.MaterialItem)
            .Include(x => x.Uom)
            .Include(x => x.ProductionWorkCenter)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(statusFilter))
            query = query.Where(x => x.Status == statusFilter);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.OrderNumber.Contains(term) ||
                (x.MaterialItem != null && (x.MaterialItem.MaterialCode.Contains(term) || x.MaterialItem.Description.Contains(term))));
        }

        var totalCount = await query.CountAsync();
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize <= 0 ? 25 : pageSize;

        var items = await query
            .OrderByDescending(x => x.ProductionOrderId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new ProductionOrderListDto
            {
                ProductionOrderId = x.ProductionOrderId,
                OrderNumber = x.OrderNumber,
                MaterialCode = x.MaterialItem != null ? x.MaterialItem.MaterialCode : "",
                MaterialDescription = x.MaterialItem != null ? x.MaterialItem.Description : "",
                PlannedQuantity = x.PlannedQuantity,
                UomCode = x.Uom != null ? x.Uom.Code : null,
                Status = x.Status,
                WorkCenterCode = x.ProductionWorkCenter != null ? x.ProductionWorkCenter.Code : "",
                PlannedStartDate = x.PlannedStartDate,
                PlannedFinishDate = x.PlannedFinishDate,
                RunCount = x.ProductionRuns.Count
            })
            .ToListAsync();

        return new PagedResult<ProductionOrderListDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ProductionOrderDto?> GetByIdAsync(int productionOrderId)
    {
        var order = await _context.ProductionOrders
            .Include(x => x.MaterialItem)
            .Include(x => x.Uom)
            .Include(x => x.ProductionWorkCenter)
            .Include(x => x.Equipment)
            .Include(x => x.ResponsibleEmployee)
            .Include(x => x.ProductionRuns)
            .ThenInclude(r => r.Outputs)
            .Include(x => x.ProductionRuns)
            .ThenInclude(r => r.Losses)
            .FirstOrDefaultAsync(x => x.ProductionOrderId == productionOrderId);

        if (order == null) return null;

        return new ProductionOrderDto
        {
            ProductionOrderId = order.ProductionOrderId,
            OrderNumber = order.OrderNumber,
            MaterialItemId = order.MaterialItemId,
            MaterialCode = order.MaterialItem?.MaterialCode ?? "",
            MaterialDescription = order.MaterialItem?.Description ?? "",
            PlannedQuantity = order.PlannedQuantity,
            UomId = order.UomId,
            UomCode = order.Uom?.Code,
            PlannedStartDate = order.PlannedStartDate,
            PlannedFinishDate = order.PlannedFinishDate,
            ActualStartDate = order.ActualStartDate,
            ActualFinishDate = order.ActualFinishDate,
            Status = order.Status,
            ProductionWorkCenterId = order.ProductionWorkCenterId,
            WorkCenterCode = order.ProductionWorkCenter?.Code ?? "",
            WorkCenterName = order.ProductionWorkCenter?.Name ?? "",
            EquipmentId = order.EquipmentId,
            EquipmentTagNumber = order.Equipment?.TagNumber,
            ResponsibleEmployeeId = order.ResponsibleEmployeeId,
            ResponsibleEmployeeName = order.ResponsibleEmployee != null ? $"{order.ResponsibleEmployee.FirstName} {order.ResponsibleEmployee.LastName}" : null,
            Notes = order.Notes,
            CancellationReason = order.CancellationReason,
            HoldReason = order.HoldReason,
            CreatedBy = order.CreatedBy,
            CreatedDate = order.CreatedDate,
            Runs = order.ProductionRuns.Select(r => new ProductionRunListDto
            {
                ProductionRunId = r.ProductionRunId,
                ProductionOrderId = r.ProductionOrderId,
                OrderNumber = order.OrderNumber,
                RunNumber = r.RunNumber,
                Status = r.Status,
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                TotalOutputQuantity = r.Outputs.Sum(o => (decimal?)o.Quantity) ?? 0,
                TotalLossQuantity = r.Losses.Sum(l => (decimal?)l.Quantity) ?? 0
            }).OrderByDescending(r => r.ProductionRunId).ToList()
        };
    }

    public async Task<ProductionDashboardDto> GetDashboardAsync()
    {
        var today = DateTime.Today;

        var activeStatuses = new[] { "Planned", "Released", "InProgress", "OnHold" };

        var activeOrders = await _context.ProductionOrders
            .Include(x => x.MaterialItem)
            .Include(x => x.Uom)
            .Include(x => x.ProductionWorkCenter)
            .Where(x => x.IsActive && activeStatuses.Contains(x.Status))
            .OrderByDescending(x => x.ProductionOrderId)
            .Take(20)
            .Select(x => new ProductionOrderListDto
            {
                ProductionOrderId = x.ProductionOrderId,
                OrderNumber = x.OrderNumber,
                MaterialCode = x.MaterialItem != null ? x.MaterialItem.MaterialCode : "",
                MaterialDescription = x.MaterialItem != null ? x.MaterialItem.Description : "",
                PlannedQuantity = x.PlannedQuantity,
                UomCode = x.Uom != null ? x.Uom.Code : null,
                Status = x.Status,
                WorkCenterCode = x.ProductionWorkCenter != null ? x.ProductionWorkCenter.Code : "",
                PlannedStartDate = x.PlannedStartDate,
                PlannedFinishDate = x.PlannedFinishDate,
                RunCount = x.ProductionRuns.Count
            }).ToListAsync();

        var activeRunCount = await _context.ProductionRuns.CountAsync(x => x.Status == "InProgress" || x.Status == "Paused");
        var heldRunCount = await _context.ProductionRuns.CountAsync(x => x.Status == "OnHold");

        var todaysOutputQuantity = await _context.ProductionOutputs
            .Where(x => x.TransactionDate.Date == today)
            .SumAsync(x => (decimal?)x.Quantity) ?? 0;

        var todaysLossQuantity = await _context.ProductionLosses
            .Where(x => x.TransactionDate.Date == today)
            .SumAsync(x => (decimal?)x.Quantity) ?? 0;

        var recentBatches = await _context.ProductionBatches
            .Include(x => x.MaterialItem)
            .Include(x => x.Uom)
            .Include(x => x.ProductionOrder)
            .Include(x => x.ProductionRun)
            .Include(x => x.QualityStatusChangedBy)
            .OrderByDescending(x => x.ProductionBatchId)
            .Take(10)
            .Select(x => new ProductionBatchDto
            {
                ProductionBatchId = x.ProductionBatchId,
                BatchNumber = x.BatchNumber,
                ProductionOrderId = x.ProductionOrderId,
                OrderNumber = x.ProductionOrder != null ? x.ProductionOrder.OrderNumber : "",
                ProductionRunId = x.ProductionRunId,
                RunNumber = x.ProductionRun != null ? x.ProductionRun.RunNumber : "",
                MaterialItemId = x.MaterialItemId,
                MaterialCode = x.MaterialItem != null ? x.MaterialItem.MaterialCode : "",
                MaterialDescription = x.MaterialItem != null ? x.MaterialItem.Description : "",
                Quantity = x.Quantity,
                UomCode = x.Uom != null ? x.Uom.Code : null,
                ProducedDate = x.ProducedDate,
                QualityStatus = x.QualityStatus,
                QualityStatusReason = x.QualityStatusReason,
                QualityStatusChangedDate = x.QualityStatusChangedDate,
                QualityStatusChangedByName = x.QualityStatusChangedBy != null ? $"{x.QualityStatusChangedBy.FirstName} {x.QualityStatusChangedBy.LastName}" : null
            }).ToListAsync();

        var recentLosses = await _context.ProductionLosses
            .Include(x => x.Uom)
            .Include(x => x.Employee)
            .OrderByDescending(x => x.ProductionLossId)
            .Take(10)
            .Select(x => new ProductionLossDto
            {
                ProductionLossId = x.ProductionLossId,
                Quantity = x.Quantity,
                UomCode = x.Uom != null ? x.Uom.Code : null,
                LossCategory = x.LossCategory,
                EmployeeName = x.Employee != null ? $"{x.Employee.FirstName} {x.Employee.LastName}" : null,
                TransactionDate = x.TransactionDate,
                Notes = x.Notes
            }).ToListAsync();

        return new ProductionDashboardDto
        {
            ActiveOrderCount = activeOrders.Count,
            ReleasedOrderCount = activeOrders.Count(x => x.Status == "Released"),
            ActiveRunCount = activeRunCount,
            HeldRunCount = heldRunCount,
            TodaysOutputQuantity = todaysOutputQuantity,
            TodaysLossQuantity = todaysLossQuantity,
            ActiveOrders = activeOrders,
            RecentBatches = recentBatches,
            RecentLosses = recentLosses,
            UnresolvedReconciliationIssueCount = 0
        };
    }

    public async Task<(bool Success, string? Error, int? ProductionOrderId)> CreateAsync(
        int materialItemId, decimal plannedQuantity, int? uomId, int productionWorkCenterId,
        DateTime? plannedStartDate, DateTime? plannedFinishDate, int? equipmentId,
        int? responsibleEmployeeId, string? notes, int? createdByEmployeeId)
    {
        if (plannedQuantity <= 0) return (false, "مقدار برنامه‌ریزی‌شده باید بزرگ‌تر از صفر باشد.", null);

        var materialExists = await _context.MaterialItems.AnyAsync(x => x.MaterialItemId == materialItemId && x.IsActive);
        if (!materialExists) return (false, "متریال/محصول انتخاب‌شده معتبر نیست.", null);

        var workCenterExists = await _context.ProductionWorkCenters.AnyAsync(x => x.ProductionWorkCenterId == productionWorkCenterId && x.IsActive);
        if (!workCenterExists) return (false, "مرکز کاری انتخاب‌شده معتبر نیست.", null);

        if (plannedStartDate.HasValue && plannedFinishDate.HasValue && plannedFinishDate.Value < plannedStartDate.Value)
            return (false, "تاریخ پایان برنامه‌ریزی‌شده نمی‌تواند قبل از تاریخ شروع باشد.", null);

        var orderNumber = await _numberingService.GetNextNumberAsync("ProductionOrder");

        var order = new ProductionOrder
        {
            OrderNumber = orderNumber,
            MaterialItemId = materialItemId,
            PlannedQuantity = plannedQuantity,
            UomId = uomId,
            ProductionWorkCenterId = productionWorkCenterId,
            PlannedStartDate = plannedStartDate,
            PlannedFinishDate = plannedFinishDate,
            EquipmentId = equipmentId,
            ResponsibleEmployeeId = responsibleEmployeeId,
            Notes = notes,
            Status = "Draft",
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.ProductionOrders.AddAsync(order);
        await _context.SaveChangesAsync();

        await LogEventAsync(order.ProductionOrderId, null, "OrderPlanned", createdByEmployeeId, $"سفارش {orderNumber} ایجاد شد.");

        return (true, null, order.ProductionOrderId);
    }

    public Task<(bool Success, string? Error)> PlanAsync(int productionOrderId, int? actorEmployeeId)
        => AdvanceAsync(productionOrderId, "Planned", "OrderPlanned", actorEmployeeId);

    public async Task<(bool Success, string? Error)> ReleaseAsync(int productionOrderId, int? actorEmployeeId)
    {
        var order = await _context.ProductionOrders.FirstOrDefaultAsync(x => x.ProductionOrderId == productionOrderId);
        if (order == null) return (false, "سفارش تولید یافت نشد.");

        if (!LegalTransitions.TryGetValue(order.Status, out var allowed) || !allowed.Contains("Released"))
            return (false, $"گذار از وضعیت «{order.Status}» به «Released» مجاز نیست.");

        order.Status = "Released";
        order.PreHoldStatus = null;
        await _context.SaveChangesAsync();
        await LogEventAsync(productionOrderId, null, "OrderReleased", actorEmployeeId, null);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> HoldAsync(int productionOrderId, string reason, int? actorEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "دلیل تعلیق باید مشخص شود.");

        var order = await _context.ProductionOrders.FirstOrDefaultAsync(x => x.ProductionOrderId == productionOrderId);
        if (order == null) return (false, "سفارش تولید یافت نشد.");

        if (order.Status != "Released" && order.Status != "InProgress")
            return (false, $"فقط سفارش‌های «Released» یا «InProgress» قابل تعلیق‌اند - وضعیت فعلی: «{order.Status}».");

        order.PreHoldStatus = order.Status;
        order.Status = "OnHold";
        order.HoldReason = reason;
        await _context.SaveChangesAsync();
        await LogEventAsync(productionOrderId, null, "OrderHeld", actorEmployeeId, reason);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ResumeAsync(int productionOrderId, int? actorEmployeeId)
    {
        var order = await _context.ProductionOrders.FirstOrDefaultAsync(x => x.ProductionOrderId == productionOrderId);
        if (order == null) return (false, "سفارش تولید یافت نشد.");

        if (order.Status != "OnHold" || string.IsNullOrEmpty(order.PreHoldStatus))
            return (false, "این سفارش در وضعیت تعلیق نیست.");

        order.Status = order.PreHoldStatus;
        order.PreHoldStatus = null;
        order.HoldReason = null;
        await _context.SaveChangesAsync();
        await LogEventAsync(productionOrderId, null, "OrderResumed", actorEmployeeId, null);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CancelAsync(int productionOrderId, string reason, int? actorEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "دلیل لغو باید مشخص شود.");

        var order = await _context.ProductionOrders.FirstOrDefaultAsync(x => x.ProductionOrderId == productionOrderId);
        if (order == null) return (false, "سفارش تولید یافت نشد.");

        if (!LegalTransitions.TryGetValue(order.Status, out var allowed) || !allowed.Contains("Cancelled"))
            return (false, $"سفارش در وضعیت «{order.Status}» قابل لغو نیست.");

        var hasActiveRuns = await _context.ProductionRuns.AnyAsync(x =>
            x.ProductionOrderId == productionOrderId && (x.Status == "InProgress" || x.Status == "Paused"));
        if (hasActiveRuns)
            return (false, "این سفارش دارای اجرای فعال است - ابتدا اجراها را کامل/لغو کنید.");

        order.Status = "Cancelled";
        order.CancellationReason = reason;
        await _context.SaveChangesAsync();
        await LogEventAsync(productionOrderId, null, "OrderCancelled", actorEmployeeId, reason);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CompleteAsync(int productionOrderId, int? actorEmployeeId)
    {
        var order = await _context.ProductionOrders.FirstOrDefaultAsync(x => x.ProductionOrderId == productionOrderId);
        if (order == null) return (false, "سفارش تولید یافت نشد.");

        if (!LegalTransitions.TryGetValue(order.Status, out var allowed) || !allowed.Contains("Completed"))
            return (false, $"گذار از وضعیت «{order.Status}» به «Completed» مجاز نیست.");

        var hasUnfinishedRuns = await _context.ProductionRuns.AnyAsync(x =>
            x.ProductionOrderId == productionOrderId && (x.Status == "Planned" || x.Status == "InProgress" || x.Status == "Paused" || x.Status == "OnHold"));
        if (hasUnfinishedRuns)
            return (false, "این سفارش دارای اجرای ناتمام است - همه‌ی اجراها باید Completed یا Cancelled باشند.");

        order.Status = "Completed";
        order.ActualFinishDate = DateTime.Now;
        await _context.SaveChangesAsync();
        await LogEventAsync(productionOrderId, null, "OrderCompleted", actorEmployeeId, null);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CloseAsync(int productionOrderId, int? actorEmployeeId)
    {
        var order = await _context.ProductionOrders.FirstOrDefaultAsync(x => x.ProductionOrderId == productionOrderId);
        if (order == null) return (false, "سفارش تولید یافت نشد.");

        if (!LegalTransitions.TryGetValue(order.Status, out var allowed) || !allowed.Contains("Closed"))
            return (false, $"گذار از وضعیت «{order.Status}» به «Closed» مجاز نیست.");

        order.Status = "Closed";
        await _context.SaveChangesAsync();
        await LogEventAsync(productionOrderId, null, "OrderClosed", actorEmployeeId, null);
        return (true, null);
    }

    public async Task AdvanceToInProgressIfNeededAsync(int productionOrderId)
    {
        var order = await _context.ProductionOrders.FirstOrDefaultAsync(x => x.ProductionOrderId == productionOrderId);
        if (order == null) return;

        if (order.Status == "Released")
        {
            order.Status = "InProgress";
            order.ActualStartDate ??= DateTime.Now;
            await _context.SaveChangesAsync();
            await LogEventAsync(productionOrderId, null, "OrderInProgress", null, "اولین اجرا شروع شد.");
        }
    }

    private async Task<(bool Success, string? Error)> AdvanceAsync(int productionOrderId, string nextStatus, string eventType, int? actorEmployeeId)
    {
        var order = await _context.ProductionOrders.FirstOrDefaultAsync(x => x.ProductionOrderId == productionOrderId);
        if (order == null) return (false, "سفارش تولید یافت نشد.");

        if (!LegalTransitions.TryGetValue(order.Status, out var allowed) || !allowed.Contains(nextStatus))
            return (false, $"گذار از وضعیت «{order.Status}» به «{nextStatus}» مجاز نیست.");

        order.Status = nextStatus;
        await _context.SaveChangesAsync();
        await LogEventAsync(productionOrderId, null, eventType, actorEmployeeId, null);
        return (true, null);
    }

    private async Task LogEventAsync(int? orderId, int? runId, string eventType, int? employeeId, string? details)
    {
        await _context.ProductionEvents.AddAsync(new ProductionEvent
        {
            ProductionOrderId = orderId,
            ProductionRunId = runId,
            EventType = eventType,
            EventDate = DateTime.Now,
            EmployeeId = employeeId,
            Details = details
        });
        await _context.SaveChangesAsync();
    }
}
