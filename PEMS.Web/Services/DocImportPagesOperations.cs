using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-830: the VDR and MDR import pages as read operations over the same services as the controllers (see <see cref="IDocImportPagesOperations"/>).</summary>
public sealed class DocImportPagesOperations(IVDRImportService vdr, IMDRImportService mdr, PEMSDbContext context) : IDocImportPagesOperations
{
    private async Task<List<DocImportChoice>> ProjectsAsync()
        => await context.Projects.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.ProjectName).Select(x => new DocImportChoice(x.ProjectId.ToString(), x.ProjectName)).ToListAsync();

    public async Task<List<DocImportBatchRow>> VdrBatchesAsync()
        => (await vdr.GetRecentBatchesAsync()).Select(b => new DocImportBatchRow(b.VDRImportBatchId, b.FileName, b.ProjectName, b.ImportDate, b.RowsTotal, b.RowsValid, b.RowsWarning, b.RowsError, b.RowsDuplicate, b.Status)).ToList();

    public async Task<List<DocImportHeadRow>> VdrBatchAsync(int batchId)
        => await vdr.GetBatchAsync(batchId) is { } b ? [new DocImportHeadRow(b.VDRImportBatchId, b.FileName, b.ProjectName, b.RowsTotal, b.RowsValid, b.RowsWarning, b.RowsError, b.RowsDuplicate, b.Status, b.Status == "Staged", null)] : [];

    public async Task<List<VdrImportRow>> VdrRowsAsync(int batchId)
    {
        var b = await vdr.GetBatchAsync(batchId);
        if (b == null) return [];
        var staged = b.Status == "Staged";
        return b.Rows.Select(r => new VdrImportRow(r.VDRImportRowId, r.RowNumber, r.IncludeInCommit, r.DocumentNumber, r.VendorNameRaw, r.Payload?.DocumentTypeCode, r.Payload?.DisciplineCodeRaw, r.Payload?.Submissions.Count ?? 0,
            r.ValidationStatus, r.ValidationMessage, r.IsDuplicate ? "Update existing" : "New", staged, staged && !r.IncludeInCommit, staged && r.IncludeInCommit)).ToList();
    }

    public Task<List<DocImportChoice>> VdrProjectOptionsAsync() => ProjectsAsync();

    public async Task<List<DocImportBatchRow>> MdrBatchesAsync()
        => (await mdr.GetRecentBatchesAsync()).Select(b => new DocImportBatchRow(b.MDRImportBatchId, b.FileName, b.ProjectName, b.ImportDate, b.RowsTotal, b.RowsValid, b.RowsWarning, b.RowsError, b.RowsDuplicate, b.Status)).ToList();

    public async Task<List<DocImportHeadRow>> MdrBatchAsync(int batchId)
        => await mdr.GetBatchAsync(batchId) is { } b ? [new DocImportHeadRow(b.MDRImportBatchId, b.FileName, b.ProjectName, b.RowsTotal, b.RowsValid, b.RowsWarning, b.RowsError, b.RowsDuplicate, b.Status, b.Status == "Staged",
            b.HeaderWarnings.Count == 0 ? null : string.Join(" | ", b.HeaderWarnings))] : [];

    public async Task<List<MdrImportRow>> MdrRowsAsync(int batchId)
    {
        var b = await mdr.GetBatchAsync(batchId);
        if (b == null) return [];
        var staged = b.Status == "Staged";
        return b.Rows.Select(r => new MdrImportRow(r.MDRImportRowId, r.RowNumber, r.IncludeInCommit, r.SheetName, r.DocumentNumber, r.Payload?.Title, r.Payload?.Scope, r.Payload?.Area, r.Payload?.DisciplineCodeRaw,
            r.Payload?.Revisions.LastOrDefault()?.RevisionNo, r.Payload?.Revisions.LastOrDefault()?.CommentStatus, r.Payload?.FinalStatusRaw, r.Payload?.Remark, r.Payload?.IsFrozen == true ? "Frozen" : "",
            r.ValidationStatus, r.ValidationMessage, r.IsDuplicate ? "Update existing" : "New", staged && !r.IncludeInCommit, staged && r.IncludeInCommit)).ToList();
    }

    public Task<List<DocImportChoice>> MdrProjectOptionsAsync() => ProjectsAsync();
}
