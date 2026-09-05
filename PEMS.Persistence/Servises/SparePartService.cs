using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard»: قبلاً SparePartController.Index کل جدول
/// SpareParts را با ToListAsync (بدون Skip/Take) می‌خواند، فیلتر lowStockOnly را در حافظه
/// اعمال می‌کرد، و برای شمارنده‌ی LowStockCount کل جدول را دوباره (یک بار دیگر) بارگذاری
/// می‌کرد. اکنون فیلتر/صفحه‌بندی/شمارش همه در SQL انجام می‌شوند.
/// </summary>
public class SparePartService : ISparePartService
{
    private readonly PEMSDbContext _context;

    public SparePartService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<SparePartListDto>> GetListAsync(int page = 1, int pageSize = 50, string? q = null, bool lowStockOnly = false)
    {
        var query = _context.SpareParts.Where(x => x.IsActive).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(x => x.PartNumber.ToLower().Contains(term) || x.PartName.ToLower().Contains(term));
        }

        if (lowStockOnly)
            query = query.Where(x => x.ReorderPoint != null && x.QuantityOnHand <= x.ReorderPoint.Value);

        var totalCount = await query.CountAsync();

        page = page < 1 ? 1 : page;
        pageSize = DataGridPageSize.Normalize(pageSize);

        var items = await query
            .OrderBy(x => x.PartNumber)
            .ThenBy(x => x.SparePartId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SparePartListDto
            {
                SparePartId = x.SparePartId,
                PartNumber = x.PartNumber,
                PartName = x.PartName,
                Manufacturer = x.Manufacturer,
                Model = x.Model,
                Unit = x.Unit,
                Description = x.Description,
                StorageLocation = x.StorageLocation,
                QuantityOnHand = x.QuantityOnHand,
                MinStockLevel = x.MinStockLevel,
                ReorderPoint = x.ReorderPoint,
                IsLowStock = x.ReorderPoint != null && x.QuantityOnHand <= x.ReorderPoint.Value
            })
            .ToListAsync();

        return new PagedResult<SparePartListDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public Task<int> GetLowStockCountAsync() =>
        _context.SpareParts.CountAsync(x => x.IsActive && x.ReorderPoint != null && x.QuantityOnHand <= x.ReorderPoint.Value);
}
