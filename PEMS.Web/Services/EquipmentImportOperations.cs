using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-742: the toggle, commit and discard of EquipmentImportController over IEquipmentImportService.</summary>
public sealed class EquipmentImportOperations(IEquipmentImportService service, PEMSDbContext context) : IEquipmentImportOperations
{
    public async Task<(bool Success, string? Error)> ToggleRowAsync(int rowId, bool include) { await service.SetRowIncludeAsync(rowId, include); return (true, null); }
    public async Task<(bool Success, string? Error)> CommitAsync(int batchId) { await service.CommitBatchAsync(batchId); return (true, null); }
    public async Task<(bool Success, string? Error)> DiscardAsync(int batchId) { await service.DiscardBatchAsync(batchId); return (true, null); }

    private static EquipmentImportBatchRow Row(EquipmentImportBatchDto b)
        => new(b.EquipmentImportBatchId, b.FileName, b.ProjectName ?? "", b.ImportDate, b.Rows.Count, b.Rows.Count(x => !x.IsUpdate), b.Rows.Count(x => x.IsUpdate), b.Status);

    public async Task<List<EquipmentImportBatchRow>> BatchesAsync() => (await service.GetRecentBatchesAsync()).Select(Row).ToList();

    public async Task<List<EquipmentImportBatchRow>> BatchAsync(int batchId) => await service.GetBatchAsync(batchId) is { } batch ? [Row(batch)] : [];

    public async Task<List<EquipmentImportRowItem>> RowsAsync(int batchId)
        => (await service.GetBatchAsync(batchId))?.Rows.Select(r => new EquipmentImportRowItem(r.EquipmentImportRowId, r.RowNumber, r.TagNumber, r.Name, r.Payload?.EquipmentTypeCode, r.DisciplineCodeRaw, r.Payload?.Area, r.Payload?.PIDNumber,
            r.Payload?.MRNumber, r.Payload?.PONumber, r.ValidationStatus, r.ValidationMessage, r.IncludeInCommit, r.IsUpdate ? "Update existing" : "New")).ToList() ?? [];

    public async Task<List<EquipmentImportChoice>> ProjectOptionsAsync()
        => await context.Projects.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.ProjectName).Select(x => new EquipmentImportChoice(x.ProjectId.ToString(), x.ProjectName)).ToListAsync();

    public async Task<int> UploadAsync(int projectId, byte[] file, string? fileName = null, int? employeeId = null)
    {
        using var stream = new MemoryStream(file);
        return await service.StageFromExcelAsync(stream, string.IsNullOrWhiteSpace(fileName) ? "equipment.xlsx" : fileName, projectId, employeeId);
    }
}
