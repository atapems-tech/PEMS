namespace PEMS.Application.Interfaces;

/// <summary>The review-step commands of FunctionalLocationImportController as operations (SCRUM-698): include or exclude a staged row, commit the batch (the highest-risk step, it creates and changes real locations) and discard it. Permissions as the controller checked them (FunctionalLocationImport Create, Approve, Delete).</summary>
public interface IFunctionalLocationImportOperations
{
    [PEMS.Application.Common.PemsOperation("FunctionalLocationImport", "ToggleRow", "FunctionalLocationImport.Create")]
    Task<(bool Success, string? Error)> ToggleRowAsync(int rowId, bool include);

    [PEMS.Application.Common.PemsOperation("FunctionalLocationImport", "Commit", "FunctionalLocationImport.Approve")]
    Task<(bool Success, string? Error)> CommitAsync(int batchId);

    [PEMS.Application.Common.PemsOperation("FunctionalLocationImport", "Discard", "FunctionalLocationImport.Delete")]
    Task<(bool Success, string? Error)> DiscardAsync(int batchId);
}
