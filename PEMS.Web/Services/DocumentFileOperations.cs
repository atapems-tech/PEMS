using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-690, 761, 685: the file-carrying commands of the engineering document and VDR pages (the storage folders and rows are the ones the controllers used).</summary>
public sealed class DocumentFileOperations(PEMSDbContext context, IFileStorageService storage, IDocumentCommentService comments) : IDocumentFileOperations
{
    private async Task<int?> SaveAsync(OperationFile? file, string folder)
    {
        if (file is null || file.Bytes.Length == 0) return null;
        string storageKey;
        await using (var stream = new MemoryStream(file.Bytes)) storageKey = await storage.SaveAsync(folder, file.FileName, stream);
        var attachment = new DocumentAttachment
        {
            FileName = file.FileName,
            FileExtension = Path.GetExtension(file.FileName),
            FilePath = storageKey,
            FileSize = file.Bytes.Length,
            MimeType = file.ContentType,
            UploadDate = DateTime.Now,
            IsActive = true,
            AIStatus = "NotApplicable"
        };
        await context.DocumentAttachments.AddAsync(attachment);
        await context.SaveChangesAsync();
        return attachment.AttachmentId;
    }

    public async Task<(bool Success, string? Error)> AddEngineeringCommentAsync(int documentId, string commentText, string? raisedByName, string? sheet, string? zone, string? reference, string? category, string? severity, string? requiredAction,
        DateTime? dueDate, OperationFile? attachment, int? pageNumber, double? pinX, double? pinY, bool marksRevisionRequired)
    {
        var attachmentId = await SaveAsync(attachment, "engineering-comments/comments");
        await comments.AddCommentAsync(documentId, commentText, raisedByName, null, sheet, zone, reference, category, severity, requiredAction, dueDate, attachmentId, pageNumber, pinX, pinY, marksRevisionRequired);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RecordEngineeringResponseAsync(int commentId, string? responseText, OperationFile? replyAttachment)
    {
        var attachmentId = await SaveAsync(replyAttachment, "engineering-comments/replies");
        await comments.RecordResponseAsync(commentId, responseText, attachmentId);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AddVdrCommentAsync(int vdrItemId, int vdrSubmissionId, string commentText, string? raisedByName, string? sheet, string? zone, string? reference, string? category, string? severity, string? requiredAction,
        DateTime? dueDate, OperationFile? attachment, int? pageNumber, double? pinX, double? pinY)
    {
        var attachmentId = await SaveAsync(attachment, "vdr/comments");
        await comments.AddCommentAsync(vdrItemId, commentText, raisedByName, vdrSubmissionId, sheet, zone, reference, category, severity, requiredAction, dueDate, attachmentId, pageNumber, pinX, pinY);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RecordVdrResponseAsync(int commentId, string? responseText, OperationFile? replyAttachment)
    {
        var attachmentId = await SaveAsync(replyAttachment, "vdr/replies");
        await comments.RecordResponseAsync(commentId, responseText, attachmentId);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UploadVdrCommentSheetAsync(int submissionId, OperationFile file)
    {
        var attachmentId = await SaveAsync(file, "vdr/comment-sheets");
        var submission = await context.VDRSubmissions.FirstOrDefaultAsync(x => x.VDRSubmissionId == submissionId);
        if (submission is null || !attachmentId.HasValue) return (true, null);
        submission.CommentSheetAttachmentId = attachmentId.Value;
        await context.SaveChangesAsync();
        return (true, "Comment sheet attached");
    }

    public async Task<(bool Success, string? Error)> UploadVdrReplySheetAsync(int submissionId, OperationFile file)
    {
        var attachmentId = await SaveAsync(file, "vdr/reply-sheets");
        var submission = await context.VDRSubmissions.FirstOrDefaultAsync(x => x.VDRSubmissionId == submissionId);
        if (submission is null || !attachmentId.HasValue) return (true, null);
        submission.ReplySheetAttachmentId = attachmentId.Value;
        await context.SaveChangesAsync();
        return (true, "Reply sheet attached");
    }
}
