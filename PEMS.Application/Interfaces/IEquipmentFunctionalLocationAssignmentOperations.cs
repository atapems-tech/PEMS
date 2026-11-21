namespace PEMS.Application.Interfaces;

public sealed record EquipmentFunctionalLocationAssignmentBatchRow(int BatchId, string FileName, DateTime ImportDate, string Status, string ImportedBy, int TotalRows, int ExactCount, int AlreadyAssignedCount, int NoChangeCount, int UnresolvedCount, int ConflictCount, int ValidationErrorCount);
public sealed record EquipmentFunctionalLocationAssignmentRowItem(int RowId, int RowNumber, int SourceRowNumber, string EquipmentTag, string EquipmentDescription, string CurrentLocation, string ProposedLocation, string Status, string ValidationStatus, string ValidationMessage, bool IncludeInCommit, bool ReplaceExisting);

/// <summary>
/// The equipment to functional location assignment import as operations (SCRUM-694): the recent batches, one batch, its rows (a status filter), the upload (staged into a batch), the include toggle of a row (Create), the permission to replace an
/// existing assignment (Approve: kept apart from the plain toggle so it is never switched on by accident), the commit (Approve; it changes the real Equipment.FunctionalLocationId) and the discard (Delete). Permissions as the controller checked them.
/// </summary>
public interface IEquipmentFunctionalLocationAssignmentOperations
{
    [PEMS.Application.Common.PemsOperation("EquipmentFunctionalLocationAssignment", "Batches", "EquipmentFunctionalLocationAssignment.View")]
    Task<List<EquipmentFunctionalLocationAssignmentBatchRow>> BatchesAsync();

    [PEMS.Application.Common.PemsOperation("EquipmentFunctionalLocationAssignment", "Batch", "EquipmentFunctionalLocationAssignment.View")]
    Task<List<EquipmentFunctionalLocationAssignmentBatchRow>> BatchAsync(int batchId);

    [PEMS.Application.Common.PemsOperation("EquipmentFunctionalLocationAssignment", "Rows", "EquipmentFunctionalLocationAssignment.View")]
    Task<List<EquipmentFunctionalLocationAssignmentRowItem>> RowsAsync(int batchId, string? status = null);

    [PEMS.Application.Common.PemsOperation("EquipmentFunctionalLocationAssignment", "Upload", "EquipmentFunctionalLocationAssignment.Create")]
    Task<(bool Success, string? Error)> UploadAsync(int? employeeId, byte[] file, string? fileName = null);

    [PEMS.Application.Common.PemsOperation("EquipmentFunctionalLocationAssignment", "ToggleRow", "EquipmentFunctionalLocationAssignment.Create")]
    Task<(bool Success, string? Error)> ToggleRowAsync(int rowId, bool include);

    [PEMS.Application.Common.PemsOperation("EquipmentFunctionalLocationAssignment", "ReplaceExisting", "EquipmentFunctionalLocationAssignment.Approve")]
    Task<(bool Success, string? Error)> ReplaceExistingAsync(int rowId, bool replace);

    [PEMS.Application.Common.PemsOperation("EquipmentFunctionalLocationAssignment", "Commit", "EquipmentFunctionalLocationAssignment.Approve")]
    Task<(bool Success, string? Error)> CommitAsync(int batchId);

    [PEMS.Application.Common.PemsOperation("EquipmentFunctionalLocationAssignment", "Discard", "EquipmentFunctionalLocationAssignment.Delete")]
    Task<(bool Success, string? Error)> DiscardAsync(int batchId);
}
