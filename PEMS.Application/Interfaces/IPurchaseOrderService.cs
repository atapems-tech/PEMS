using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface IPurchaseOrderService
{
    Task<List<PurchaseOrderDto>> GetAllAsync();
    Task<PurchaseOrderDto?> GetByIdAsync(int id);
    Task<int> CreateAsync(string poNumber, int projectId, int vendorId, int? contractId,
        string? description, decimal? value);

    // پیدا کردن PO بر اساس شماره یا ساخت خودکار آن - برای Import استفاده می‌شود
    Task<int> FindOrCreateByNumberAsync(string poNumber, int projectId, int vendorId);
}
