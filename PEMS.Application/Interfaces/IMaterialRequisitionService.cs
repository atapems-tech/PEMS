using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface IMaterialRequisitionService
{
    Task<PagedResult<MRDto>> GetAllAsync(int page = 1, int pageSize = 50);
    Task<MRDto?> GetByIdAsync(int id);

    Task<int> CreateAsync(string mrNumber, int projectId, int disciplineId, string? equipmentTag,
        string description, DateTime? requiredDate, int? responsibleEngineerId, int? relatedDocumentId,
        List<(string desc, string? part, double qty, string? unit)> items);

    Task ChangeStatusAsync(int mrId, string newStatus);

    Task<int> AddQuotationAsync(int mrId, string mrqNumber, string vendorName, DateTime? inquiryDate,
        decimal? amount, string? currency, string? notes);

    Task UpdateQuotationEvaluationAsync(int mrqId, string technical, string commercial, string approval);
}
