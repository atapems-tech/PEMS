using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class MaterialStockService : IMaterialStockService
{
    private readonly PEMSDbContext _context;

    public MaterialStockService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<MaterialStockRowDto>> GetLowStockAsync(int take = 50)
    {
        var stocks = await _context.MaterialStocks
            .Include(x => x.MaterialItem).Include(x => x.Warehouse).Include(x => x.StorageLocation)
            .Where(x => x.IsActive && x.ReorderPoint != null)
            .ToListAsync();

        return stocks.Where(x => x.IsLowStock)
            .OrderBy(x => x.AvailableQuantity)
            .Take(take)
            .Select(x => new MaterialStockRowDto
            {
                MaterialStockId = x.MaterialStockId,
                WarehouseId = x.WarehouseId,
                WarehouseCode = x.Warehouse?.Code ?? "",
                WarehouseName = x.Warehouse?.Name ?? "",
                StorageLocationCode = x.StorageLocation?.Code,
                OnHandQuantity = x.OnHandQuantity,
                ReservedQuantity = x.ReservedQuantity,
                AvailableQuantity = x.AvailableQuantity,
                MinimumQuantity = x.MinimumQuantity,
                MaximumQuantity = x.MaximumQuantity,
                ReorderPoint = x.ReorderPoint,
                IsLowStock = x.IsLowStock,
                IsOutOfStock = x.IsOutOfStock
            }).ToList();
    }

    public async Task<(bool Success, string? Error)> ReceiveAsync(
        int materialItemId, int warehouseId, int? storageLocationId, decimal quantity,
        string? reason, int? employeeId, string? referenceType = null, int? referenceId = null,
        int? productionBatchId = null)
    {
        if (quantity <= 0) return (false, "مقدار دریافتی باید بزرگ‌تر از صفر باشد.");

        var stock = await GetOrCreateStockAsync(materialItemId, warehouseId, storageLocationId);

        stock.OnHandQuantity += quantity;
        stock.LastUpdated = DateTime.Now;

        await _context.InventoryTransactions.AddAsync(new InventoryTransaction
        {
            MaterialItemId = materialItemId,
            WarehouseId = warehouseId,
            StorageLocationId = storageLocationId,
            TransactionType = "Receipt",
            Quantity = quantity,
            ReferenceType = referenceType ?? "Manual",
            ReferenceId = referenceId,
            ProductionBatchId = productionBatchId,
            EmployeeId = employeeId,
            TransactionDate = DateTime.Now,
            Reason = reason
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AdjustAsync(
        int materialItemId, int warehouseId, int? storageLocationId, decimal newOnHandQuantity,
        string? reason, int? employeeId, string? referenceType = null, int? referenceId = null,
        int? productionBatchId = null)
    {
        if (newOnHandQuantity < 0) return (false, "موجودی نمی‌تواند منفی باشد.");

        var stock = await GetOrCreateStockAsync(materialItemId, warehouseId, storageLocationId);
        var delta = newOnHandQuantity - stock.OnHandQuantity;
        if (delta == 0) return (true, null);

        stock.OnHandQuantity = newOnHandQuantity;
        stock.LastUpdated = DateTime.Now;

        await _context.InventoryTransactions.AddAsync(new InventoryTransaction
        {
            MaterialItemId = materialItemId,
            WarehouseId = warehouseId,
            StorageLocationId = storageLocationId,
            TransactionType = "Adjustment",
            Quantity = delta,
            ReferenceType = referenceType ?? "Manual",
            ReferenceId = referenceId,
            ProductionBatchId = productionBatchId,
            EmployeeId = employeeId,
            TransactionDate = DateTime.Now,
            Reason = reason
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<int?> GetLastTransactionIdAsync(string referenceType, int referenceId)
    {
        return await _context.InventoryTransactions
            .Where(x => x.ReferenceType == referenceType && x.ReferenceId == referenceId)
            .OrderByDescending(x => x.InventoryTransactionId)
            .Select(x => (int?)x.InventoryTransactionId)
            .FirstOrDefaultAsync();
    }

    public async Task<(bool Success, string? Error)> ReturnAsync(
        int materialItemId, int warehouseId, int? storageLocationId, decimal quantity,
        string? reason, int? employeeId, int? productionBatchId = null)
    {
        if (quantity <= 0) return (false, "مقدار برگشتی باید بزرگ‌تر از صفر باشد.");

        var stock = await GetOrCreateStockAsync(materialItemId, warehouseId, storageLocationId);

        stock.OnHandQuantity += quantity;
        stock.LastUpdated = DateTime.Now;

        await _context.InventoryTransactions.AddAsync(new InventoryTransaction
        {
            MaterialItemId = materialItemId,
            WarehouseId = warehouseId,
            StorageLocationId = storageLocationId,
            TransactionType = "Return",
            Quantity = quantity,
            ReferenceType = "Manual",
            ProductionBatchId = productionBatchId,
            EmployeeId = employeeId,
            TransactionDate = DateTime.Now,
            Reason = reason
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> TransferAsync(
        int materialItemId, int fromWarehouseId, int? fromStorageLocationId,
        int toWarehouseId, int? toStorageLocationId, decimal quantity, string? reason, int? employeeId,
        int? productionBatchId = null)
    {
        if (quantity <= 0) return (false, "مقدار انتقال باید بزرگ‌تر از صفر باشد.");

        if (fromWarehouseId == toWarehouseId && fromStorageLocationId == toStorageLocationId)
            return (false, "مبدأ و مقصد انتقال نمی‌توانند یکی باشند.");

        var fromStock = await _context.MaterialStocks.FirstOrDefaultAsync(x =>
            x.MaterialItemId == materialItemId && x.WarehouseId == fromWarehouseId
            && x.StorageLocationId == fromStorageLocationId && x.IsActive);

        if (fromStock == null || fromStock.AvailableQuantity < quantity)
        {
            var available = fromStock?.AvailableQuantity ?? 0;
            return (false, $"موجودی قابل‌انتقال کافی نیست - موجودی قابل‌مصرف در انبار مبدأ: {available}.");
        }

        var toStock = await GetOrCreateStockAsync(materialItemId, toWarehouseId, toStorageLocationId);

        fromStock.OnHandQuantity -= quantity;
        fromStock.LastUpdated = DateTime.Now;

        toStock.OnHandQuantity += quantity;
        toStock.LastUpdated = DateTime.Now;

        var fromWarehouseCode = await _context.Warehouses.Where(x => x.WarehouseId == fromWarehouseId).Select(x => x.Code).FirstOrDefaultAsync();
        var toWarehouseCode = await _context.Warehouses.Where(x => x.WarehouseId == toWarehouseId).Select(x => x.Code).FirstOrDefaultAsync();

        await _context.InventoryTransactions.AddAsync(new InventoryTransaction
        {
            MaterialItemId = materialItemId,
            WarehouseId = fromWarehouseId,
            StorageLocationId = fromStorageLocationId,
            TransactionType = "TransferOut",
            Quantity = quantity,
            ReferenceType = "Manual",
            ProductionBatchId = productionBatchId,
            EmployeeId = employeeId,
            TransactionDate = DateTime.Now,
            Reason = reason ?? $"انتقال به {toWarehouseCode}"
        });

        await _context.InventoryTransactions.AddAsync(new InventoryTransaction
        {
            MaterialItemId = materialItemId,
            WarehouseId = toWarehouseId,
            StorageLocationId = toStorageLocationId,
            TransactionType = "TransferIn",
            Quantity = quantity,
            ReferenceType = "Manual",
            ProductionBatchId = productionBatchId,
            EmployeeId = employeeId,
            TransactionDate = DateTime.Now,
            Reason = reason ?? $"انتقال از {fromWarehouseCode}"
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SetThresholdsAsync(
        int materialStockId, decimal? minimumQuantity, decimal? maximumQuantity,
        decimal? reorderPoint, decimal? reorderQuantity)
    {
        var stock = await _context.MaterialStocks.FirstOrDefaultAsync(x => x.MaterialStockId == materialStockId);
        if (stock == null) return (false, "رکورد موجودی پیدا نشد.");

        stock.MinimumQuantity = minimumQuantity;
        stock.MaximumQuantity = maximumQuantity;
        stock.ReorderPoint = reorderPoint;
        stock.ReorderQuantity = reorderQuantity;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    private async Task<MaterialStock> GetOrCreateStockAsync(int materialItemId, int warehouseId, int? storageLocationId)
    {
        var stock = await _context.MaterialStocks.FirstOrDefaultAsync(x =>
            x.MaterialItemId == materialItemId && x.WarehouseId == warehouseId && x.StorageLocationId == storageLocationId);

        if (stock != null) return stock;

        stock = new MaterialStock
        {
            MaterialItemId = materialItemId,
            WarehouseId = warehouseId,
            StorageLocationId = storageLocationId,
            OnHandQuantity = 0,
            ReservedQuantity = 0,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.MaterialStocks.AddAsync(stock);
        await _context.SaveChangesAsync();
        return stock;
    }
}
