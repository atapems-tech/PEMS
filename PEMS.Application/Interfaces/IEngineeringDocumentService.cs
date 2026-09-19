using PEMS.Application.DTOs;
using PEMS.Domain.Entities;

namespace PEMS.Application.Interfaces;

public interface IEngineeringDocumentService
{
    Task<List<EngineeringDocumentDto>> GetAllAsync();

    // طبق Phase E-03
    Task<PagedResult<EngineeringDocumentDto>> GetPagedAsync(EngineeringDocumentListFilter filter, bool restrictToDiscipline, int? disciplineId);

    Task<EngineeringDocumentSummaryCountsDto> GetSummaryCountsAsync(bool restrictToDiscipline, int? disciplineId);

    // طبق Phase E-05C
    Task<List<EngineeringRevisionQualityDto>> GetRevisionQualityAsync(bool restrictToDiscipline, int? disciplineId, int? minRevisionCount, int? projectId = null);

    Task<EngineeringDocumentDto?> GetByIdAsync(int id);

    // طبق ممیزی مقیاس‌پذیری (P1 - Document Register Remediation)
    Task<PagedResult<EngineeringDocumentDto>> GetRegisterPagedAsync(
        DocumentRegisterFilter filter, bool restrictToDiscipline, int? disciplineId);

    // Export فهرست Index: همان فیلتر و همان محدودیت دیسیپلین، بدون صفحه‌بندی، با سقف ردیف
    Task<(List<EngineeringDocumentDto> Items, int TotalMatching)> GetAllMatchingAsync(
        EngineeringDocumentListFilter filter, bool restrictToDiscipline, int? disciplineId, int maxRows);

    Task<(List<EngineeringDocumentDto> Items, int TotalMatching)> GetRegisterAllMatchingAsync(
        DocumentRegisterFilter filter, bool restrictToDiscipline, int? disciplineId, int maxRows);

    Task<List<EngineeringDocumentDto>> GetRegisterByIdsAsync(
        List<int> ids, bool restrictToDiscipline, int? disciplineId);

    Task<DocumentRegisterFilterOptionsDto> GetRegisterFilterOptionsAsync(bool restrictToDiscipline, int? disciplineId);

    Task AddAsync(EngineeringDocument document);

    Task<EngineeringDocument?> GetEntityByIdAsync(int id);

    Task UpdateAsync(EngineeringDocument document);

    Task DeleteAsync(EngineeringDocument document);

    Task FreezeAsync(int documentId, int? actorEmployeeId, string reason);

    Task UnfreezeAsync(int documentId, int? actorEmployeeId, string reason);
}