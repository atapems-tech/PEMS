using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «Functional Location Master Data Import»: همان الگوی IEquipmentImportService
/// (Stage → Review/Toggle → Commit/Discard) - Functional Location معادل خودش را دارد،
/// نه یک زیرساخت Import موازی جدید.
/// </summary>
public interface IFunctionalLocationImportService
{
    Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int? importedById);

    Task<FunctionalLocationImportBatchDto?> GetBatchAsync(int batchId);

    Task<List<FunctionalLocationImportBatchDto>> GetRecentBatchesAsync();

    Task SetRowIncludeAsync(int rowId, bool include);

    Task<FunctionalLocationImportCommitResultDto> CommitBatchAsync(int batchId);

    Task DiscardBatchAsync(int batchId);
}
