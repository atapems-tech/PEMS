namespace PEMS.Application.Interfaces;

/// <summary>The review-step commands of EquipmentFunctionalLocationAssignmentController as operations (SCRUM-694): include or exclude a staged row (Create), allow replacing an existing assignment (Approve: kept apart from the plain toggle so it is never switched on by accident), commit the batch (Approve; it changes the real Equipment.FunctionalLocationId) and discard it (Delete). Permissions as the controller checked them.</summary>
public interface IEquipmentFunctionalLocationAssignmentOperations
{
    [PEMS.Application.Common.PemsOperation("EquipmentFunctionalLocationAssignment", "ToggleRow", "EquipmentFunctionalLocationAssignment.Create")]
    Task<(bool Success, string? Error)> ToggleRowAsync(int rowId, bool include);

    [PEMS.Application.Common.PemsOperation("EquipmentFunctionalLocationAssignment", "ReplaceExisting", "EquipmentFunctionalLocationAssignment.Approve")]
    Task<(bool Success, string? Error)> ReplaceExistingAsync(int rowId, bool replace);

    [PEMS.Application.Common.PemsOperation("EquipmentFunctionalLocationAssignment", "Commit", "EquipmentFunctionalLocationAssignment.Approve")]
    Task<(bool Success, string? Error)> CommitAsync(int batchId);

    [PEMS.Application.Common.PemsOperation("EquipmentFunctionalLocationAssignment", "Discard", "EquipmentFunctionalLocationAssignment.Delete")]
    Task<(bool Success, string? Error)> DiscardAsync(int batchId);
}
