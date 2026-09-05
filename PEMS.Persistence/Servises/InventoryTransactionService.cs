using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>طبق سند Inventory بخش ۱۰: دفتر کل تراکنش‌ها فقط خواندنی است - نوشتن همیشه از مسیرهای عملیاتی دیگر می‌آید.</summary>
public class InventoryTransactionService : IInventoryTransactionService
{
    private readonly PEMSDbContext _context;

    public InventoryTransactionService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<InventoryTransactionRowDto>> GetLedgerAsync(
        int page = 1, int pageSize = 30, int? materialItemId = null, int? warehouseId = null,
        string? transactionType = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.InventoryTransactions
            .Include(x => x.MaterialItem).Include(x => x.Warehouse).Include(x => x.Employee)
            .AsQueryable();

        if (materialItemId.HasValue) query = query.Where(x => x.MaterialItemId == materialItemId);
        if (warehouseId.HasValue) query = query.Where(x => x.WarehouseId == warehouseId);
        if (!string.IsNullOrWhiteSpace(transactionType)) query = query.Where(x => x.TransactionType == transactionType);
        if (fromDate.HasValue) query = query.Where(x => x.TransactionDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.TransactionDate <= toDate.Value);

        var totalCount = await query.CountAsync();

        page = page < 1 ? 1 : page;
        // Defense-in-depth طبق سند Global Data Grid Standard - حتی اگر Controller قبلاً
        // Normalize کرده باشد، Service هم دوباره فقط ۲۰/۵۰/۱۰۰ را می‌پذیرد.
        pageSize = DataGridPageSize.Normalize(pageSize);

        var items = await query
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.InventoryTransactionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InventoryTransactionRowDto
            {
                InventoryTransactionId = x.InventoryTransactionId,
                MaterialCode = x.MaterialItem != null ? x.MaterialItem.MaterialCode : "",
                MaterialDescription = x.MaterialItem != null ? x.MaterialItem.Description : "",
                WarehouseCode = x.Warehouse != null ? x.Warehouse.Code : "",
                TransactionType = x.TransactionType,
                Quantity = x.Quantity,
                ReferenceType = x.ReferenceType,
                ReferenceId = x.ReferenceId,
                EmployeeName = x.Employee != null ? x.Employee.FirstName + " " + x.Employee.LastName : null,
                TransactionDate = x.TransactionDate,
                Reason = x.Reason
            })
            .ToListAsync();

        return new PagedResult<InventoryTransactionRowDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }
}
