using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface IInventoryTransactionService
{
    Task<PagedResult<InventoryTransactionRowDto>> GetLedgerAsync(
        int page = 1, int pageSize = 30, int? materialItemId = null, int? warehouseId = null,
        string? transactionType = null, DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>
    /// طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۱۲: مدل Read بچ-آگاه - یک
    /// لجر دوم نیست، همیشه از SUM(InventoryTransaction.Quantity) گروه‌بندی‌شده روی
    /// (MaterialItem, Warehouse, StorageLocation, ProductionBatch) محاسبه می‌شود، دقیقاً با
    /// همون قرارداد علامت/نوع تراکنشی که MaterialStockService از قبل استفاده می‌کند (بدون
    /// قرارداد جدید). فقط ردیف‌هایی که ProductionBatchId واقعاً دارند (غیر Null) را برمی‌گرداند -
    /// تراکنش‌های قدیمی/بدون بچ در این خروجی ظاهر نمی‌شوند.
    /// </summary>
    Task<List<BatchInventoryBalanceRowDto>> GetBatchBalancesAsync(int? materialItemId = null, int? warehouseId = null, int? productionBatchId = null);
}
