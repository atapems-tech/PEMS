using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// پیاده‌سازی Comment Management روی جدول مشترک DocumentComment (طبق Enterprise Document
/// Core). طبق استاندارد واقعی Comment Sheet/Reply Sheet صنعت EPC (Technip/Saipem/Petrofac و
/// مشابه): هر کامنت شماره‌ی یکتای ترتیبی (C-001...) + دیسیپلین/Sheet/Zone/Category/Severity/
/// Required Action/Due Date دارد، و می‌تواند هم خودش هم پاسخش فایل پیوست داشته باشد. طبق
/// درخواست: مدارک مهندسی هم دقیقاً همین چرخه را دارند، نه فقط VDR.
/// </summary>
public class DocumentCommentService : IDocumentCommentService
{
    private readonly PEMSDbContext _context;

    public DocumentCommentService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<List<VDRCommentDto>> GetForDocumentAsync(int documentId)
    {
        var comments = await _context.DocumentComments
            .Include(x => x.VDRSubmission)
            .Include(x => x.Attachment)
            .Include(x => x.ReplyAttachment)
            .Include(x => x.TriggeredRevision)
            .Where(x => x.DocumentId == documentId)
            .OrderByDescending(x => x.RaisedDate)
            .ToListAsync();

        return comments.Select(c => new VDRCommentDto
        {
            VDRCommentId = c.DocumentCommentId,
            VDRSubmissionId = c.VDRSubmissionId ?? 0,
            RevisionNo = c.VDRSubmission?.RevisionNo ?? 0,
            CommentNo = c.CommentNo,
            Sheet = c.Sheet,
            Zone = c.Zone,
            Reference = c.Reference,
            Category = c.Category,
            Severity = c.Severity,
            RequiredAction = c.RequiredAction,
            DueDate = c.DueDate,
            PageNumber = c.PageNumber,
            PinX = c.PinX,
            PinY = c.PinY,
            CommentText = c.CommentText,
            RaisedByName = c.RaisedByName,
            RaisedDate = c.RaisedDate,
            ActionStatus = c.ActionStatus,
            AttachmentId = c.AttachmentId,
            AttachmentFileName = c.Attachment?.FileName,
            AttachmentFilePath = c.Attachment?.FilePath,
            ResponseText = c.ResponseText,
            ResponseDate = c.ResponseDate,
            ReplyAttachmentId = c.ReplyAttachmentId,
            ReplyAttachmentFileName = c.ReplyAttachment?.FileName,
            ReplyAttachmentFilePath = c.ReplyAttachment?.FilePath,
            ClosedByName = c.ClosedByName,
            ClosedDate = c.ClosedDate,
            MarksRevisionRequired = c.MarksRevisionRequired,
            TriggeredRevisionId = c.TriggeredRevisionId,
            TriggeredRevisionNo = c.TriggeredRevision?.RevisionNo
        }).ToList();
    }

    public async Task<int> AddCommentAsync(int documentId, string commentText, string? raisedByName,
        int? vdrSubmissionId = null, string? sheet = null, string? zone = null, string? reference = null,
        string? category = null, string? severity = null, string? requiredAction = null, DateTime? dueDate = null,
        int? attachmentId = null, int? pageNumber = null, double? pinX = null, double? pinY = null,
        bool marksRevisionRequired = false)
    {
        // طبق استاندارد: شماره‌ی یکتای ترتیبی به‌ازای هر مدرک (C-001, C-002, ...) - نه یک
        // شماره‌ی سراسری
        var existingCount = await _context.DocumentComments.CountAsync(x => x.DocumentId == documentId);
        var commentNo = $"C-{(existingCount + 1):000}";

        var comment = new DocumentComment
        {
            DocumentId = documentId,
            VDRSubmissionId = vdrSubmissionId,
            CommentNo = commentNo,
            Sheet = sheet,
            Zone = zone,
            Reference = reference,
            Category = category,
            Severity = severity,
            RequiredAction = requiredAction,
            DueDate = dueDate,
            CommentText = commentText,
            RaisedByName = raisedByName,
            RaisedDate = DateTime.Now,
            ActionStatus = "OpenComment",
            AttachmentId = attachmentId,
            PageNumber = pageNumber,
            PinX = pinX,
            PinY = pinY,
            MarksRevisionRequired = marksRevisionRequired
        };

        await _context.DocumentComments.AddAsync(comment);
        await _context.SaveChangesAsync();

        return comment.DocumentCommentId;
    }

    public async Task SetActionStatusAsync(int commentId, string actionStatus)
    {
        var comment = await _context.DocumentComments.FirstOrDefaultAsync(x => x.DocumentCommentId == commentId);
        if (comment == null) return;

        comment.ActionStatus = actionStatus;

        if (actionStatus == "Closed")
        {
            comment.ClosedDate = DateTime.Now;
        }

        await _context.SaveChangesAsync();
    }

    public async Task RecordResponseAsync(int commentId, string? responseText, int? replyAttachmentId = null)
    {
        var comment = await _context.DocumentComments.FirstOrDefaultAsync(x => x.DocumentCommentId == commentId);
        if (comment == null) return;

        comment.ResponseText = responseText;
        comment.ResponseDate = DateTime.Now;
        comment.ReplyAttachmentId = replyAttachmentId ?? comment.ReplyAttachmentId;

        // ثبت پاسخ Vendor یعنی آماده‌ی بررسی توسط مهندس دیسیپلین است؛ تعیین Closed/Verified
        // نهایی همچنان با اقدام دستی مهندس از طریق SetActionStatusAsync انجام می‌شود
        if (comment.ActionStatus is "OpenComment" or "VendorResponsePending")
            comment.ActionStatus = "Verified";

        await _context.SaveChangesAsync();
    }
}
