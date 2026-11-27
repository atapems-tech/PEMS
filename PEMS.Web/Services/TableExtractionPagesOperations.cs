using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PEMS.Application.AI.Interfaces;
using PEMS.Application.AI.Models;
using PEMS.Application.AI.Parser;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-830: the table extraction pages as read operations over the same services as the controller (see <see cref="ITableExtractionPagesOperations"/>).</summary>
public sealed class TableExtractionPagesOperations(PEMSDbContext context, IDocumentAIService ai, IMemoryCache cache, ICurrentUserService currentUser) : ITableExtractionPagesOperations
{
    private static string PhysicalPath(DocumentAttachment attachment) => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/', '\\'));

    public async Task<List<TableAttachmentRow>> AttachmentsAsync()
        => (await context.DocumentAttachments.AsNoTracking().Where(x => x.IsActive).OrderByDescending(x => x.UploadDate).ThenByDescending(x => x.AttachmentId).Take(2000)
            .Select(x => new { x.AttachmentId, x.FileName, x.FileExtension, x.UploadDate, x.AIStatus }).ToListAsync())
            .Select(x => new TableAttachmentRow(x.AttachmentId, x.FileName, x.FileExtension, x.UploadDate, x.AIStatus, DeterministicTableExtractor.CanHandle(x.FileExtension))).ToList();

    private async Task<(DocumentAttachment? Attachment, AIAnalysisResult? Result)> RunAsync(int attachmentId, string? mode, string? hint)
    {
        var attachment = await context.DocumentAttachments.AsNoTracking().FirstOrDefaultAsync(x => x.AttachmentId == attachmentId);
        if (attachment == null) return (null, null);
        var noAi = string.Equals(mode, "nai", StringComparison.OrdinalIgnoreCase);
        var key = $"table-extraction:{currentUser.UserId}:{attachmentId}:{(noAi ? "nai" : "ai")}:{hint}";
        if (cache.TryGetValue(key, out AIAnalysisResult? cached) && cached != null) return (attachment, cached);
        AIAnalysisResult result;
        var path = PhysicalPath(attachment);
        if (noAi && !DeterministicTableExtractor.CanHandle(attachment.FileExtension))
            result = new AIAnalysisResult { Success = false, ErrorMessage = $"استخراج بدون AI فقط برای PDF پشتیبانی می‌شود (فایل فعلی: {attachment.FileExtension})." };
        else if (!File.Exists(path))
            result = new AIAnalysisResult { Success = false, ErrorMessage = $"فایل روی دیسک یافت نشد: {path}" };
        else if (noAi)
        {
            try
            {
                var tableData = await Task.Run(() => DeterministicTableExtractor.Extract(path));
                result = new AIAnalysisResult { Success = true, Summary = tableData.Summary, Confidence = 1.0, UsedOcr = false, Warnings = tableData.Warnings, ExtractedTableData = tableData };
            }
            catch (Exception ex) { result = new AIAnalysisResult { Success = false, ErrorMessage = $"خطا در استخراج بدون AI: {ex.Message}" }; }
        }
        else result = await ai.AnalyzeTableDocumentAsync(path, attachment.FileExtension, string.IsNullOrWhiteSpace(hint) ? null : hint);
        cache.Set(key, result, TimeSpan.FromMinutes(10));
        return (attachment, result);
    }

    public async Task<List<TableAnalysisHead>> AnalysisAsync(int attachmentId, string? mode = null, string? hint = null)
    {
        var (a, r) = await RunAsync(attachmentId, mode, hint);
        if (a == null || r == null) return [];
        var t = r.ExtractedTableData; var i = t?.DocumentInfo;
        return [new TableAnalysisHead(attachmentId, a.FileName, r.Success, r.Success ? null : r.ErrorMessage, r.Summary, t?.RowCount ?? 0, r.Confidence.ToString("P0"), r.UsedOcr ? "Yes" : "No", r.Warnings.Count,
            i?.DocumentNo, i?.Title, i?.DocumentType, i?.Discipline, i?.RevisionNo, i?.VendorName, i == null ? null : $"{i.Area ?? "N/A"} / {i.Unit ?? "N/A"}", i?.System, r.Success && t != null && t.Rows.Any())];
    }

    public async Task<List<TableAnalysisWarning>> AnalysisWarningsAsync(int attachmentId, string? mode = null, string? hint = null)
    {
        var (_, r) = await RunAsync(attachmentId, mode, hint);
        var n = 0;
        return r == null ? [] : r.Warnings.Select(w => new TableAnalysisWarning(++n, w)).ToList();
    }

    public async Task<List<TableAnalysisRow>> AnalysisRowsAsync(int attachmentId, string? mode = null, string? hint = null)
    {
        var (_, r) = await RunAsync(attachmentId, mode, hint);
        var n = 0;
        return r?.ExtractedTableData == null ? [] : r.ExtractedTableData.Rows.Select(row => new TableAnalysisRow(++n, row.TagNumber ?? "-", row.Description ?? row.Service ?? "-",
            $"{row.SignalType ?? "-"} {(row.SignalClass != null ? $"({row.SignalClass})" : "")}".Trim(), $"{row.JunctionBox ?? "-"} / {row.Terminal ?? "-"}", row.CableNo ?? "-", $"{row.MarshallingCabinet ?? "-"} / {row.MarshallingTerminal ?? "-"}",
            $"{row.IoCard ?? "-"} / {row.IoChannel ?? "-"}", row.LoopNo ?? "-", row.System ?? "-", row.SpareOrHotSpare == true ? "Yes" : "-", row.Remarks ?? "-",
            row.RawExtra != null && row.RawExtra.Any() ? string.Join("; ", row.RawExtra.Select(kv => $"{kv.Key}={kv.Value}")) : "-")).ToList();
    }

    public async Task<List<TableBatchHead>> BatchHeadAsync(int batchId)
    {
        var b = await context.TableExtractionBatches.AsNoTracking().Include(x => x.Rows).FirstOrDefaultAsync(x => x.TableExtractionBatchId == batchId);
        if (b == null) return [];
        int C(string s) => b.Rows.Count(r => r.MatchStatus == s);
        return [new TableBatchHead(batchId, b.SourceFileName, b.TableExtractionBatchId, b.ExtractionMethod, b.TotalRows, b.CreatedDate, C("Unmatched"), C("MatchedNoInstrument"), C("MatchedHasInstrument"), C("Confirmed"), C("Rejected"))];
    }

    public async Task<List<TableBatchRow>> BatchRowsAsync(int batchId)
    {
        var rows = await context.TableExtractionRows.AsNoTracking().Include(r => r.MatchedEquipment).Where(r => r.TableExtractionBatchId == batchId).ToListAsync();
        return rows.OrderBy(r => r.MatchStatus == "Confirmed" || r.MatchStatus == "Rejected" ? 1 : 0).ThenBy(r => r.TagNumber)
            .Select(r => new TableBatchRow(r.TableExtractionRowId, r.TagNumber ?? "-", r.Description, r.JunctionBox, r.CableNo, r.DcsTagNo, r.MatchStatus, r.MatchedEquipment?.TagNumber, r.ResultInstrumentId is { } iid ? "#" + iid : null, r.MatchedEquipmentId,
                r.MatchStatus is "MatchedNoInstrument" or "MatchedHasInstrument", r.MatchStatus == "Unmatched")).ToList();
    }
}
