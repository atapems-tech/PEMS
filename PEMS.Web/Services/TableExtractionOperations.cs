using Microsoft.EntityFrameworkCore;
using PEMS.Application.AI.Models;
using PEMS.Application.AI.Parser;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-753: the review commands of the table extraction pages over the import service (the texts of the controller are kept).</summary>
public sealed class TableExtractionOperations(PEMSDbContext context, ITableExtractionImportService import) : ITableExtractionOperations
{
    // attachment.FilePath is a web path (/uploads/intake/x.pdf), not a disk path
    private static string PhysicalPath(DocumentAttachment attachment) => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/', '\\'));

    private static TableExtractionRowInput ToInput(ExtractedTableRow r) => new()
    {
        TagNumber = r.TagNumber, Description = r.Description, JunctionBox = r.JunctionBox, Terminal = r.Terminal, CableNo = r.CableNo, MarshallingCabinet = r.MarshallingCabinet, MarshallingTerminal = r.MarshallingTerminal,
        SystemCabinet = r.SystemCabinet, IoCard = r.IoCard, IoChannel = r.IoChannel, IoAddress = r.IoAddress, DcsTagNo = r.DcsTagNo, LoopNo = r.LoopNo, System = r.System, SignalClass = r.SignalClass,
        SpareOrHotSpare = r.SpareOrHotSpare, Remarks = r.Remarks, RawExtra = r.RawExtra
    };

    public async Task<int> StageForReviewAsync(int attachmentId, int? employeeId)
    {
        var attachment = await context.DocumentAttachments.FirstOrDefaultAsync(x => x.AttachmentId == attachmentId) ?? throw new InvalidOperationException("The file was not found.");
        if (!DeterministicTableExtractor.CanHandle(attachment.FileExtension)) throw new InvalidOperationException($"Stage کردن فقط برای PDF پشتیبانی می‌شود (فایل فعلی: {attachment.FileExtension}).");
        var path = PhysicalPath(attachment);
        if (!File.Exists(path)) throw new InvalidOperationException($"فایل روی دیسک یافت نشد: {path}");
        try
        {
            var tableData = await Task.Run(() => DeterministicTableExtractor.Extract(path));
            return await import.StageAsync(attachmentId, "Deterministic", tableData.Rows.Select(ToInput).ToList(), employeeId);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"خطا در استخراج/Stage: {ex.Message}", ex);
        }
    }

    public async Task<(bool Success, string? Error)> ConfirmRowAsync(int rowId, int equipmentId, int? employeeId)
    {
        await import.ConfirmRowAsync(rowId, equipmentId, employeeId);
        return (true, "تایید شد و به Instrument متصل شد.");
    }

    public async Task<(bool Success, string? Error)> BulkConfirmAsync(List<int> selectedRowIds, int? employeeId)
    {
        if (selectedRowIds.Count == 0) return (false, "هیچ ردیفی انتخاب نشده.");
        var (confirmed, errors) = await import.BulkConfirmMatchedAsync(selectedRowIds, employeeId);
        var message = $"{confirmed} ردیف تایید و به Instrument متصل شد.";
        if (errors.Count > 0) message += $" {errors.Count} ردیف خطا داشت: " + string.Join(" | ", errors.Take(5));
        return (true, message);
    }

    public async Task<(bool Success, string? Error)> ConfirmRowManualAsync(int rowId, string equipmentTagNumber, int? employeeId)
    {
        var equipment = await context.Equipments.FirstOrDefaultAsync(e => e.TagNumber == equipmentTagNumber);
        if (equipment is null) return (false, $"هیچ Equipment ای با تگ «{equipmentTagNumber}» پیدا نشد.");
        await import.ConfirmRowAsync(rowId, equipment.EquipmentId, employeeId);
        return (true, "تایید شد و به Instrument متصل شد.");
    }

    public async Task<(bool Success, string? Error)> RejectRowAsync(int rowId, string? comment, int? employeeId)
    {
        await import.RejectRowAsync(rowId, employeeId, comment);
        return (true, null);
    }
}
