using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface IContractService
{
    Task<List<ContractDto>> GetAllAsync();
    Task<ContractDto?> GetByIdAsync(int id);
    Task<int> CreateAsync(string contractNo, int projectId, int vendorId, string? scope,
        decimal? value, DateTime? startDate, DateTime? endDate);
    Task ChangeStatusAsync(int contractId, string status);
}
