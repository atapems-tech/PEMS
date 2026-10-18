using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Application.Inventory;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// Phase 2 / P1 - خواندن موجودی قابل‌استفاده از همان منبع حقیقت موجود (MaterialStock = فرافکنی، با Available = OnHand - Reserved که
/// رزرو/انتقال/مصرف هم روی آن تصمیم می‌گیرند). هیچ نوشتنی ندارد و هیچ داده‌ای را اصلاح/Clamp نمی‌کند. فقط ردیف‌های فعال شمرده می‌شوند.
/// سه Query ثابت (متریال‌ها، انبارها، ردیف‌های موجودی) صرف‌نظر از تعداد کلیدها (به‌صورت تکه‌های ۱۰۰۰تایی)، بدون N+1.
/// موارد مبهم: چند ردیف فعال با محل NULL برای یک (متریال، انبار) ⇒ AmbiguousRows (هیچ ردیفی به‌دلخواه انتخاب نمی‌شود)؛ همان وضعیتی که
/// ایندکس یکتای فیلترشده‌ی موجود (فقط StorageLocationId IS NOT NULL) جلویش را نمی‌گیرد. چند ردیف با محل‌های متفاوت ⇒ تجمیع سطح انبار.
/// </summary>
public class UsableStockService : IUsableStockService
{
    private const int ChunkSize = 1000;
    private readonly PEMSDbContext _ctx;

    public UsableStockService(PEMSDbContext ctx) => _ctx = ctx;

    private sealed record MaterialInfo(int Id, bool IsActive, bool IsStockItem, int? UomId);
    private sealed record WarehouseInfo(int Id, bool IsActive);
    private sealed record StockRow(int MaterialItemId, int WarehouseId, int? StorageLocationId, bool IsActive,
        decimal OnHand, decimal Reserved, decimal? Min, decimal? Max, decimal? ReorderPoint, decimal? ReorderQuantity);

    public async Task<IReadOnlyList<UsableStockResult>> GetUsableStockAsync(IReadOnlyCollection<StockKey> keys, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var distinct = keys.Distinct().OrderBy(k => k.MaterialItemId).ThenBy(k => k.WarehouseId).ToList();
        if (distinct.Count == 0) return Array.Empty<UsableStockResult>();

        var itemIds = distinct.Select(k => k.MaterialItemId).Distinct().ToList();
        var warehouseIds = distinct.Select(k => k.WarehouseId).Distinct().ToList();
        var wanted = distinct.ToHashSet();

        var materials = new Dictionary<int, MaterialInfo>();
        var rows = new List<StockRow>();
        foreach (var chunk in itemIds.Chunk(ChunkSize))
        {
            var ids = chunk.ToList();
            foreach (var m in await _ctx.MaterialItems.AsNoTracking().Where(x => ids.Contains(x.MaterialItemId))
                         .Select(x => new MaterialInfo(x.MaterialItemId, x.IsActive, x.IsStockItem, x.UomId)).ToListAsync(ct))
                materials[m.Id] = m;

            rows.AddRange(await _ctx.MaterialStocks.AsNoTracking()
                .Where(x => ids.Contains(x.MaterialItemId) && warehouseIds.Contains(x.WarehouseId))
                .Select(x => new StockRow(x.MaterialItemId, x.WarehouseId, x.StorageLocationId, x.IsActive, x.OnHandQuantity, x.ReservedQuantity,
                    x.MinimumQuantity, x.MaximumQuantity, x.ReorderPoint, x.ReorderQuantity))
                .ToListAsync(ct));
        }

        var warehouses = new Dictionary<int, WarehouseInfo>();
        foreach (var chunk in warehouseIds.Chunk(ChunkSize))
        {
            var ids = chunk.ToList();
            foreach (var w in await _ctx.Warehouses.AsNoTracking().Where(x => ids.Contains(x.WarehouseId))
                         .Select(x => new WarehouseInfo(x.WarehouseId, x.IsActive)).ToListAsync(ct))
                warehouses[w.Id] = w;
        }

        var byKey = rows.Where(r => wanted.Contains(new StockKey(r.MaterialItemId, r.WarehouseId)))
            .GroupBy(r => new StockKey(r.MaterialItemId, r.WarehouseId))
            .ToDictionary(g => g.Key, g => g.ToList());

        var results = new List<UsableStockResult>(distinct.Count);
        foreach (var key in distinct)
        {
            byKey.TryGetValue(key, out var keyRows);
            results.Add(Evaluate(key, materials.GetValueOrDefault(key.MaterialItemId), warehouses.GetValueOrDefault(key.WarehouseId), keyRows ?? new List<StockRow>()));
        }
        return results;
    }

