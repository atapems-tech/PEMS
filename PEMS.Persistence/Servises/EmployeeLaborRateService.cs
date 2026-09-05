using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class EmployeeLaborRateService : IEmployeeLaborRateService
{
    private readonly PEMSDbContext _context;

    public EmployeeLaborRateService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task SetRateAsync(int employeeId, string rateType, decimal rate, string currency, DateTime effectiveFrom)
    {
        // Rule 7: نرخ نمی‌تواند منفی باشد
        if (rate < 0)
            throw new Exception("نرخ نمی‌تواند منفی باشد.");

        var employee = await _context.Employees.FirstOrDefaultAsync(x => x.EmployeeId == employeeId);
        if (employee == null) throw new Exception("کارمند یافت نشد.");

        // طبق بند ۱۰: نرخ قبلی از همین نوع (اگر فعال است) بسته می‌شود - نه Overwrite
        var previousActiveRate = await _context.EmployeeLaborRates
            .Where(x => x.EmployeeId == employeeId && x.RateType == rateType && x.IsActive && x.EffectiveTo == null)
            .FirstOrDefaultAsync();

        if (previousActiveRate != null)
            previousActiveRate.EffectiveTo = effectiveFrom.AddDays(-1);

        await _context.EmployeeLaborRates.AddAsync(new EmployeeLaborRate
        {
            EmployeeId = employeeId,
            RateType = rateType,
            Rate = rate,
            Currency = string.IsNullOrWhiteSpace(currency) ? "IRR" : currency,
            EffectiveFrom = effectiveFrom,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
    }

    public async Task<List<EmployeeLaborRate>> GetHistoryAsync(int employeeId)
    {
        return await _context.EmployeeLaborRates
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.EffectiveFrom)
            .ToListAsync();
    }
}
