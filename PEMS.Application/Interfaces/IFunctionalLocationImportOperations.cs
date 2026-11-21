namespace PEMS.Application.Interfaces;

public sealed record FunctionalLocationImportBatchRow(int BatchId, string FileName, DateTime ImportDate, string Status, string ImportedBy, int SourceRows, int ExpandedLocations, int NewCount, int ExistingCount, int ConflictCount, int InvalidCount);
public sealed record FunctionalLocationImportRowItem(int RowId, int RowNumber, int SourceRowNumber, string Code, string Name, string ParentArea, string Status, string Action, string ValidationStatus, string ValidationMessage, bool IncludeInCommit);

/// <summary>
/// The functional location import as operations (SCRUM-701): the recent batches, one batch, its rows (a status filter), the upload (staged into a batch), the include toggle of a row, the commit (the highest-risk step: it
/// creates and changes real locations) and the discard. Permissions as the controller checked them: FunctionalLocationImport View, Create, Approve and Delete.
/// </summary>
public interface IFunctionalLocationImportOperations
{
    [PEMS.Application.Common.PemsOperation("FunctionalLocationImport", "Batches", "FunctionalLocationImport.View")]
    Task<List<FunctionalLocationImportBatchRow>> BatchesAsync();

    [PEMS.Application.Common.PemsOperation("FunctionalLocationImport", "Batch", "FunctionalLocationImport.View")]
    Task<List<FunctionalLocationImportBatchRow>> BatchAsync(int batchId);

    [PEMS.Application.Common.PemsOperation("FunctionalLocationImport", "Rows", "FunctionalLocationImport.View")]
    Task<List<FunctionalLocationImportRowItem>> RowsAsync(int batchId, string? status = null);

    [PEMS.Application.Common.PemsOperation("FunctionalLocationImport", "Upload", "FunctionalLocationImport.Create")]
    Task<(bool Success, string? Error)> UploadAsync(int? employeeId, byte[] file, string? fileName = null);

    [PEMS.Application.Common.PemsOperation("FunctionalLocationImport", "ToggleRow", "FunctionalLocationImport.Create")]
    Task<(bool Success, string? Error)> ToggleRowAsync(int rowId, bool include);

    [PEMS.Application.Common.PemsOperation("FunctionalLocationImport", "Commit", "FunctionalLocationImport.Approve")]
    Task<(bool Success, string? Error)> CommitAsync(int batchId);

    [PEMS.Application.Common.PemsOperation("FunctionalLocationImport", "Discard", "FunctionalLocationImport.Delete")]
    Task<(bool Success, string? Error)> DiscardAsync(int batchId);
}
