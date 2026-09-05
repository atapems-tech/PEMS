using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard»: قبلاً InstrumentController.Index کل جدول
/// Instruments را با ToListAsync (بدون Skip/Take) می‌خواند و فیلتر Status/شمارنده‌های
/// داشبورد را در حافظه‌ی C# حساب می‌کرد. اکنون فیلتر/صفحه‌بندی/شمارش همه در SQL انجام
/// می‌شوند - رفتار فیلترها و شمارنده‌ها عیناً حفظ شده (شمارنده‌ها روی فیلتر q حساب می‌شوند،
/// نه روی فیلتر Status - همون رفتار قبلی).
/// </summary>
public class InstrumentService : IInstrumentService
{
    private readonly PEMSDbContext _context;

    public InstrumentService(PEMSDbContext context)
    {
        _context = context;
    }

    private static IQueryable<Instrument> ApplySearch(IQueryable<Instrument> query, string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return query;
        var term = q.Trim().ToLower();
        return query.Where(x =>
            (x.LoopNumber != null && x.LoopNumber.ToLower().Contains(term)) ||
            (x.Equipment != null && x.Equipment.TagNumber.ToLower().Contains(term)));
    }

    // طبق قرارداد پروژه (همون InstrumentController.CalibrationStatus قبلی): Overdue اگر
    // سررسید گذشته، DueSoon اگر ظرف ۳۰ روز آینده، وگرنه OK؛ Unknown اگر اصلاً سررسیدی ثبت نشده.
    private static IQueryable<Instrument> ApplyStatus(IQueryable<Instrument> query, string? status, DateTime today, DateTime dueSoonEnd) =>
        status switch
        {
            "Overdue" => query.Where(x => x.NextCalibrationDue != null && x.NextCalibrationDue.Value.Date < today),
            "DueSoon" => query.Where(x => x.NextCalibrationDue != null && x.NextCalibrationDue.Value.Date >= today && x.NextCalibrationDue.Value.Date <= dueSoonEnd),
            "OK" => query.Where(x => x.NextCalibrationDue != null && x.NextCalibrationDue.Value.Date > dueSoonEnd),
            "Unknown" => query.Where(x => x.NextCalibrationDue == null),
            _ => query
        };

    public async Task<PagedResult<InstrumentListDto>> GetListAsync(int page = 1, int pageSize = 50, string? status = null, string? q = null)
    {
        var today = DateTime.Today;
        var dueSoonEnd = today.AddDays(30);

        var query = _context.Instruments.Where(x => x.IsActive).AsQueryable();
        query = ApplySearch(query, q);
        query = ApplyStatus(query, status, today, dueSoonEnd);

        var totalCount = await query.CountAsync();

        page = page < 1 ? 1 : page;
        // Defense-in-Depth طبق سند: Repository/Service هم دوباره فقط ۲۰/۵۰/۱۰۰ را می‌پذیرد،
        // حتی اگر Controller قبلاً Normalize کرده باشد.
        pageSize = DataGridPageSize.Normalize(pageSize);

        var items = await query
            .OrderBy(x => x.Equipment!.TagNumber)
            .ThenBy(x => x.InstrumentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InstrumentListDto
            {
                InstrumentId = x.InstrumentId,
                EquipmentId = x.EquipmentId,
                TagNumber = x.Equipment!.TagNumber,
                Manufacturer = x.Equipment.Manufacturer,
                Model = x.Equipment.Model,
                InstrumentType = x.InstrumentType,
                LoopNumber = x.LoopNumber,
                System = x.System,
                LastCalibrationDate = x.LastCalibrationDate,
                NextCalibrationDue = x.NextCalibrationDue,
                CalibrationStatus =
                    x.NextCalibrationDue == null ? "Unknown" :
                    x.NextCalibrationDue.Value.Date < today ? "Overdue" :
                    x.NextCalibrationDue.Value.Date <= dueSoonEnd ? "DueSoon" : "OK"
            })
            .ToListAsync();

        return new PagedResult<InstrumentListDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<InstrumentDashboardDto> GetDashboardAsync(string? q = null)
    {
        var today = DateTime.Today;
        var dueSoonEnd = today.AddDays(30);

        var query = ApplySearch(_context.Instruments.Where(x => x.IsActive).AsQueryable(), q);

        return new InstrumentDashboardDto
        {
            TotalCount = await query.CountAsync(),
            OverdueCount = await query.CountAsync(x => x.NextCalibrationDue != null && x.NextCalibrationDue.Value.Date < today),
            DueSoonCount = await query.CountAsync(x => x.NextCalibrationDue != null && x.NextCalibrationDue.Value.Date >= today && x.NextCalibrationDue.Value.Date <= dueSoonEnd)
        };
    }
}
