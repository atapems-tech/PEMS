using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// Master Instrument List - طبق سند «PEMS — Global Data Grid Standard»: صفحه‌بندی/فیلتر/
/// مرتب‌سازی همه در SQL. Status (Overdue/DueSoon/OK/Unknown) از NextCalibrationDue محاسبه و
/// در همان کوئری فیلتر می‌شود، نه بعد از بارگذاری در حافظه.
/// </summary>
public interface IInstrumentService
{
    Task<PagedResult<InstrumentListDto>> GetListAsync(int page = 1, int pageSize = 50, string? status = null, string? q = null);

    /// <summary>
    /// دقیقاً همون فیلتر جستجوی GetListAsync (q) اعمال می‌شود، اما فیلتر Status نه - چون Status
    /// خودش یکی از خروجی‌های همین شمارنده‌هاست (الگوی قبلی Controller دقیقاً همین بود).
    /// </summary>
    Task<InstrumentDashboardDto> GetDashboardAsync(string? q = null);
}
