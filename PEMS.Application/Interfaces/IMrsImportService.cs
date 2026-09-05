using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق سند Inventory بخش ۲۰-۲۱: خط لوله‌ی Import فایل‌های واقعی MRS - Stage → Review →
/// Commit. هیچ ردیفی مستقیم در MaterialItem/InventoryTransaction نمی‌نشیند مگر بعد از تایید.
/// </summary>
public interface IMrsImportService
{
    Task<(bool Success, string? Error, int? BatchId)> StageFromExcelAsync(
        Stream fileStream, string fileName, string? sheetName, int? importedById, int? sourceAttachmentId = null);

    Task<List<MrsImportBatchSummaryDto>> GetBatchesAsync();

    Task<MrsImportBatchDetailDto?> GetBatchDetailAsync(int batchId, string? matchStatusFilter = null);

    Task<(bool Success, string? Error, int Committed, int Skipped)> CommitAsync(
        int batchId, List<int> rowIds, int? reviewedById);

    Task<(bool Success, string? Error)> RejectRowsAsync(int batchId, List<int> rowIds, int? reviewedById);
}
