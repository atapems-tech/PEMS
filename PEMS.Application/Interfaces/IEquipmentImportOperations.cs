namespace PEMS.Application.Interfaces;

/// <summary>The review-step commands of EquipmentImportController as operations (SCRUM-742): include or exclude a staged row, commit the batch (the highest-risk step: it creates and changes real equipment) and discard it. Permissions as the controller checked them (EquipmentImport Create, Approve, Delete).</summary>
public interface IEquipmentImportOperations
{
    [PEMS.Application.Common.PemsOperation("EquipmentImport", "ToggleRow", "EquipmentImport.Create")]
    Task<(bool Success, string? Error)> ToggleRowAsync(int rowId, bool include);

    [PEMS.Application.Common.PemsOperation("EquipmentImport", "Commit", "EquipmentImport.Approve")]
    Task<(bool Success, string? Error)> CommitAsync(int batchId);

    [PEMS.Application.Common.PemsOperation("EquipmentImport", "Discard", "EquipmentImport.Delete")]
    Task<(bool Success, string? Error)> DiscardAsync(int batchId);
}
