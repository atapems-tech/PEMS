using PEMS.Application.Common;

namespace PEMS.Application.Interfaces;

/// <summary>SCRUM-753: the review commands of the table extraction pages (same rules and texts as TableExtractionController). A failure of the service is a refusal with its message.</summary>
public interface ITableExtractionOperations
{
    /// <summary>Extracts the table again (without AI) and stages it as a batch to review; answers the id of the new batch.</summary>
    [PemsOperation("TableExtraction", "StageForReview", "TableExtraction.Create")]
    Task<int> StageForReviewAsync(int attachmentId, int? employeeId);

    [PemsOperation("TableExtraction", "ConfirmRow", "TableExtraction.Approve")]
    Task<(bool Success, string? Error)> ConfirmRowAsync(int rowId, int equipmentId, int? employeeId);

    [PemsOperation("TableExtraction", "BulkConfirm", "TableExtraction.Approve")]
    Task<(bool Success, string? Error)> BulkConfirmAsync(List<int> selectedRowIds, int? employeeId);

    [PemsOperation("TableExtraction", "ConfirmRowManual", "TableExtraction.Approve")]
    Task<(bool Success, string? Error)> ConfirmRowManualAsync(int rowId, string equipmentTagNumber, int? employeeId);

    [PemsOperation("TableExtraction", "RejectRow", "TableExtraction.Create")]
    Task<(bool Success, string? Error)> RejectRowAsync(int rowId, string? comment, int? employeeId);
}
