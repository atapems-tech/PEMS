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

    private const int MaxPostingAttempts = 3;

    /// <summary>
    /// Phase 2 / P4 - اجرای یک ثبت موجودی با تلاش مجدد «محدود» (حداکثر ۳ بار) فقط روی تعارض هم‌زمانی RowVersion. هر تلاش یا کاملاً
    /// ذخیره می‌شود (یک SaveChanges = یک تراکنش دیتابیس: ردیف موجودی + InventoryTransaction با هم) یا هیچ اثری ندارد؛ پس تلاش مجدد
    /// تراکنش/مقدار تکراری نمی‌سازد: تراکنش‌های Add‌شده‌ی تلاش ناموفق Detach و ردیف‌های موجودی دستکاری‌شده از دیتابیس دوباره خوانده
    /// می‌شوند (مقدار معتبر روز)، سپس عملیات روی همان مقدار تازه اعمال می‌شود. پس از سقف تلاش، شکست صریح (Success=false با پیام قابل‌اقدام) برمی‌گردد؛ هیچ شکستی بلعیده نمی‌شود.
    /// </summary>
    private async Task<(bool Success, string? Error)> PostWithRetryAsync(Func<List<object>, Task<(bool Success, string? Error)>> attempt)
    {
        for (var n = 1; ; n++)
        {
            var touched = new List<object>();
            try
            {
                return await attempt(touched);
            }
            catch (DbUpdateConcurrencyException)
            {
                // تلاش ناموفق هیچ اثری ندارد (SaveChanges اتمیک است): تراکنش‌های Add‌شده Detach و ردیف‌های موجودی از دیتابیس دوباره خوانده می‌شوند
                foreach (var entity in touched)
                {
                    var entry = _context.Entry(entity);
                    if (entity is InventoryTransaction) entry.State = EntityState.Detached;
                    else if (entry.State != EntityState.Detached) await entry.ReloadAsync();
                }

                // پس از سقف تلاش، شکست صریح و قابل‌اقدام برمی‌گردد (نه استثنای خام، نه بلعیدن): هیچ موجودی/تراکنشی ذخیره نشده است
                if (n >= MaxPostingAttempts) return (false, ConcurrencyExhaustedMessage);
            }
        }
    }

    internal const string ConcurrencyExhaustedMessage =
        "موجودی هم‌زمان توسط عملیات دیگری تغییر کرد و پس از ۳ تلاش ثبت نشد؛ هیچ موجودی یا تراکنشی ذخیره نشد. دوباره تلاش کنید. / " +
        "The stock was modified concurrently and the operation could not be saved after 3 attempts; nothing was saved. Please retry.";

    public Task<(bool Success, string? Error)> ReceiveAsync(
        int materialItemId, int warehouseId, int? storageLocationId, decimal quantity,
        string? reason, int? employeeId, string? referenceType = null, int? referenceId = null,
        int? productionBatchId = null)
    {
        if (quantity <= 0) return Task.FromResult((false, (string?)"مقدار دریافتی باید بزرگ‌تر از صفر باشد."));

        return PostWithRetryAsync(async touched =>
        {
            var (stock, error) = await StockRowResolver.GetOrCreateAsync(_context, materialItemId, warehouseId, storageLocationId);
            if (stock == null) return (false, error);
            touched.Add(stock);

            stock.OnHandQuantity += quantity;
            stock.LastUpdated = DateTime.Now;

            var tx = new InventoryTransaction
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
            };
            touched.Add(tx);
            await _context.InventoryTransactions.AddAsync(tx);

            await _context.SaveChangesAsync();
            return (true, null);
        });
    }

    public Task<(bool Success, string? Error)> AdjustAsync(
        int materialItemId, int warehouseId, int? storageLocationId, decimal newOnHandQuantity,
        string? reason, int? employeeId, string? referenceType = null, int? referenceId = null,
        int? productionBatchId = null)
    {
        if (newOnHandQuantity < 0) return Task.FromResult((false, (string?)"موجودی نمی‌تواند منفی باشد."));

        return PostWithRetryAsync(async touched =>
        {
            var (stock, error) = await StockRowResolver.GetOrCreateAsync(_context, materialItemId, warehouseId, storageLocationId);
            if (stock == null) return (false, error);
            touched.Add(stock);

            var delta = newOnHandQuantity - stock.OnHandQuantity;
            if (delta == 0) return (true, null);
            if (newOnHandQuantity < stock.ReservedQuantity)
                return (false, "موجودی نمی‌تواند از مقدار رزروشده (" + stock.ReservedQuantity + ") کمتر شود؛ ابتدا رزروها را آزاد کنید.");

            stock.OnHandQuantity = newOnHandQuantity;
            stock.LastUpdated = DateTime.Now;

            var tx = new InventoryTransaction
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
            };
            touched.Add(tx);
            await _context.InventoryTransactions.AddAsync(tx);

            await _context.SaveChangesAsync();
            return (true, null);
        });
    }

    /// <summary>
    /// Phase 2 / P4 closure (Workstream D): تغییر «نسبی» موجودی (Delta علامت‌دار). برخلاف AdjustAsync (مقدار مطلق که صدازننده از خواندن قبلی
    /// محاسبه کرده و می‌تواند با عملیات هم‌زمان دیگر روی همان ردیف Race داشته باشد)، مقدار جدید این‌جا داخل هر تلاش و روی مقدار تازه‌ی ردیف
    /// حساب می‌شود؛ پس تلاش مجدد بعد از تعارض RowVersion مقدار روز را می‌بیند و هیچ تغییر هم‌زمانی گم نمی‌شود. شکل دفتر کل همان Adjustment
    /// (Quantity = Delta علامت‌دار) است؛ ردیف موجودی کافی نباشد یا کمتر از رزرو شود ⇒ شکست صریح بدون هیچ تغییر.
    /// </summary>
    public Task<(bool Success, string? Error)> AdjustByAsync(
        int materialItemId, int warehouseId, int? storageLocationId, decimal delta,
        string? reason, int? employeeId, string? referenceType = null, int? referenceId = null,
        int? productionBatchId = null)
    {
        if (delta == 0) return Task.FromResult((true, (string?)null));

        return PostWithRetryAsync(async touched =>
        {
            MaterialStock? stock;
            if (delta < 0)
            {
                // کاهش هرگز ردیف خالی نمی‌سازد: ردیف نبودن یعنی موجودی صفر
                var lookup = await StockRowResolver.FindExactAsync(_context, materialItemId, warehouseId, storageLocationId, activeOnly: false);
                if (lookup.IsAmbiguous)
                    return (false, StockRowResolver.AmbiguousMessage(materialItemId, warehouseId, storageLocationId, lookup.CandidateIds));
                stock = lookup.Row;
                var current = stock?.OnHandQuantity ?? 0;
                if (stock == null || current + delta < 0)
                    return (false, $"موجودی کافی برای ارسال این مصرف به انبار وجود ندارد - موجودی فعلی: {current}.");
            }
            else
            {
                var (created, error) = await StockRowResolver.GetOrCreateAsync(_context, materialItemId, warehouseId, storageLocationId);
                if (created == null) return (false, error);
                stock = created;
            }
            touched.Add(stock);

            var newOnHand = stock.OnHandQuantity + delta;
            if (newOnHand < stock.ReservedQuantity)
                return (false, "موجودی نمی‌تواند از مقدار رزروشده (" + stock.ReservedQuantity + ") کمتر شود؛ ابتدا رزروها را آزاد کنید.");

            stock.OnHandQuantity = newOnHand;
            stock.LastUpdated = DateTime.Now;

            var tx = new InventoryTransaction
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
            };
            touched.Add(tx);
            await _context.InventoryTransactions.AddAsync(tx);

            await _context.SaveChangesAsync();
            return (true, null);
        });
    }

    public async Task<int?> GetLastTransactionIdAsync(string referenceType, int referenceId)
    {
        return await _context.InventoryTransactions
            .Where(x => x.ReferenceType == referenceType && x.ReferenceId == referenceId)
            .OrderByDescending(x => x.InventoryTransactionId)
            .Select(x => (int?)x.InventoryTransactionId)
            .FirstOrDefaultAsync();
    }

    public Task<(bool Success, string? Error)> ReturnAsync(
        int materialItemId, int warehouseId, int? storageLocationId, decimal quantity,
        string? reason, int? employeeId, int? productionBatchId = null)
    {
        if (quantity <= 0) return Task.FromResult((false, (string?)"مقدار برگشتی باید بزرگ‌تر از صفر باشد."));

        return PostWithRetryAsync(async touched =>
        {
            var (stock, error) = await StockRowResolver.GetOrCreateAsync(_context, materialItemId, warehouseId, storageLocationId);
            if (stock == null) return (false, error);
            touched.Add(stock);

            stock.OnHandQuantity += quantity;
            stock.LastUpdated = DateTime.Now;

            var tx = new InventoryTransaction
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
            };
            touched.Add(tx);
            await _context.InventoryTransactions.AddAsync(tx);

            await _context.SaveChangesAsync();
            return (true, null);
        });
    }

    public Task<(bool Success, string? Error)> TransferAsync(
        int materialItemId, int fromWarehouseId, int? fromStorageLocationId,
        int toWarehouseId, int? toStorageLocationId, decimal quantity, string? reason, int? employeeId,
        int? productionBatchId = null)
    {
        if (quantity <= 0) return Task.FromResult((false, (string?)"مقدار انتقال باید بزرگ‌تر از صفر باشد."));

        if (fromWarehouseId == toWarehouseId && fromStorageLocationId == toStorageLocationId)
            return Task.FromResult((false, (string?)"مبدأ و مقصد انتقال نمی‌توانند یکی باشند."));

        return PostWithRetryAsync(async touched =>
        {
            var from = await StockRowResolver.FindExactAsync(_context, materialItemId, fromWarehouseId, fromStorageLocationId, activeOnly: true);
            if (from.IsAmbiguous)
                return (false, StockRowResolver.AmbiguousMessage(materialItemId, fromWarehouseId, fromStorageLocationId, from.CandidateIds));

            var fromStock = from.Row;
            if (fromStock == null || fromStock.AvailableQuantity < quantity)
            {
                var available = fromStock?.AvailableQuantity ?? 0;
                return (false, $"موجودی قابل‌انتقال کافی نیست - موجودی قابل‌مصرف در انبار مبدأ: {available}.");
            }
            touched.Add(fromStock);

            var (toStock, toError) = await StockRowResolver.GetOrCreateAsync(_context, materialItemId, toWarehouseId, toStorageLocationId);
            if (toStock == null) return (false, toError);
            touched.Add(toStock);

            fromStock.OnHandQuantity -= quantity;
            fromStock.LastUpdated = DateTime.Now;

            toStock.OnHandQuantity += quantity;
            toStock.LastUpdated = DateTime.Now;

            var fromWarehouseCode = await _context.Warehouses.Where(x => x.WarehouseId == fromWarehouseId).Select(x => x.Code).FirstOrDefaultAsync();
            var toWarehouseCode = await _context.Warehouses.Where(x => x.WarehouseId == toWarehouseId).Select(x => x.Code).FirstOrDefaultAsync();

            var txOut = new InventoryTransaction
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
            };
            var txIn = new InventoryTransaction
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
            };
            touched.Add(txOut);
            touched.Add(txIn);
            await _context.InventoryTransactions.AddAsync(txOut);
            await _context.InventoryTransactions.AddAsync(txIn);

            await _context.SaveChangesAsync();
            return (true, null);
        });
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
}
