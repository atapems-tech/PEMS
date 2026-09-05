using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard»: قبلاً MaintenanceStrategyController.Index کل
/// جدول (یا زیرمجموعه‌ی فیلترشده با equipmentId) را با Include(Equipment)+ToListAsync (بدون
/// Skip/Take) می‌خواند. اکنون فیلتر equipmentId و صفحه‌بندی همه در SQL انجام می‌شوند.
/// </summary>
public class MaintenanceStrategyService : IMaintenanceStrategyService
{
    private readonly PEMSDbContext _context;

    public MaintenanceStrategyService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<MaintenanceStrategyListDto>> GetListAsync(int page = 1, int pageSize = 50, int? equipmentId = null)
    {
        var query = _context.MaintenanceStrategies.Where(x => x.IsActive).AsQueryable();

        if (equipmentId.HasValue)
            query = query.Where(x => x.EquipmentId == equipmentId.Value);

        var totalCount = await query.CountAsync();

        page = page < 1 ? 1 : page;
        pageSize = DataGridPageSize.Normalize(pageSize);

        var items = await query
            .OrderBy(x => x.Equipment!.TagNumber)
            .ThenBy(x => x.MaintenanceStrategyId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new MaintenanceStrategyListDto
            {
                MaintenanceStrategyId = x.MaintenanceStrategyId,
                EquipmentId = x.EquipmentId,
                EquipmentTagNumber = x.Equipment!.TagNumber,
                StrategyType = x.StrategyType,
                FailureModeCode = x.FailureModeCode,
                ConsequenceCategory = x.ConsequenceCategory,
                Justification = x.Justification
            })
            .ToListAsync();

        return new PagedResult<MaintenanceStrategyListDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }
}