    private static UsableStockResult Evaluate(StockKey key, MaterialInfo? material, WarehouseInfo? warehouse, List<StockRow> rows)
    {
        var issues = new List<NetDemandIssue>();

        if (material == null) issues.Add(new NetDemandIssue(NetDemandIssueCode.MaterialNotFound));
        else
        {
            if (!material.IsActive) issues.Add(new NetDemandIssue(NetDemandIssueCode.InactiveMaterial));
            if (!material.IsStockItem) issues.Add(new NetDemandIssue(NetDemandIssueCode.NotStockItem));
            // MaterialStock واحد ندارد: مقدار موجودی به واحد پایه‌ی متریال است؛ اگر پایه نامعلوم باشد مقایسه‌ی واحد ممکن نیست
            if (material.UomId == null) issues.Add(new NetDemandIssue(NetDemandIssueCode.UnitUnknown, "material has no base unit of measure"));
        }
        if (warehouse == null) issues.Add(new NetDemandIssue(NetDemandIssueCode.WarehouseNotFound));
        else if (!warehouse.IsActive) issues.Add(new NetDemandIssue(NetDemandIssueCode.WarehouseInactive));

        if (issues.Any(i => i.IsBlocking))
            return new UsableStockResult(key.MaterialItemId, key.WarehouseId, StockState.NotCalculable, null, null, null, 0, null, Sorted(issues));

        var active = rows.Where(r => r.IsActive).ToList();
        if (rows.Any(r => !r.IsActive && (r.OnHand != 0 || r.Reserved != 0)))
            issues.Add(new NetDemandIssue(NetDemandIssueCode.StockInactiveRowHasQuantity, "an inactive stock row carries quantity and is excluded"));

        if (active.Count == 0)
        {
            issues.Add(new NetDemandIssue(NetDemandIssueCode.StockNoRecord, "no active stock row: treated as zero stock, as the reservation service does"));
            return new UsableStockResult(key.MaterialItemId, key.WarehouseId, StockState.NoStockRecord, 0m, 0m, 0m, 0, null, Sorted(issues));
        }

        var nullLocationRows = active.Count(r => r.StorageLocationId == null);
        if (nullLocationRows > 1)
        {
            issues.Add(new NetDemandIssue(NetDemandIssueCode.StockAmbiguousRows, $"{nullLocationRows} active stock rows without a storage location"));
            return new UsableStockResult(key.MaterialItemId, key.WarehouseId, StockState.AmbiguousRows, null, null, null, active.Count, null, Sorted(issues));
        }

        var onHand = active.Sum(r => r.OnHand);
        var reserved = active.Sum(r => r.Reserved);
        if (active.Count > 1) issues.Add(new NetDemandIssue(NetDemandIssueCode.StockMultipleLocationRows, $"{active.Count} rows aggregated at warehouse level"));
        if (active.Any(r => r.OnHand < 0)) issues.Add(new NetDemandIssue(NetDemandIssueCode.StockNegativeOnHand, $"on hand {onHand}"));
        if (active.Any(r => r.Reserved < 0)) issues.Add(new NetDemandIssue(NetDemandIssueCode.StockNegativeReserved, $"reserved {reserved}"));
        if (reserved > onHand) issues.Add(new NetDemandIssue(NetDemandIssueCode.StockReservedExceedsOnHand, $"reserved {reserved} > on hand {onHand}"));

        var policy = active.Count == 1
            ? new ReorderPolicy(active[0].Min, active[0].Max, active[0].ReorderPoint, active[0].ReorderQuantity)
            : null;

        return new UsableStockResult(key.MaterialItemId, key.WarehouseId, StockState.Ok, onHand, reserved, onHand - reserved, active.Count, policy, Sorted(issues));
    }

    private static IReadOnlyList<NetDemandIssue> Sorted(List<NetDemandIssue> issues) =>
        issues.DistinctBy(i => (i.Code, i.Detail)).OrderBy(i => i.Code).ThenBy(i => i.Detail, StringComparer.Ordinal).ToList();
}
