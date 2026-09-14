using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS CMMS — Data Migration &amp; Controlled Data Onboarding»: همون الگوی
/// IEquipmentFunctionalLocationAssignmentService (Stage → Review/Toggle → Commit/Discard) -
/// برای MaintenanceHistory (رکورد تاریخچه‌ی نگهداری/تعمیر). هیچ Equipment/WorkOrder جدیدی
/// نمی‌سازد؛ فقط رکورد MaintenanceHistory جدید و Append-only، بعد از تایید صریح، درج می‌کند.
/// </summary>
public interface IMaintenanceHistoryImportService
{
    Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int? importedById);

    Task<MaintenanceHistoryImportBatchSummaryDto?> GetBatchSummaryAsync(int batchId);

    Task<MaintenanceHistoryImportBatchDto?> GetBatchAsync(int batchId, int page, int pageSize, string? statusFilter);

    Task<List<MaintenanceHistoryImportBatchSummaryDto>> GetRecentBatchesAsync();

    Task SetRowIncludeAsync(int rowId, bool include);

    Task<MaintenanceHistoryImportCommitResultDto> CommitBatchAsync(int batchId);

    Task DiscardBatchAsync(int batchId);
}
