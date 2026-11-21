namespace PEMS.Application.Interfaces;

public sealed record DocumentRevisionRow(int RevisionId, int DocumentId, string DocumentNo, string RevisionNo, string PreviousRevisionNo, string Status, string IssuePurpose, DateTime RevisionDate, string PreparedBy, string CheckedBy, string ApprovedBy);

public sealed record PendingDocumentRow(int DocumentId, string DocumentNo, string Title, string Project, string Discipline, string Status, string RevisionNo);

public sealed record WorkflowHistoryRow(int WorkflowId, string DocumentNo, string Action, string FromStatus, string ToStatus, string ActionBy, DateTime ActionDate, string Comment);

public sealed record OcrAttachmentRow(int AttachmentId, string FileName, string DocumentNo, string FileExtension, long FileSize, string AiStatus, string AiSummary, string AiError, DateTime UploadDate);

/// <summary>The counts of the OCR queue by AI status (the whole table, not the filtered grid).</summary>
public sealed record OcrCounts(int Pending, int Processing, int Completed, int Failed);

/// <summary>
/// SCRUM-684, 686, 732: the reads and the retry of three small document screens as operations: the revision history of the documents, the document approval queue with its history (the check, approve and reject
/// commands are the operations of <see cref="IDocumentWorkflowService"/> itself) and the OCR and AI processing queue with its retry. The document details page and the revision creation stay on the documents controller.
/// </summary>
public interface IDocumentFlowOperations
{
    [PEMS.Application.Common.PemsOperation("DocumentRevisionPage", "List")]
    Task<List<DocumentRevisionRow>> RevisionsAsync(int? documentId = null);

    [PEMS.Application.Common.PemsOperation("DocumentRevisionPage", "DocumentChoices")]
    Task<List<ReportChoiceRow>> DocumentChoicesAsync();

    [PEMS.Application.Common.PemsOperation("DocumentWorkflowPage", "Pending")]
    Task<List<PendingDocumentRow>> PendingAsync(string? status = null);

    [PEMS.Application.Common.PemsOperation("DocumentWorkflowPage", "History")]
    Task<List<WorkflowHistoryRow>> HistoryAsync();

    [PEMS.Application.Common.PemsOperation("OcrPage", "Queue", "OCRProcessing.View")]
    Task<List<OcrAttachmentRow>> QueueAsync(string? status = null);

    [PEMS.Application.Common.PemsOperation("OcrPage", "Counts", "OCRProcessing.View")]
    Task<OcrCounts> CountsAsync();

    /// <summary>Queues a background job to analyse the attachment again (it uses the server and the AI model: the create permission is the floor).</summary>
    [PEMS.Application.Common.PemsOperation("OcrPage", "Retry", "OCRProcessing.Create")]
    Task<(bool Success, string? Error)> RetryAsync(int id);
}
