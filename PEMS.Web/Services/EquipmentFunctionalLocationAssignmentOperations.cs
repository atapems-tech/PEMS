using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-694: the equipment to functional location assignment import over IEquipmentFunctionalLocationAssignmentService (the same calls; the file name comes from a field because the upload carries bytes only).</summary>
public sealed class EquipmentFunctionalLocationAssignmentOperations(IEquipmentFunctionalLocationAssignmentService service) : IEquipmentFunctionalLocationAssignmentOperations
{
    private const int RowLimit = 5000;
    private const string Fallback = "import.xlsx";

    private static EquipmentFunctionalLocationAssignmentBatchRow Row(EquipmentFunctionalLocationAssignmentBatchSummaryDto b)
        => new(b.EquipmentFunctionalLocationAssignmentBatchId, b.FileName, b.ImportDate, b.Status, b.ImportedByName ?? "", b.TotalRows, b.ExactCount, b.AlreadyAssignedCount, b.NoChangeCount, b.UnresolvedCount, b.ConflictCount, b.ValidationErrorCount);

    public async Task<List<EquipmentFunctionalLocationAssignmentBatchRow>> BatchesAsync() => (await service.GetRecentBatchesAsync()).Select(Row).ToList();

    public async Task<List<EquipmentFunctionalLocationAssignmentBatchRow>> BatchAsync(int batchId) => await service.GetBatchSummaryAsync(batchId) is { } b ? [Row(b)] : [];

    public async Task<List<EquipmentFunctionalLocationAssignmentRowItem>> RowsAsync(int batchId, string? status = null)
        => (await service.GetBatchAsync(batchId, 1, RowLimit, string.IsNullOrWhiteSpace(status) ? null : status))?.Rows.Items
            .Select(r => new EquipmentFunctionalLocationAssignmentRowItem(r.EquipmentFunctionalLocationAssignmentRowId, r.RowNumber, r.SourceRowNumber, r.EquipmentTag, r.EquipmentDescription ?? "",
                r.CurrentFunctionalLocationBreadcrumb ?? "", r.ProposedFunctionalLocationBreadcrumb ?? r.FunctionalLocationCode, r.Status, r.ValidationStatus, r.ValidationMessage ?? "", r.IncludeInCommit, r.ReplaceExistingAssignment)).ToList() ?? [];

    public async Task<(bool Success, string? Error)> UploadAsync(int? employeeId, byte[] file, string? fileName = null)
    {
        if (file is null || file.Length == 0) return (false, "Please select an Equipment ↔ Functional Location assignment Excel file.");
        await using var stream = new MemoryStream(file);
        await service.StageFromExcelAsync(stream, string.IsNullOrWhiteSpace(fileName) ? Fallback : Path.GetFileName(fileName), employeeId);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ToggleRowAsync(int rowId, bool include) { await service.SetRowIncludeAsync(rowId, include); return (true, null); }
    public async Task<(bool Success, string? Error)> ReplaceExistingAsync(int rowId, bool replace) { await service.SetReplaceExistingAssignmentAsync(rowId, replace); return (true, null); }
    public async Task<(bool Success, string? Error)> CommitAsync(int batchId) { await service.CommitBatchAsync(batchId); return (true, null); }
    public async Task<(bool Success, string? Error)> DiscardAsync(int batchId) { await service.DiscardBatchAsync(batchId); return (true, null); }
}
