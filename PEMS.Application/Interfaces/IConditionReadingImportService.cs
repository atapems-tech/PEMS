using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۹:
/// دقیقاً همون الگوی IMaintenanceHistoryImportService (Stage → Review/Toggle → Commit/Discard).
/// </summary>
public interface IConditionReadingImportService
{
    Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int? importedById);

    Task<ConditionReadingImportBatchSummaryDto?> GetBatchSummaryAsync(int batchId);

    Task<ConditionReadingImportBatchDto?> GetBatchAsync(int batchId, int page, int pageSize, string? statusFilter);

    Task<List<ConditionReadingImportBatchSummaryDto>> GetRecentBatchesAsync();

    Task SetRowIncludeAsync(int rowId, bool include);

    Task<ConditionReadingImportCommitResultDto> CommitBatchAsync(int batchId);

    Task DiscardBatchAsync(int batchId);
}
