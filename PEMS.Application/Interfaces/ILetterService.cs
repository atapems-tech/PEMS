using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface ILetterService
{
    Task<PagedResult<LetterDto>> SearchAsync(string? letterType, string? letterNumber, string? subject,
        string? sender, string? receiver, int? projectId, int? disciplineId, string? status,
        DateTime? fromDate, DateTime? toDate, int page, int pageSize,
        bool canViewConfidential, int? requestingApplicationUserId);

    Task<LetterDetailDto?> GetDetailAsync(int letterId);

    Task<int> CreateAsync(string letterType, string letterNumber, DateTime letterDate, string? sender,
        string? receiver, string subject, string? body, string priority, string confidentialityLevel,
        int? projectId, int? disciplineId, int? relatedDocumentId, int? relatedMRId, int? registeredById);

    // برای نمایش دوطرفه: لیست نامه‌های مرتبط با یک مدرک مهندسی/Vendor یا یک MR مشخص
    Task<List<RelatedLetterDto>> GetRelatedToDocumentAsync(int documentId);

    Task<List<RelatedLetterDto>> GetRelatedToMRAsync(int mrId);

    // گردش‌کار Outgoing: Draft -> PendingApproval -> Approved -> Issued (یا Rejected)
    Task AdvanceStatusAsync(int letterId, string newStatus, int? actorId);

    Task<LetterDashboardDto> GetDashboardAsync(int? currentApplicationUserId = null);

    Task ToggleStarAsync(int letterId, int applicationUserId);

    // طبق درخواست: پشتیبانی از چند گیرنده (نامه/رونوشت) روی یک نامه
    Task AddRecipientAsync(int letterId, string recipientType, int? employeeId, int? departmentId, string? externalName);

    Task MarkRecipientReadAsync(int letterRecipientId, int applicationUserId);

    // تایید الکترونیکی سبک (نه امضای PKI واقعی) - نام تایپ‌شده + مهر زمانی
    Task SignAsync(int letterId, string signedByName);

    // ویرایش متن نامه با ادیتور متن غنی (Rich Text)
    Task UpdateBodyAsync(int letterId, string body);

    // نمایش گردش کامل پیام (ایجاد، ارجاع، خوانده‌شدن، تکمیل، امضا) به‌ترتیب زمانی یکجا
    Task<List<LetterTimelineEventDto>> GetTimelineAsync(int letterId);

    // Thread View: زنجیره‌ی کامل پیام‌های قبلی این نامه (والدها، از قدیم به جدید)
    Task<List<ThreadMessageDto>> GetThreadAsync(int letterId);

    // پاسخ به یک نامه - نامه‌ی جدید با ParentLetterId تنظیم‌شده می‌سازد
    Task<int> ReplyAsync(int parentLetterId, string letterNumber, string subject, string? body, string? receiver,
        int? registeredById);

    // طبق بخش DCC: ثبت پیوست به‌عنوان بخشی از یک عملیات (نه فقط دکمه‌ی جدای بعد از ساخت نامه)
    Task AddAttachmentAsync(int letterId, string fileName, string fileExtension, string filePath,
        long fileSize, string? mimeType, int? uploadedById);
}
