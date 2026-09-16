using PEMS.Application.Common;
using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۲۱-۲۲: پایه‌ی حداقلی گزارش‌گیری
/// Operations - فقط معیارهایی که تعریف‌شان قطعی است (Gross/Net عمداً حذف - بند ۴ سند فاز).
/// همیشه از حقایق موثق (ProductionOutput/ProductionLoss/ProductionBatch/ProductionTarget)
/// می‌خواند - هرگز از یک ذخیره‌ی دوم.
/// </summary>
public interface IOperationsReportService
{
    Task<OperationsReportSummaryDto> GetSummaryAsync(
        PeriodType periodType, DateTime? anchorOrFrom, DateTime? to, DayOfWeek? weekStartDay,
        int? productionWorkCenterId = null, int? materialItemId = null);
}
