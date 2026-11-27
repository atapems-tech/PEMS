namespace PEMS.Application.Interfaces;

public sealed record TableAttachmentRow(int Id, string File, string? Extension, DateTime Uploaded, string? AiStatus, bool CanNoAi);
public sealed record TableAnalysisHead(int Id, string File, bool Success, string? Error, string? Summary, int Rows, string Confidence, string UsedOcr, int Warnings, string? DocumentNo, string? Title, string? Type, string? Discipline, string? Revision, string? Vendor, string? AreaUnit, string? System, bool CanStage);
public sealed record TableAnalysisWarning(int Id, string Warning);
public sealed record TableAnalysisRow(int Id, string Tag, string? Description, string? Signal, string? JunctionBoxTerminal, string? Cable, string? Marshalling, string? IoCard, string? Loop, string? System, string Spare, string? Remarks, string? RawExtra);
public sealed record TableBatchHead(int Id, string File, int BatchNumber, string Method, int Rows, DateTime Created, int Unmatched, int MatchedNoInstrument, int MatchedHasInstrument, int Confirmed, int Rejected);
public sealed record TableBatchRow(int Id, string Tag, string? Description, string? JunctionBox, string? CableNo, string? DcsTag, string Status, string? Equipment, string? Instrument, int? EquipmentId, bool CanConfirm, bool CanLink);

/// <summary>
/// SCRUM-830: the reads of TableExtractionController as operations: the attachment list, the extraction result (with AI or without; kept for ten minutes so the result page's three reads run the extraction once), the review batch with its figures and rows.
/// The commands (stage, confirm, reject, link, bulk confirm) stay in ITableExtractionOperations.
/// </summary>
public interface ITableExtractionPagesOperations
{
    [PEMS.Application.Common.PemsOperation("TablePage", "Attachments", "TableExtraction.View")]
    Task<List<TableAttachmentRow>> AttachmentsAsync();

    [PEMS.Application.Common.PemsOperation("TablePage", "Analysis", "TableExtraction.View")]
    Task<List<TableAnalysisHead>> AnalysisAsync(int attachmentId, string? mode = null, string? hint = null);

    [PEMS.Application.Common.PemsOperation("TablePage", "AnalysisWarnings", "TableExtraction.View")]
    Task<List<TableAnalysisWarning>> AnalysisWarningsAsync(int attachmentId, string? mode = null, string? hint = null);

    [PEMS.Application.Common.PemsOperation("TablePage", "AnalysisRows", "TableExtraction.View")]
    Task<List<TableAnalysisRow>> AnalysisRowsAsync(int attachmentId, string? mode = null, string? hint = null);

    [PEMS.Application.Common.PemsOperation("TablePage", "BatchHead", "TableExtraction.View")]
    Task<List<TableBatchHead>> BatchHeadAsync(int batchId);

    [PEMS.Application.Common.PemsOperation("TablePage", "BatchRows", "TableExtraction.View")]
    Task<List<TableBatchRow>> BatchRowsAsync(int batchId);
}
