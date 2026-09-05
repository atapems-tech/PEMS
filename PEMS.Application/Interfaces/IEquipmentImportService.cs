using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق درخواست: ایمپورت اکسل تجهیزات برای CMMS - همان الگوی MDR/VDR Import (Staging سپس تایید).
/// </summary>
public interface IEquipmentImportService
{
    Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int projectId, int? importedById);

    Task<EquipmentImportBatchDto?> GetBatchAsync(int batchId);

    Task<List<EquipmentImportBatchDto>> GetRecentBatchesAsync();

    Task SetRowIncludeAsync(int rowId, bool include);

    Task<(int created, int updated, int skipped)> CommitBatchAsync(int batchId);

    Task DiscardBatchAsync(int batchId);
}
