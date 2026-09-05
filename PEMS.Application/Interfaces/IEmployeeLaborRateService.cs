namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق سند معماری Labor Cost - بند ۹ و ۱۰: نرخ جدید نرخ قبلی را Overwrite نمی‌کند؛ نرخ
/// قبلی با EffectiveTo بسته می‌شود و رکورد جدید اضافه می‌شود.
/// </summary>
public interface IEmployeeLaborRateService
{
    Task SetRateAsync(int employeeId, string rateType, decimal rate, string currency, DateTime effectiveFrom);

    Task<List<PEMS.Domain.Entities.EmployeeLaborRate>> GetHistoryAsync(int employeeId);
}
