using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface ILetterService
{
    // طبق «PEMS Correspondence Workspace» بند ۸: پوشه‌ی «Starred» در Inbox سبک Gmail باید یک
    // فیلتر واقعی سمت سرور باشد - نه فقط پرچم نمایشی IsStarred روی نتایج صفحه‌ی جاری
    Task<PagedResult<LetterDto>> SearchAsync(string? letterType, string? letterNumber, string? subject,
        string? sender, string? receiver, int? projectId, int? disciplineId, string? status,
        DateTime? fromDate, DateTime? toDate, int page, int pageSize,
        bool canViewConfidential, int? requestingApplicationUserId, bool starredOnly = false);

    Task<LetterDetailDto?> GetDetailAsync(int letterId);

    Task<int> CreateAsync(string letterType, string letterNumber, DateTime letterDate, string? sender,
        string? receiver, string subject, string? body, string priority, string confidentialityLevel,
        int? projectId, int? disciplineId, int? relatedDocumentId, int? relatedMRId, int? registeredById);

    // برای نمایش دوطرفه: لیست نامه‌های مرتبط با یک مدرک مهندسی/Vendor یا یک MR مشخص
    Task<List<RelatedLetterDto>> GetRelatedToDocumentAsync(int documentId);

    Task<List<RelatedLetterDto>> GetRelatedToMRAsync(int mrId);

    // ==============================================================================
    // طبق «PEMS Letter Details — Controlled Lifecycle»: به‌جای AdvanceStatusAsync قدیمی
    // (وضعیت آزاد/بدون اعتبارسنجی گذار)، هر گذار قانونی یک اکشن صریح و مجزا با اعتبارسنجی
    // سمت‌سرور واقعی است. کاربر هرگز وضعیت دلخواه انتخاب نمی‌کند - فقط یک اقدام کسب‌وکاری.
    //
    //   Draft --(SubmitForReview)--> PendingApproval
    //   PendingApproval --(Approve)--> Approved
    //   PendingApproval --(ReturnForRevision)--> ReturnedForRevision
    //   PendingApproval --(Reject)--> Rejected
    //   ReturnedForRevision --(Resubmit، فقط توسط RegisteredById)--> PendingApproval
    //   Approved --(Issue)--> Issued
    //   Issued --(Close)--> Closed
    // ==============================================================================

    Task<(bool Success, string? Error)> SubmitForReviewAsync(int letterId, int? actorApplicationUserId);

    Task<(bool Success, string? Error)> ApproveAsync(int letterId, int? reviewerApplicationUserId, string? comment);

    Task<(bool Success, string? Error)> ReturnForRevisionAsync(int letterId, int? reviewerApplicationUserId, string reason);

    Task<(bool Success, string? Error)> RejectAsync(int letterId, int? reviewerApplicationUserId, string reason);

    Task<(bool Success, string? Error)> IssueAsync(int letterId, int? actorApplicationUserId);

    Task<(bool Success, string? Error)> CloseAsync(int letterId, int? actorApplicationUserId);

    // طبق بند ۷: فقط نویسنده‌ی اصلی (RegisteredById) و فقط وقتی Status == ReturnedForRevision
    // مجاز به این اقدام است - بعد از موفقیت، نامه بلافاصله دوباره PendingApproval (غیرقابل‌ویرایش)
    // می‌شود. rowVersion برای تشخیص ویرایش هم‌زمان (بند ۲۹) الزامی است.
    Task<(bool Success, string? Error)> ResubmitAsync(int letterId, string subject, string body,
        int currentApplicationUserId, byte[]? rowVersion);

    Task<LetterDashboardDto> GetDashboardAsync(int? currentApplicationUserId = null);

    Task ToggleStarAsync(int letterId, int applicationUserId);

    // طبق درخواست: پشتیبانی از چند گیرنده (نامه/رونوشت) روی یک نامه
    Task AddRecipientAsync(int letterId, string recipientType, int? employeeId, int? departmentId, string? externalName);

    Task MarkRecipientReadAsync(int letterRecipientId, int applicationUserId);

    // تایید الکترونیکی سبک (نه امضای PKI واقعی) - نام تایپ‌شده + مهر زمانی
    Task SignAsync(int letterId, string signedByName);

    // ==============================================================================
    // طبق بند ۴/۷/۱۰: جایگزین UpdateBodyAsync قدیمی (که بدون هیچ بررسی مالکیت/وضعیتی، متن هر
    // نامه‌ای را در هر وضعیتی می‌نوشت). ویرایش محتوای رسمی فقط در دو حالت مجاز است - Draft
    // (پیش‌نویس هنوز ثبت‌نشده) یا ReturnedForRevision (فقط نویسنده‌ی اصلی) - و در هر دو حالت
    // بدون تغییر Status (فقط ذخیره، نه ارسال مجدد؛ برای آن، ResubmitAsync را ببینید).
    // ==============================================================================
    Task<(bool Success, string? Error)> SaveDraftEditAsync(int letterId, string subject, string body,
        int currentApplicationUserId, byte[]? rowVersion);

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
