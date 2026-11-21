using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-684, 686, 732: the reads of the revision history, the document approval queue and the OCR queue, and the OCR retry (same filters, same order as the three controllers).</summary>
public sealed class DocumentFlowOperations(PEMSDbContext context) : IDocumentFlowOperations
{
    private static string Name(string? first, string? last) => ((first ?? "") + " " + (last ?? "")).Trim();

    public async Task<List<DocumentRevisionRow>> RevisionsAsync(int? documentId = null)
    {
        var query = context.DocumentRevisions.AsNoTracking().Where(x => x.IsActive);
        if (documentId.HasValue) query = query.Where(x => x.DocumentId == documentId.Value);
        var rows = await query.OrderByDescending(x => x.RevisionDate).ThenByDescending(x => x.RevisionId).Take(2000)
            .Select(x => new
            {
                x.RevisionId, x.DocumentId, DocumentNo = x.Document != null ? x.Document.DocumentNo : null, x.RevisionNo, x.PreviousRevisionNo, x.Status, x.IssuePurpose, x.RevisionDate,
                Prepared = x.PreparedBy != null ? x.PreparedBy.FirstName + " " + x.PreparedBy.LastName : "",
                Checked = x.CheckedBy != null ? x.CheckedBy.FirstName + " " + x.CheckedBy.LastName : "",
                Approved = x.ApprovedBy != null ? x.ApprovedBy.FirstName + " " + x.ApprovedBy.LastName : ""
            }).ToListAsync();
        return rows.Select(x => new DocumentRevisionRow(x.RevisionId, x.DocumentId, x.DocumentNo ?? "", x.RevisionNo ?? "", x.PreviousRevisionNo ?? "", x.Status ?? "", x.IssuePurpose ?? "", x.RevisionDate, x.Prepared, x.Checked, x.Approved)).ToList();
    }

    public async Task<List<ReportChoiceRow>> DocumentChoicesAsync()
        => (await context.EngineeringDocuments.AsNoTracking().OrderBy(x => x.DocumentNo).Select(x => new { x.DocumentId, x.DocumentNo, x.Title }).ToListAsync())
            .Select(x => new ReportChoiceRow(x.DocumentId.ToString(CultureInfo.InvariantCulture), x.DocumentNo + " - " + x.Title)).ToList();

    public async Task<List<PendingDocumentRow>> PendingAsync(string? status = null)
    {
        var query = context.EngineeringDocuments.AsNoTracking().Where(x => x.IsActive);
        // without a filter both waiting stages show: Submitted (waits for the check) and Checked (waits for the approval)
        query = string.IsNullOrWhiteSpace(status) ? query.Where(x => x.Status == "Submitted" || x.Status == "Checked") : query.Where(x => x.Status == status);
        var rows = await query.OrderBy(x => x.ModifiedDate ?? x.CreatedDate).ThenBy(x => x.DocumentId).Take(2000)
            .Select(x => new { x.DocumentId, x.DocumentNo, x.Title, Project = x.Project != null ? x.Project.ProjectName : null, Discipline = x.Discipline != null ? x.Discipline.Name : null, x.Status, x.RevisionNo }).ToListAsync();
        return rows.Select(x => new PendingDocumentRow(x.DocumentId, x.DocumentNo ?? "", x.Title ?? "", x.Project ?? "", x.Discipline ?? "", x.Status ?? "", x.RevisionNo ?? "")).ToList();
    }

    public async Task<List<WorkflowHistoryRow>> HistoryAsync()
    {
        var rows = await context.DocumentWorkflows.AsNoTracking().OrderByDescending(x => x.ActionDate).Take(30)
            .Select(x => new { x.WorkflowId, DocumentNo = x.Document != null ? x.Document.DocumentNo : null, x.ActionType, x.FromStatus, x.ToStatus, First = x.ActionBy != null ? x.ActionBy.FirstName : null, Last = x.ActionBy != null ? x.ActionBy.LastName : null, x.ActionDate, x.Comment }).ToListAsync();
        return rows.Select(x => new WorkflowHistoryRow(x.WorkflowId, x.DocumentNo ?? "", x.ActionType ?? "", x.FromStatus ?? "", x.ToStatus ?? "", x.First is null ? "—" : Name(x.First, x.Last), x.ActionDate, x.Comment ?? "")).ToList();
    }

    public async Task<List<OcrAttachmentRow>> QueueAsync(string? status = null)
    {
        var query = context.DocumentAttachments.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.AIStatus == status);
        var rows = await query.OrderByDescending(x => x.UploadDate).ThenByDescending(x => x.AttachmentId).Take(2000)
            .Select(x => new { x.AttachmentId, x.FileName, DocumentNo = x.Document != null ? x.Document.DocumentNo : null, x.FileExtension, x.FileSize, x.AIStatus, x.AISummary, x.AIError, x.UploadDate }).ToListAsync();
        return rows.Select(x => new OcrAttachmentRow(x.AttachmentId, x.FileName ?? "", x.DocumentNo ?? "", x.FileExtension ?? "", x.FileSize, x.AIStatus ?? "", x.AISummary ?? "", x.AIError ?? "", x.UploadDate)).ToList();
    }

    public async Task<OcrCounts> CountsAsync()
    {
        var counts = (await context.DocumentAttachments.AsNoTracking().GroupBy(x => x.AIStatus).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync()).ToDictionary(x => x.Status ?? "", x => x.Count);
        int Of(string key) => counts.TryGetValue(key, out var n) ? n : 0;
        return new OcrCounts(Of("Pending"), Of("Processing"), Of("Completed"), Of("Failed"));
    }

    public Task<(bool Success, string? Error)> RetryAsync(int id)
    {
        Hangfire.BackgroundJob.Enqueue<IDocumentAIIngestionService>(x => x.ProcessAttachmentAsync(id));
        return Task.FromResult<(bool Success, string? Error)>((true, null));
    }
}
