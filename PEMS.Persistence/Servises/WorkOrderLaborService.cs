using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class WorkOrderLaborService : IWorkOrderLaborService
{
    private readonly PEMSDbContext _context;

    public WorkOrderLaborService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<int> AddLaborEntryAsync(
        int workOrderId, int employeeId, int? teamId, DateTime workDate,
        decimal regularHours, decimal overtimeHours, string? notes)
    {
        // Rule 6: ساعت کار نمی‌تواند منفی باشد
        if (regularHours < 0 || overtimeHours < 0)
            throw new Exception("ساعت کار نمی‌تواند منفی باشد.");

        if (regularHours == 0 && overtimeHours == 0)
            throw new Exception("حداقل یکی از ساعت عادی یا اضافه‌کاری باید بزرگ‌تر از صفر باشد.");

        var workOrder = await _context.WorkOrders.FirstOrDefaultAsync(x => x.WorkOrderId == workOrderId);
        if (workOrder == null) throw new Exception("WorkOrder یافت نشد.");

        var employee = await _context.Employees.FirstOrDefaultAsync(x => x.EmployeeId == employeeId);
        if (employee == null) throw new Exception("کارمند یافت نشد.");

        // Rule 5: کارمند غیرفعال نباید Labor Entry جدید بگیرد
        if (!employee.IsActive)
            throw new Exception("این کارمند غیرفعال است.");

        var (regularRate, overtimeRate) = await GetEffectiveRatesAsync(employeeId, workDate);

        // Rule 8: LaborCost همیشه در سرور محاسبه می‌شود، هرگز از ورودی Client گرفته نمی‌شود
        var laborCost = (regularHours * regularRate) + (overtimeHours * overtimeRate);

        var entry = new WorkOrderLabor
        {
            WorkOrderId = workOrderId,
            EmployeeId = employeeId,
            TeamId = teamId,
            WorkDate = workDate,
            RegularHours = regularHours,
            OvertimeHours = overtimeHours,
            // طبق بند ۱۴: Snapshot - نرخ همین الان ذخیره می‌شود و دیگر با تغییر نرخ آینده تغییر نمی‌کند
            Rate = regularRate,
            OvertimeRate = overtimeRate,
            LaborCost = laborCost,
            Notes = notes,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.WorkOrderLabors.AddAsync(entry);
        await _context.SaveChangesAsync();

        return entry.WorkOrderLaborId;
    }

    public async Task<(decimal RegularRate, decimal OvertimeRate)> GetEffectiveRatesAsync(int employeeId, DateTime asOfDate)
    {
        var rates = await _context.EmployeeLaborRates
            .Where(x => x.EmployeeId == employeeId && x.IsActive &&
                x.EffectiveFrom <= asOfDate && (x.EffectiveTo == null || x.EffectiveTo >= asOfDate))
            .ToListAsync();

        // Rule 7: نرخ نمی‌تواند منفی باشد - این‌جا فقط از داده‌ی از‌قبل‌معتبر می‌خوانیم،
        // اعتبارسنجی واقعی موقع ثبت نرخ (SetRateAsync در EmployeeController) انجام می‌شود
        var regular = rates.Where(x => x.RateType == "Normal").OrderByDescending(x => x.EffectiveFrom).FirstOrDefault();
        var overtime = rates.Where(x => x.RateType == "Overtime").OrderByDescending(x => x.EffectiveFrom).FirstOrDefault();

        return (regular?.Rate ?? 0, overtime?.Rate ?? 0);
    }

    public async Task<decimal> GetTotalLaborCostAsync(int workOrderId)
    {
        return await _context.WorkOrderLabors
            .Where(x => x.WorkOrderId == workOrderId && x.IsActive)
            .SumAsync(x => x.LaborCost);
    }
}
