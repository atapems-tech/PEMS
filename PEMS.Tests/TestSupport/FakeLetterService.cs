using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Tests.TestSupport;

/// <summary>
/// طبق Phase E-02: DocumentWorkflowService به ILetterService وابسته است (نامه‌ی خودکار رد/صدور)،
/// اما این تست‌ها روی گردش‌کار تمرکز دارند، نه ماژول نامه‌نگاری. به‌جای واردکردن LetterService
/// واقعی (که خودش به IRichTextSanitizerService و IKnowledgeBaseService وابسته است)، یک Fake
/// حداقلی فقط برای دو متدی که DocumentWorkflowService واقعاً صدا می‌زند (CreateAsync,
/// AddRecipientAsync) نوشته شده - نه یک معماری Mock جدید، فقط پیاده‌سازی حداقلی همان اینترفیس
/// موجود برای ایزوله‌کردن تست از زیرسیستم نامه‌نگاری.
/// </summary>
public class FakeLetterService : ILetterService
{
    public List<(string LetterType, string LetterNumber, string Subject, int? RelatedDocumentId)> CreatedLetters { get; } = new();

    private int _nextId = 1;

    public Task<int> CreateAsync(string letterType, string letterNumber, DateTime letterDate, string? sender,
        string? receiver, string subject, string? body, string priority, string confidentialityLevel,
        int? projectId, int? disciplineId, int? relatedDocumentId, int? relatedMRId, int? registeredById)
    {
        CreatedLetters.Add((letterType, letterNumber, subject, relatedDocumentId));
        return Task.FromResult(_nextId++);
    }

    public Task AddRecipientAsync(int letterId, string recipientType, int? employeeId, int? departmentId, string? externalName)
        => Task.CompletedTask;

    public Task<List<RelatedLetterDto>> GetRelatedToDocumentAsync(int documentId) => Task.FromResult(new List<RelatedLetterDto>());

    public Task<PagedResult<LetterDto>> SearchAsync(string? letterType, string? letterNumber, string? subject,
        string? sender, string? receiver, int? projectId, int? disciplineId, string? status,
        DateTime? fromDate, DateTime? toDate, int page, int pageSize,
        bool canViewConfidential, int? requestingApplicationUserId, bool starredOnly = false)
        => throw new NotSupportedException("در این تست‌ها استفاده نمی‌شود.");

    public Task<LetterDetailDto?> GetDetailAsync(int letterId) => throw new NotSupportedException();
    public Task<List<RelatedLetterDto>> GetRelatedToMRAsync(int mrId) => throw new NotSupportedException();
    public Task<LetterDashboardDto> GetDashboardAsync(int? currentApplicationUserId = null) => throw new NotSupportedException();
    public Task ToggleStarAsync(int letterId, int applicationUserId) => throw new NotSupportedException();
    public Task MarkRecipientReadAsync(int letterRecipientId, int applicationUserId) => throw new NotSupportedException();
    public Task SignAsync(int letterId, string signedByName) => throw new NotSupportedException();
    public Task<(bool Success, string? Error)> SubmitForReviewAsync(int letterId, int? actorApplicationUserId) => throw new NotSupportedException();
    public Task<(bool Success, string? Error)> ApproveAsync(int letterId, int? reviewerApplicationUserId, string? comment) => throw new NotSupportedException();
    public Task<(bool Success, string? Error)> ReturnForRevisionAsync(int letterId, int? reviewerApplicationUserId, string reason) => throw new NotSupportedException();
    public Task<(bool Success, string? Error)> RejectAsync(int letterId, int? reviewerApplicationUserId, string reason) => throw new NotSupportedException();
    public Task<(bool Success, string? Error)> IssueAsync(int letterId, int? actorApplicationUserId) => throw new NotSupportedException();
    public Task<(bool Success, string? Error)> CloseAsync(int letterId, int? actorApplicationUserId) => throw new NotSupportedException();
    public Task<(bool Success, string? Error)> ResubmitAsync(int letterId, string subject, string body, int currentApplicationUserId, byte[]? rowVersion) => throw new NotSupportedException();
    public Task<(bool Success, string? Error)> SaveDraftEditAsync(int letterId, string subject, string body, int currentApplicationUserId, byte[]? rowVersion) => throw new NotSupportedException();
    public Task<List<LetterTimelineEventDto>> GetTimelineAsync(int letterId) => throw new NotSupportedException();
    public Task<List<ThreadMessageDto>> GetThreadAsync(int letterId) => throw new NotSupportedException();
    public Task<int> ReplyAsync(int parentLetterId, string letterNumber, string subject, string? body, string? receiver,
        int? registeredById) => throw new NotSupportedException();
    public Task AddAttachmentAsync(int letterId, string fileName, string fileExtension, string filePath,
        long fileSize, string? mimeType, int? uploadedById) => throw new NotSupportedException();
}
