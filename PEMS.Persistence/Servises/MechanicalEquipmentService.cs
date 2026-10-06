using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۳:
/// دقیقاً همون الگوی InstrumentService (PEMS.Persistence/Servises/InstrumentService.cs) - فیلتر/
/// صفحه‌بندی/شمارش همه در SQL. SubType («این MechanicalEquipment زیررده‌ی Rotating دارد یا
/// Static یا هیچ‌کدام») از طریق Any() روی جدول‌های فرزند محاسبه می‌شود - بدون بارگذاری کامل آن‌ها.
/// </summary>
public class MechanicalEquipmentService : IMechanicalEquipmentService
{
    private readonly PEMSDbContext _context;

    public MechanicalEquipmentService(PEMSDbContext context)
    {
        _context = context;
    }

    private static IQueryable<Domain.Entities.MechanicalEquipment> ApplySearch(IQueryable<Domain.Entities.MechanicalEquipment> query, string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return query;
        var term = q.Trim().ToLower();
        return query.Where(x =>
            x.TagNumber.ToLower().Contains(term) ||
            (x.Manufacturer != null && x.Manufacturer.ToLower().Contains(term)));
    }

    public async Task<PagedResult<MechanicalEquipmentListDto>> GetListAsync(int page = 1, int pageSize = 50, string? subType = null, string? q = null)
    {
        var query = _context.MechanicalEquipments.Where(x => x.IsActive).AsQueryable();
        query = ApplySearch(query, q);

        var rotatingIds = _context.RotatingEquipments.Where(x => x.IsActive).Select(x => x.MechanicalEquipmentId);
        var staticIds = _context.StaticEquipments.Where(x => x.IsActive).Select(x => x.MechanicalEquipmentId);

        query = subType switch
        {
            "Rotating" => query.Where(x => rotatingIds.Contains(x.MechanicalEquipmentId)),
            "Static" => query.Where(x => staticIds.Contains(x.MechanicalEquipmentId)),
            "Unclassified" => query.Where(x => !rotatingIds.Contains(x.MechanicalEquipmentId) && !staticIds.Contains(x.MechanicalEquipmentId)),
            _ => query
        };

        var totalCount = await query.CountAsync();

        page = page < 1 ? 1 : page;
        pageSize = DataGridPageSize.Normalize(pageSize);

        var items = await query
            .OrderBy(x => x.TagNumber)
            .ThenBy(x => x.MechanicalEquipmentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new MechanicalEquipmentListDto
            {
                MechanicalEquipmentId = x.MechanicalEquipmentId,
                EquipmentId = x.EquipmentId,
                TagNumber = x.TagNumber,
                EquipmentType = x.EquipmentType,
                Manufacturer = x.Manufacturer,
                Model = x.Model,
                SubType = rotatingIds.Contains(x.MechanicalEquipmentId) ? "Rotating" :
                    staticIds.Contains(x.MechanicalEquipmentId) ? "Static" : "Unclassified"
            })
            .ToListAsync();

        return new PagedResult<MechanicalEquipmentListDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<MechanicalEquipmentDashboardDto> GetDashboardAsync(string? q = null)
    {
        var query = ApplySearch(_context.MechanicalEquipments.Where(x => x.IsActive).AsQueryable(), q);

        var rotatingIds = _context.RotatingEquipments.Where(x => x.IsActive).Select(x => x.MechanicalEquipmentId);
        var staticIds = _context.StaticEquipments.Where(x => x.IsActive).Select(x => x.MechanicalEquipmentId);

        return new MechanicalEquipmentDashboardDto
        {
            TotalCount = await query.CountAsync(),
            RotatingCount = await query.CountAsync(x => rotatingIds.Contains(x.MechanicalEquipmentId)),
            StaticCount = await query.CountAsync(x => staticIds.Contains(x.MechanicalEquipmentId)),
            UnclassifiedCount = await query.CountAsync(x => !rotatingIds.Contains(x.MechanicalEquipmentId) && !staticIds.Contains(x.MechanicalEquipmentId))
        };
    }
}
