using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
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

    // طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۱۲: مدل Read بچ-آگاه - جمع
    // روی InventoryTransaction، نه یک جدول موجودی دوم. علامت/نوع تراکنش دقیقاً همون قراردادی
    // است که MaterialStockService از قبل پیاده کرده (Receipt/Return/TransferIn/Adjustment با
    // علامت مثبتِ ذخیره‌شده جمع می‌شوند چون Adjustment از قبل با Delta امضادار ذخیره می‌شود؛
    // فقط TransferOut باید منفی شود - دقیقاً هم‌الگو با MaterialStockService.TransferAsync که
    // fromStock.OnHandQuantity -= quantity می‌کند ولی Quantity مثبت ذخیره می‌کند). هیچ قرارداد
    // علامت جدیدی این‌جا اختراع نشده.
    public async Task<List<BatchInventoryBalanceRowDto>> GetBatchBalancesAsync(int? materialItemId = null, int? warehouseId = null, int? productionBatchId = null)
    {
        var query = _context.InventoryTransactions.Where(x => x.ProductionBatchId != null);

        if (materialItemId.HasValue) query = query.Where(x => x.MaterialItemId == materialItemId);
        if (warehouseId.HasValue) query = query.Where(x => x.WarehouseId == warehouseId);
        if (productionBatchId.HasValue) query = query.Where(x => x.ProductionBatchId == productionBatchId);

        var grouped = await query
            .GroupBy(x => new { x.MaterialItemId, x.WarehouseId, x.StorageLocationId, x.ProductionBatchId })
            .Select(g => new
            {
                g.Key.MaterialItemId,
                g.Key.WarehouseId,
                g.Key.StorageLocationId,
                ProductionBatchId = g.Key.ProductionBatchId!.Value,
                Balance = g.Sum(x => x.TransactionType == "TransferOut" ? -x.Quantity : x.Quantity)
            })
            .ToListAsync();

        if (grouped.Count == 0) return new List<BatchInventoryBalanceRowDto>();

        var materialCodes = await _context.MaterialItems
            .Where(x => grouped.Select(g => g.MaterialItemId).Contains(x.MaterialItemId))
            .ToDictionaryAsync(x => x.MaterialItemId, x => x.MaterialCode);

        var warehouseCodes = await _context.Warehouses
            .Where(x => grouped.Select(g => g.WarehouseId).Contains(x.WarehouseId))
            .ToDictionaryAsync(x => x.WarehouseId, x => x.Code);

        var storageLocationCodes = await _context.StorageLocations
            .Where(x => grouped.Select(g => g.StorageLocationId).Contains(x.StorageLocationId))
            .ToDictionaryAsync(x => x.StorageLocationId, x => x.Code);

        var batches = await _context.ProductionBatches
            .Include(x => x.Uom)
            .Where(x => grouped.Select(g => g.ProductionBatchId).Contains(x.ProductionBatchId))
            .ToDictionaryAsync(x => x.ProductionBatchId, x => x);

        return grouped.Select(g => new BatchInventoryBalanceRowDto
        {
            MaterialItemId = g.MaterialItemId,
            MaterialCode = materialCodes.GetValueOrDefault(g.MaterialItemId, ""),
            WarehouseId = g.WarehouseId,
            WarehouseCode = warehouseCodes.GetValueOrDefault(g.WarehouseId, ""),
            StorageLocationId = g.StorageLocationId,
            StorageLocationCode = g.StorageLocationId.HasValue ? storageLocationCodes.GetValueOrDefault(g.StorageLocationId.Value) : null,
            ProductionBatchId = g.ProductionBatchId,
            BatchNumber = batches.TryGetValue(g.ProductionBatchId, out var batch) ? batch.BatchNumber : "",
            QualityStatus = batch?.QualityStatus ?? "",
            Balance = g.Balance,
            UomCode = batch?.Uom?.Code
        }).ToList();
    }
}
