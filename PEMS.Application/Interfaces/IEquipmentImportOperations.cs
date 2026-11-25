namespace PEMS.Application.Interfaces;

public sealed record EquipmentImportBatchRow(int BatchId, string FileName, string ProjectName, DateTime ImportDate, int Rows, int NewCount, int UpdateCount, string Status);
public sealed record EquipmentImportRowItem(int RowId, int RowNumber, string TagNumber, string? Name, string? EquipmentType, string? Discipline, string? Area, string? PidNumber, string? MrNumber, string? PoNumber, string ValidationStatus,
    string? ValidationMessage, bool IncludeInCommit, string Action);
public sealed record EquipmentImportChoice(string Value, string Label);

/// <summary>The review-step commands of EquipmentImportController as operations (SCRUM-742): include or exclude a staged row, commit the batch (the highest-risk step: it creates and changes real equipment) and discard it. Permissions as the controller checked them (EquipmentImport Create, Approve, Delete).</summary>
public interface IEquipmentImportOperations
{
    [PEMS.Application.Common.PemsOperation("EquipmentImport", "ToggleRow", "EquipmentImport.Create")]
    Task<(bool Success, string? Error)> ToggleRowAsync(int rowId, bool include);

    [PEMS.Application.Common.PemsOperation("EquipmentImport", "Commit", "EquipmentImport.Approve")]
    Task<(bool Success, string? Error)> CommitAsync(int batchId);

    [PEMS.Application.Common.PemsOperation("EquipmentImport", "Discard", "EquipmentImport.Delete")]
    Task<(bool Success, string? Error)> DiscardAsync(int batchId);

    // ---- SCRUM-695: the reads of the batch pages and the project upload (the PDF scan stays in the controller)
    [PEMS.Application.Common.PemsOperation("EquipmentImport", "Batches", "EquipmentImport.View")]
    Task<List<EquipmentImportBatchRow>> BatchesAsync();

    [PEMS.Application.Common.PemsOperation("EquipmentImport", "Batch", "EquipmentImport.View")]
    Task<List<EquipmentImportBatchRow>> BatchAsync(int batchId);

    [PEMS.Application.Common.PemsOperation("EquipmentImport", "Rows", "EquipmentImport.View")]
    Task<List<EquipmentImportRowItem>> RowsAsync(int batchId);

    [PEMS.Application.Common.PemsOperation("EquipmentImport", "ProjectOptions", "EquipmentImport.View")]
    Task<List<EquipmentImportChoice>> ProjectOptionsAsync();

    /// <summary>Stages the workbook of a project into a batch; the answer is the batch id (the action opens its review page).</summary>
    [PEMS.Application.Common.PemsOperation("EquipmentImport", "Upload", "EquipmentImport.Create")]
    Task<int> UploadAsync(int projectId, byte[] file, string? fileName = null, int? employeeId = null);
}
