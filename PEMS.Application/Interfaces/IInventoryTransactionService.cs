using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface IInventoryTransactionService
{
    Task<PagedResult<InventoryTransactionRowDto>> GetLedgerAsync(
        int page = 1, int pageSize = 30, int? materialItemId = null, int? warehouseId = null,
        string? transactionType = null, DateTime? fromDate = null, DateTime? toDate = null);
}
