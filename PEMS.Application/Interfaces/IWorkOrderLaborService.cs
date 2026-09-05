namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق سند معماری Labor Cost - بند ۱۱ تا ۱۴: ثبت واقعی ساعت‌کار، محاسبه‌ی هزینه در سرور
/// (نه از سمت Client)، و Snapshot کردن نرخ در لحظه‌ی ثبت.
/// </summary>
public interface IWorkOrderLaborService
{
    Task<int> AddLaborEntryAsync(
        int workOrderId, int employeeId, int? teamId, DateTime workDate,
        decimal regularHours, decimal overtimeHours, string? notes);

    // نرخ فعلی کارمند (Normal/Overtime) طبق تاریخچه‌ی EmployeeLaborRate در یک تاریخ مشخص
    Task<(decimal RegularRate, decimal OvertimeRate)> GetEffectiveRatesAsync(int employeeId, DateTime asOfDate);

    Task<decimal> GetTotalLaborCostAsync(int workOrderId);
}
