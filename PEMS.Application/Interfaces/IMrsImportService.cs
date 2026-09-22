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

    // طبق ممیزی «Global 500-Row Batch Limitation Remediation»: صفحه‌بندی واقعی سمت سرور - همون
    // الگوی DataGridPageSize/PagedResult که در بقیه‌ی برنامه (مثلاً MaintenanceHistoryImport) از
    // قبل استاندارد است، نه یک Take() ثابت که ردیف‌های بعد از آن را برای همیشه نامرئی می‌کند.
    Task<MrsImportBatchDetailDto?> GetBatchDetailAsync(int batchId, string? matchStatusFilter = null, int page = 1, int pageSize = 50);

    Task<(bool Success, string? Error, int Committed, int Skipped)> CommitAsync(
        int batchId, List<int> rowIds, int? reviewedById);

    Task<(bool Success, string? Error)> RejectRowsAsync(int batchId, List<int> rowIds, int? reviewedById);

    // طبق همون ممیزی: راه دومی که کل ردیف‌های یک دسته‌ی Match Status را در سراسر Batch (نه فقط
    // صفحه‌ی جاری) با یک اقدام صریح Commit/Reject می‌کند - بدون این‌که کاربر مجبور شود صفحه‌به‌صفحه
    // Checkbox بزند. انتخاب‌های تک‌ردیفی/صفحه‌ای بالا هم دست‌نخورده باقی می‌مانند.
    Task<(bool Success, string? Error, int Committed, int Skipped)> CommitByStatusAsync(
        int batchId, string matchStatusFilter, int? reviewedById);

    Task<(bool Success, string? Error, int Count)> RejectByStatusAsync(
        int batchId, string matchStatusFilter, int? reviewedById);
}
