using PEMS.Application.Common;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-719, 762: the review commands of the MDR and VDR imports over their import services (same texts as the controllers had).</summary>
public sealed class ImportReviewOperations(IMDRImportService mdr, IVDRImportService vdr) : IImportReviewOperations
{
    private static (bool Success, string? Error) Ok(string? message = null) => (true, message);

    public async Task<(bool Success, string? Error)> ToggleMdrRowAsync(int rowId, bool include) { await mdr.SetRowIncludeAsync(rowId, include); return Ok(); }

    public async Task<(bool Success, string? Error)> CommitMdrAsync(int batchId)
    {
        var (created, updated, skipped) = await mdr.CommitBatchAsync(batchId);
        return Ok($"MDR Import committed: {created} created, {updated} updated, {skipped} skipped.");
    }

    public async Task<(bool Success, string? Error)> DiscardMdrAsync(int batchId) { await mdr.DiscardBatchAsync(batchId); return Ok("MDR import batch discarded."); }

    public async Task<(bool Success, string? Error)> RematchMdrDisciplinesAsync(int? projectId)
    {
        var count = await mdr.RematchDisciplinesAsync(projectId);
        return Ok(count > 0 ? $"Discipline matched for {count} previously imported document(s)." : "No documents needed discipline re-matching.");
    }

    public async Task<(bool Success, string? Error)> FixMdrLegacyStatusesAsync(int? projectId)
    {
        var count = await mdr.FixLegacyStatusValuesAsync(projectId);
        return Ok(count > 0 ? $"Status corrected for {count} previously imported document(s)." : "No documents needed a status fix.");
    }

    public async Task<int> UploadMdrAsync(OperationFile? file, int projectId, int? employeeId)
    {
        if (file is null || file.Bytes.Length == 0) throw new InvalidOperationException("Please select an MDR Excel file.");
        await using var stream = new MemoryStream(file.Bytes);
        return await mdr.StageFromExcelAsync(stream, file.FileName, projectId, employeeId);
    }

    public async Task<int> UploadVdrAsync(OperationFile? file, int projectId, int? employeeId)
    {
        if (file is null || file.Bytes.Length == 0) throw new InvalidOperationException("Please select a VDR Excel file.");
        await using var stream = new MemoryStream(file.Bytes);
        return await vdr.StageFromExcelAsync(stream, file.FileName, projectId, employeeId);
    }

    public async Task<(bool Success, string? Error)> ToggleVdrRowAsync(int rowId, bool include) { await vdr.SetRowIncludeAsync(rowId, include); return Ok(); }

    public async Task<(bool Success, string? Error)> CommitVdrAsync(int batchId)
    {
        var (created, updated, skipped) = await vdr.CommitBatchAsync(batchId);
        return Ok($"Import committed: {created} created, {updated} updated, {skipped} skipped.");
    }

    public async Task<(bool Success, string? Error)> DiscardVdrAsync(int batchId) { await vdr.DiscardBatchAsync(batchId); return Ok("Import batch discarded."); }
}
