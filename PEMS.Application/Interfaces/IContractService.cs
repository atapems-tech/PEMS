using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface IContractService
{
    Task<List<ContractDto>> GetAllAsync();

    // طبق «PEMS — Global Grid Hardening» P1: GetAllAsync بالا کل جدول Contracts را بدون
    // Skip/Take بارگذاری می‌کند (GetByIdAsync هم از همین متد استفاده می‌کند - دست‌نخورده ماند).
    // این متد جدید و جدا صفحه‌بندی واقعی سمت SQL برای Index دارد.
    Task<PagedResult<ContractDto>> GetPagedAsync(int page, int pageSize);
    Task<ContractDto?> GetByIdAsync(int id);
    Task<int> CreateAsync(string contractNo, int projectId, int vendorId, string? scope,
        decimal? value, DateTime? startDate, DateTime? endDate);
    Task ChangeStatusAsync(int contractId, string status);
}
