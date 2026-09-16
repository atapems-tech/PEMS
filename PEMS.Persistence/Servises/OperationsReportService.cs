using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۲۱-۲۲: پایه‌ی حداقلی
/// گزارش‌گیری Operations - فقط معیارهای قطعی (ProductionOutput/ProductionLoss/ProductionBatch/
/// ProductionTarget موثق). Gross/Net عمداً پیاده نشده (بند ۴ سند فاز - تصمیم کسب‌وکاری
/// حل‌نشده). هیچ‌وقت از یک ذخیره‌ی دوم (مثل یک DailyOperationsSubmission آینده) نمی‌خواند.
/// </summary>
public class OperationsReportService : IOperationsReportService
{
    private readonly PEMSDbContext _context;

    public OperationsReportService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<OperationsReportSummaryDto> GetSummaryAsync(
        PeriodType periodType, DateTime? anchorOrFrom, DateTime? to, DayOfWeek? weekStartDay,
        int? productionWorkCenterId = null, int? materialItemId = null)
    {
        var period = ReportingPeriod.Resolve(periodType, anchorOrFrom, to, weekStartDay);

        var outputQuery = _context.ProductionOutputs
            .Include(x => x.ProductionBatch)
            .Include(x => x.ProductionRun).ThenInclude(r => r!.ProductionOrder)
            .Where(x => x.TransactionDate >= period.Start && x.TransactionDate < period.End);

        if (materialItemId.HasValue)
            outputQuery = outputQuery.Where(x => x.MaterialItemId == materialItemId.Value);

        if (productionWorkCenterId.HasValue)
        {
            // طبق ProductionRun.cs: ProductionWorkCenterId می‌تواند Null باشد (از سفارش به ارث
            // می‌رسد) - هر دو حالت را پوشش می‌دهیم، بدون حدس زدن مقدار جدید.
            outputQuery = outputQuery.Where(x =>
                (x.ProductionRun!.ProductionWorkCenterId ?? x.ProductionRun.ProductionOrder!.ProductionWorkCenterId) == productionWorkCenterId.Value);
        }

        var outputs = await outputQuery
            .Select(x => new { x.Quantity, QualityStatus = x.ProductionBatch != null ? x.ProductionBatch.QualityStatus : "" })
            .ToListAsync();

        var totalOutput = outputs.Sum(x => x.Quantity);
        var accepted = outputs.Where(x => x.QualityStatus == "Released").Sum(x => x.Quantity);
        var rejected = outputs.Where(x => x.QualityStatus == "Rejected").Sum(x => x.Quantity);
        var blocked = outputs.Where(x => x.QualityStatus == "Blocked").Sum(x => x.Quantity);
        var pendingQc = outputs.Where(x => x.QualityStatus is "Produced" or "AwaitingQC").Sum(x => x.Quantity);

        // طبق ممیزی: ProductionLoss در سطح Run است، نه در سطح متریال - فیلتر materialItemId
        // روی این کوئری اعمال نمی‌شود (نه یک محدودسازی حدسی/نادرست).
        var lossQuery = _context.ProductionLosses
            .Include(x => x.ProductionRun).ThenInclude(r => r!.ProductionOrder)
            .Where(x => x.TransactionDate >= period.Start && x.TransactionDate < period.End);

        if (productionWorkCenterId.HasValue)
        {
            lossQuery = lossQuery.Where(x =>
                (x.ProductionRun!.ProductionWorkCenterId ?? x.ProductionRun.ProductionOrder!.ProductionWorkCenterId) == productionWorkCenterId.Value);
        }

        var totalLoss = await lossQuery.SumAsync(x => (decimal?)x.Quantity) ?? 0;

        var targetQuery = _context.ProductionTargets
            .Where(x => x.IsActive && x.TargetDate >= period.Start && x.TargetDate < period.End);

        if (materialItemId.HasValue) targetQuery = targetQuery.Where(x => x.MaterialItemId == materialItemId.Value);
        if (productionWorkCenterId.HasValue) targetQuery = targetQuery.Where(x => x.ProductionWorkCenterId == productionWorkCenterId.Value);

        var totalTarget = await targetQuery.SumAsync(x => (decimal?)x.TargetQuantity) ?? 0;

        return new OperationsReportSummaryDto
        {
            PeriodStart = period.Start,
            PeriodEnd = period.End,
            TotalOutputQuantity = totalOutput,
            AcceptedQuantity = accepted,
            RejectedQuantity = rejected,
            PendingQcQuantity = pendingQc,
            BlockedQuantity = blocked,
            TotalLossQuantity = totalLoss,
            TotalTargetQuantity = totalTarget
        };
    }
}
