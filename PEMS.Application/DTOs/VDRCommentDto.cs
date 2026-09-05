namespace PEMS.Application.DTOs;

public class VDRCommentDto
{
    public int VDRCommentId { get; set; }
    public int VDRSubmissionId { get; set; }
    public int RevisionNo { get; set; }
    public string CommentNo { get; set; } = "";
    public string? Sheet { get; set; }
    public string? Zone { get; set; }
    public string? Reference { get; set; }
    public string? Category { get; set; }
    public string? Severity { get; set; }
    public string? RequiredAction { get; set; }
    public DateTime? DueDate { get; set; }
    public int? PageNumber { get; set; }
    public double? PinX { get; set; }
    public double? PinY { get; set; }
    public string CommentText { get; set; } = "";
    public string? RaisedByName { get; set; }
    public DateTime RaisedDate { get; set; }
    public string ActionStatus { get; set; } = "OpenComment";
    public int? AttachmentId { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentFilePath { get; set; }
    public string? ResponseText { get; set; }
    public DateTime? ResponseDate { get; set; }
    public int? ReplyAttachmentId { get; set; }
    public string? ReplyAttachmentFileName { get; set; }
    public string? ReplyAttachmentFilePath { get; set; }
    public string? ClosedByName { get; set; }
    public DateTime? ClosedDate { get; set; }

    // طبق بخش M ممیزی (Clouded/Marked-for-Revision)
    public bool MarksRevisionRequired { get; set; }
    public int? TriggeredRevisionId { get; set; }
    public string? TriggeredRevisionNo { get; set; }
}
