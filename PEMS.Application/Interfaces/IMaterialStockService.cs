using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق سند Inventory بخش ۹-۱۰: موجودی هرگز مستقیم تغییر نمی‌کند - هر تغییر یک
/// InventoryTransaction می‌سازد که موجودی از روی آن به‌روزرسانی می‌شود.
/// </summary>
public interface IMaterialStockService
{
    Task<List<MaterialStockRowDto>> GetLowStockAsync(int take = 50);

    Task<(bool Success, string? Error)> ReceiveAsync(
        int materialItemId, int warehouseId, int? storageLocationId, decimal quantity,
        string? reason, int? employeeId);

    Task<(bool Success, string? Error)> AdjustAsync(
        int materialItemId, int warehouseId, int? storageLocationId, decimal newOnHandQuantity,
        string? reason, int? employeeId);

    /// <summary>
    /// طبق CMMS Production Baseline v1 بند ۱۲.۱: متریال فیزیکی به انبار برمی‌گردد - برخلاف
    /// Receive (که برای دریافت نو از تأمین‌کننده است)، این‌جا مصرف قبلی معکوس می‌شود.
    /// </summary>
    Task<(bool Success, string? Error)> ReturnAsync(
        int materialItemId, int warehouseId, int? storageLocationId, decimal quantity,
        string? reason, int? employeeId);

    /// <summary>
    /// طبق CMMS Production Baseline v1 بند ۱۲.۱: انتقال بین دو انبار - دو ردیف InventoryTransaction
    /// جفت (TransferOut/TransferIn) می‌سازد چون هر ردیف فقط یک WarehouseId دارد.
    /// </summary>
    Task<(bool Success, string? Error)> TransferAsync(
        int materialItemId, int fromWarehouseId, int? fromStorageLocationId,
        int toWarehouseId, int? toStorageLocationId, decimal quantity, string? reason, int? employeeId);

    Task<(bool Success, string? Error)> SetThresholdsAsync(
        int materialStockId, decimal? minimumQuantity, decimal? maximumQuantity,
        decimal? reorderPoint, decimal? reorderQuantity);
}
