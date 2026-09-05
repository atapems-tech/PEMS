using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// موتور Import فایل MDR واقعی: خواندن، Validate، Duplicate-Check، Staging، و Commit نهایی
/// به EngineeringDocuments - دقیقاً همان الگوی IVDRImportService.
/// </summary>
public interface IMDRImportService
{
    Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int projectId, int? importedById);

    Task<MDRImportBatchDto?> GetBatchAsync(int batchId);

    Task<List<MDRImportBatchDto>> GetRecentBatchesAsync();

    Task SetRowIncludeAsync(int rowId, bool include);

    Task<(int created, int updated, int skipped)> CommitBatchAsync(int batchId);

    Task DiscardBatchAsync(int batchId);

    // طبق باگ واقعی گزارش‌شده: مدارکی که قبلاً با تطبیق دقیق (که شکست خورده) ایمپورت شده‌اند و
    // دیسیپلین‌شان خالی مانده، با الگوریتم قوی‌تر جدید دوباره تطبیق داده می‌شوند
    Task<int> RematchDisciplinesAsync(int? projectId = null);

    // طبق باگ واقعی گزارش‌شده: مدارک قبلاً ایمپورت‌شده با رشته‌ی وضعیت اشتباه ("Revise &
    // Resubmit"، "Under Review") را به وضعیت واقعی گردش‌کار برمی‌گرداند
    Task<int> FixLegacyStatusValuesAsync(int? projectId = null);
}
