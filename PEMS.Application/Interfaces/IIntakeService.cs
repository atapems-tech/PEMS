using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// AI Intake Queue: هسته‌ی جدید معماری AI Engineering Intelligence.
/// Upload دیگر مستقیم EngineeringDocument نمی‌سازد؛ فایل ابتدا این‌جا طبقه‌بندی می‌شود و
/// فقط بعد از عبور از Confidence Gate (خودکار در سطح بالا، یا تایید انسانی در سطح متوسط/پایین)
/// به ماژول درست (MDR/VDR/...) هدایت می‌شود.
/// </summary>
public interface IIntakeService
{
    Task<int> UploadToIntakeAsync(Stream fileStream, string fileName, long fileSize, string? mimeType,
        int projectId, int? uploadedById);

    Task ProcessIntakeAsync(int attachmentId);

    Task<PagedResult<IntakeQueueItemDto>> GetQueueAsync(string? stage = null, int page = 1, int pageSize = 50);

    Task<IntakeDetailDto?> GetDetailAsync(int attachmentId);

    // مسیریابی نهایی (خودکار در Confidence بالا، یا با تایید/اصلاح انسانی در سطح متوسط/پایین)
    Task<(string module, int entityId)> RouteAsync(int attachmentId, string targetModule,
        string? category, string? documentType, string? disciplineCode, int? projectId, string? area,
        string? revisionNo, string? issuePurpose, string? vendorName, string? mrNumber, int? reviewedById);

    Task RejectAsync(int attachmentId, int? reviewedById);
}
