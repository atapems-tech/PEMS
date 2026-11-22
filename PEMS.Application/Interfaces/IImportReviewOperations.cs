using PEMS.Application.Common;

namespace PEMS.Application.Interfaces;

/// <summary>SCRUM-719, 762: the review commands of the MDR and VDR Excel imports (same rules and texts as MDRImportController and VDRImportController). A success may carry the message to show.</summary>
public interface IImportReviewOperations
{
    [PemsOperation("MdrImport", "ToggleRow", "MDRImport.Create")]
    Task<(bool Success, string? Error)> ToggleMdrRowAsync(int rowId, bool include);

    [PemsOperation("MdrImport", "Commit", "MDRImport.Approve")]
    Task<(bool Success, string? Error)> CommitMdrAsync(int batchId);

    [PemsOperation("MdrImport", "Discard", "MDRImport.Delete")]
    Task<(bool Success, string? Error)> DiscardMdrAsync(int batchId);

    [PemsOperation("MdrImport", "RematchDisciplines", "MDRImport.Approve")]
    Task<(bool Success, string? Error)> RematchMdrDisciplinesAsync(int? projectId);

    [PemsOperation("MdrImport", "FixLegacyStatuses", "MDRImport.Approve")]
    Task<(bool Success, string? Error)> FixMdrLegacyStatusesAsync(int? projectId);

    /// <summary>Stages the workbook as a batch to review; answers the id of the new batch.</summary>
    [PemsOperation("MdrImport", "Upload", "MDRImport.Create")]
    Task<int> UploadMdrAsync(OperationFile? file, int projectId, int? employeeId);

    [PemsOperation("VdrImport", "Upload", "VDR.Create")]
    Task<int> UploadVdrAsync(OperationFile? file, int projectId, int? employeeId);

    [PemsOperation("VdrImport", "ToggleRow", "VDR.Create")]
    Task<(bool Success, string? Error)> ToggleVdrRowAsync(int rowId, bool include);

    [PemsOperation("VdrImport", "Commit", "VDR.Approve")]
    Task<(bool Success, string? Error)> CommitVdrAsync(int batchId);

    [PemsOperation("VdrImport", "Discard", "VDR.Delete")]
    Task<(bool Success, string? Error)> DiscardVdrAsync(int batchId);
}
