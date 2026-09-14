using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS CMMS — Equipment Functional Location Assignment &amp; Import»: همون الگوی
/// IFunctionalLocationImportService/IEquipmentImportService (Stage → Review/Toggle → Commit/
/// Discard) - با یک تفاوت مهم: این سرویس هرگز Equipment یا FunctionalLocation جدید نمی‌سازد؛
/// فقط رابطه‌ی Equipment.FunctionalLocationId موجود را، بعد از تایید صریح، به‌روز می‌کند.
/// </summary>
public interface IEquipmentFunctionalLocationAssignmentService
{
    Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int? importedById);

    Task<EquipmentFunctionalLocationAssignmentBatchSummaryDto?> GetBatchSummaryAsync(int batchId);

    Task<EquipmentFunctionalLocationAssignmentBatchDto?> GetBatchAsync(int batchId, int page, int pageSize, string? statusFilter);

    Task<List<EquipmentFunctionalLocationAssignmentBatchSummaryDto>> GetRecentBatchesAsync();

    Task SetRowIncludeAsync(int rowId, bool include);

    // طبق بند «Existing Assignment Protection»: تنها راه صریح برای اجازه‌دادن به جایگزینی یک
    // تخصیص موجود و متفاوت - پیش‌فرض false می‌ماند مگر کاربر آگاهانه آن را فعال کند.
    Task SetReplaceExistingAssignmentAsync(int rowId, bool replace);

    Task<EquipmentFunctionalLocationAssignmentCommitResultDto> CommitBatchAsync(int batchId);

    Task DiscardBatchAsync(int batchId);
}
