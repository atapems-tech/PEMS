namespace PEMS.Application.Interfaces;

public sealed record DocImportBatchRow(int Id, string File, string Project, DateTime ImportDate, int Total, int Valid, int Warning, int Error, int Duplicate, string Status);
public sealed record DocImportHeadRow(int Id, string File, string Project, int Total, int Valid, int Warning, int Error, int Duplicate, string Status, bool Staged, string? HeaderWarnings);
public sealed record VdrImportRow(int Id, int RowNumber, bool Include, string DocumentNumber, string? Vendor, string? Type, string? Discipline, int Revisions, string Validation, string? Message, string Duplicate, bool CanToggle, bool CanInclude, bool CanExclude);
public sealed record MdrImportRow(int Id, int RowNumber, bool Include, string? Sheet, string DocumentNumber, string? Title, string? Scope, string? Area, string? Discipline, string? Revision, string? CommentStatus, string? FinalStatus, string? Remark, string Frozen,
    string Validation, string? Message, string Duplicate, bool CanInclude, bool CanExclude);
public sealed record DocImportChoice(string Value, string Label);

/// <summary>
/// SCRUM-830: the read side of VDRImportController and MDRImportController (the recent batches, one batch with its figures and the header warnings of the workbook, its rows, the project choices of the upload form) as operations.
/// VDR.View and MDRImport.View are the rights the controllers checked; the commands stay in IImportReviewOperations.
/// </summary>
public interface IDocImportPagesOperations
{
    [PEMS.Application.Common.PemsOperation("DocImportPage", "VdrBatches", "VDR.View")]
    Task<List<DocImportBatchRow>> VdrBatchesAsync();

    [PEMS.Application.Common.PemsOperation("DocImportPage", "VdrBatch", "VDR.View")]
    Task<List<DocImportHeadRow>> VdrBatchAsync(int batchId);

    [PEMS.Application.Common.PemsOperation("DocImportPage", "VdrRows", "VDR.View")]
    Task<List<VdrImportRow>> VdrRowsAsync(int batchId);

    [PEMS.Application.Common.PemsOperation("DocImportPage", "VdrProjectOptions", "VDR.View")]
    Task<List<DocImportChoice>> VdrProjectOptionsAsync();

    [PEMS.Application.Common.PemsOperation("DocImportPage", "MdrBatches", "MDRImport.View")]
    Task<List<DocImportBatchRow>> MdrBatchesAsync();

    [PEMS.Application.Common.PemsOperation("DocImportPage", "MdrBatch", "MDRImport.View")]
    Task<List<DocImportHeadRow>> MdrBatchAsync(int batchId);

    [PEMS.Application.Common.PemsOperation("DocImportPage", "MdrRows", "MDRImport.View")]
    Task<List<MdrImportRow>> MdrRowsAsync(int batchId);

    [PEMS.Application.Common.PemsOperation("DocImportPage", "MdrProjectOptions", "MDRImport.View")]
    Task<List<DocImportChoice>> MdrProjectOptionsAsync();
}
