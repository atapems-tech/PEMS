using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface IVDRImportService
{
    // فایل اکسل را می‌خواند، Validate و Duplicate-Check می‌کند، و در یک Batch جدید Stage می‌کند
    // (هیچ داده‌ای هنوز وارد جداول اصلی VDR نمی‌شود)
    Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int projectId, int? importedById);

    Task<VDRImportBatchDto?> GetBatchAsync(int batchId);

    Task<List<VDRImportBatchDto>> GetRecentBatchesAsync();

    Task SetRowIncludeAsync(int rowId, bool include);

    // ردیف‌های تایید‌شده (IncludeInCommit = true) را به VDRItem/VDRSubmission واقعی تبدیل می‌کند
    Task<(int created, int updated, int skipped)> CommitBatchAsync(int batchId);

    Task DiscardBatchAsync(int batchId);
}
