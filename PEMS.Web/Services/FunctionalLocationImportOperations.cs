using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-701: the functional location import over IFunctionalLocationImportService (the same calls; the file name comes from a field because the upload carries bytes only).</summary>
public sealed class FunctionalLocationImportOperations(IFunctionalLocationImportService service) : IFunctionalLocationImportOperations
{
    private const string Fallback = "import.xlsx";

    private static FunctionalLocationImportBatchRow Row(FunctionalLocationImportBatchDto b)
        => new(b.FunctionalLocationImportBatchId, b.FileName, b.ImportDate, b.Status, b.ImportedByName ?? "", b.SourceRowCount, b.ExpandedLocationCount, b.NewCount, b.ExistingCount, b.ConflictCount, b.InvalidCount);

    public async Task<List<FunctionalLocationImportBatchRow>> BatchesAsync() => (await service.GetRecentBatchesAsync()).Select(Row).ToList();

    public async Task<List<FunctionalLocationImportBatchRow>> BatchAsync(int batchId) => await service.GetBatchAsync(batchId) is { } b ? [Row(b)] : [];

    public async Task<List<FunctionalLocationImportRowItem>> RowsAsync(int batchId, string? status = null)
        => (await service.GetBatchAsync(batchId))?.Rows
            .Where(r => string.IsNullOrWhiteSpace(status) || string.Equals(r.Status, status, StringComparison.OrdinalIgnoreCase))
            .Select(r => new FunctionalLocationImportRowItem(r.FunctionalLocationImportRowId, r.RowNumber, r.SourceRowNumber, r.Code, r.Name, r.ParentAreaName ?? r.Name, r.Status, r.Action, r.ValidationStatus, r.ValidationMessage ?? "", r.IncludeInCommit)).ToList() ?? [];

    public async Task<(bool Success, string? Error)> UploadAsync(int? employeeId, byte[] file, string? fileName = null)
    {
        if (file is null || file.Length == 0) return (false, "Please select a Functional Location Excel file.");
        await using var stream = new MemoryStream(file);
        await service.StageFromExcelAsync(stream, string.IsNullOrWhiteSpace(fileName) ? Fallback : Path.GetFileName(fileName), employeeId);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ToggleRowAsync(int rowId, bool include) { await service.SetRowIncludeAsync(rowId, include); return (true, null); }

    public async Task<(bool Success, string? Error)> CommitAsync(int batchId)
    {
        await service.CommitBatchAsync(batchId);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DiscardAsync(int batchId) { await service.DiscardBatchAsync(batchId); return (true, null); }
}
