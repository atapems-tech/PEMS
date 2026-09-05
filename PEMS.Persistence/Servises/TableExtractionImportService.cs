using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class TableExtractionImportService : ITableExtractionImportService
{
    private readonly PEMSDbContext _context;

    public TableExtractionImportService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<int> StageAsync(int attachmentId, string extractionMethod, List<TableExtractionRowInput> rows, int? createdById)
    {
        var attachment = await _context.DocumentAttachments.FirstOrDefaultAsync(x => x.AttachmentId == attachmentId)
            ?? throw new InvalidOperationException($"پیوست {attachmentId} یافت نشد.");

        // تمام تگ‌های Equipment و Instrument های موجود یک‌بار در حافظه بارگذاری می‌شوند تا
        // برای هر ردیف Query جدا زده نشود (N+1)
        var equipmentByNormalizedTag = await _context.Equipments
            .Select(e => new { e.EquipmentId, e.TagNumber })
            .ToListAsync();
        var normalizedLookup = equipmentByNormalizedTag
            .GroupBy(e => NormalizeTag(e.TagNumber))
            .ToDictionary(g => g.Key, g => g.First().EquipmentId);

        var equipmentIdsWithInstrument = (await _context.Instruments.Select(i => i.EquipmentId).ToListAsync()).ToHashSet();

        var batch = new TableExtractionBatch
        {
            SourceAttachmentId = attachmentId,
            SourceFileName = attachment.FileName,
            ExtractionMethod = extractionMethod,
            TotalRows = rows.Count,
            CreatedById = createdById,
            CreatedDate = DateTime.Now
        };

        foreach (var input in rows)
        {
            int? matchedEquipmentId = null;
            var status = "Unmatched";

            if (!string.IsNullOrWhiteSpace(input.TagNumber))
            {
                var normalized = NormalizeTag(input.TagNumber);
                if (normalizedLookup.TryGetValue(normalized, out var equipmentId))
                {
                    matchedEquipmentId = equipmentId;
                    status = equipmentIdsWithInstrument.Contains(equipmentId) ? "MatchedHasInstrument" : "MatchedNoInstrument";
                }
            }

            batch.Rows.Add(new TableExtractionRow
            {
                TagNumber = input.TagNumber,
                Description = input.Description,
                JunctionBox = input.JunctionBox,
                Terminal = input.Terminal,
                CableNo = input.CableNo,
                MarshallingCabinet = input.MarshallingCabinet,
                MarshallingTerminal = input.MarshallingTerminal,
                SystemCabinet = input.SystemCabinet,
                IoCard = input.IoCard,
                IoChannel = input.IoChannel,
                IoAddress = input.IoAddress,
                DcsTagNo = input.DcsTagNo,
                LoopNo = input.LoopNo,
                System = input.System,
                SignalClass = input.SignalClass,
                SpareOrHotSpare = input.SpareOrHotSpare,
                Remarks = input.Remarks,
                RawExtraJson = input.RawExtra is { Count: > 0 } ? JsonSerializer.Serialize(input.RawExtra) : null,
                MatchedEquipmentId = matchedEquipmentId,
                MatchStatus = status
            });
        }

        await _context.TableExtractionBatches.AddAsync(batch);
        await _context.SaveChangesAsync();
        return batch.TableExtractionBatchId;
    }

    public async Task ConfirmRowAsync(int rowId, int equipmentId, int? reviewedById)
    {
        var row = await _context.TableExtractionRows.FirstOrDefaultAsync(x => x.TableExtractionRowId == rowId)
            ?? throw new InvalidOperationException($"ردیف {rowId} یافت نشد.");

        if (row.MatchStatus == "Confirmed")
            throw new InvalidOperationException("این ردیف قبلاً تایید شده است.");

        var equipmentExists = await _context.Equipments.AnyAsync(e => e.EquipmentId == equipmentId);
        if (!equipmentExists)
            throw new InvalidOperationException($"Equipment {equipmentId} یافت نشد.");

        var instrument = await _context.Instruments.FirstOrDefaultAsync(i => i.EquipmentId == equipmentId);
        if (instrument == null)
        {
            instrument = new Instrument
            {
                EquipmentId = equipmentId,
                InstrumentType = row.SignalClass ?? string.Empty,
                CreatedDate = DateTime.Now
            };
            await _context.Instruments.AddAsync(instrument);
        }

        // فقط فیلدهای خالی پر می‌شوند - هیچ مقدار موجود (که ممکن است دستی وارد شده باشد) بازنویسی نمی‌شود
        instrument.JunctionBox ??= row.JunctionBox;
        instrument.MarshallingCabinet ??= row.MarshallingCabinet;
        instrument.CableNumber ??= row.CableNo;
        instrument.SystemCabinet ??= row.SystemCabinet;
        instrument.LoopNumber ??= row.LoopNo;
        instrument.System ??= row.System;
        instrument.SignalClass ??= row.SignalClass;

        await _context.SaveChangesAsync(); // instrument.InstrumentId را برای IOPoint لازم داریم

        if (!string.IsNullOrWhiteSpace(row.TagNumber))
        {
            await _context.IOPoints.AddAsync(new IOPoint
            {
                InstrumentId = instrument.InstrumentId,
                TagNumber = row.TagNumber,
                IOType = row.SignalClass ?? string.Empty,
                Channel = row.IoChannel,
                Address = row.IoAddress,
                Description = row.Description,
                CreatedDate = DateTime.Now
            });
        }

        row.MatchStatus = "Confirmed";
        row.ResultInstrumentId = instrument.InstrumentId;
        row.ReviewedById = reviewedById;
        row.ReviewedDate = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    public async Task RejectRowAsync(int rowId, int? reviewedById, string? comment)
    {
        var row = await _context.TableExtractionRows.FirstOrDefaultAsync(x => x.TableExtractionRowId == rowId)
            ?? throw new InvalidOperationException($"ردیف {rowId} یافت نشد.");

        row.MatchStatus = "Rejected";
        row.ReviewedById = reviewedById;
        row.ReviewedDate = DateTime.Now;
        row.ReviewComment = comment;

        await _context.SaveChangesAsync();
    }

    // طبق همان الگوی RelationshipService.BulkConfirmAsync - هر ردیف با EquipmentId پیشنهادی خودش
    // (نه یک انتخاب دستی مشترک) Confirm می‌شود؛ خطای یک ردیف بقیه را متوقف نمی‌کند
    public async Task<(int Confirmed, List<string> Errors)> BulkConfirmMatchedAsync(List<int> rowIds, int? reviewedById)
    {
        var confirmed = 0;
        var errors = new List<string>();

        foreach (var rowId in rowIds)
        {
            var row = await _context.TableExtractionRows.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TableExtractionRowId == rowId);

            if (row == null)
            {
                errors.Add($"ردیف {rowId}: یافت نشد.");
                continue;
            }

            if (row.MatchedEquipmentId == null)
            {
                errors.Add($"ردیف {rowId} ({row.TagNumber}): Equipment تطبیق‌یافته ندارد.");
                continue;
            }

            try
            {
                await ConfirmRowAsync(rowId, row.MatchedEquipmentId.Value, reviewedById);
                confirmed++;
            }
            catch (Exception ex)
            {
                errors.Add($"ردیف {rowId} ({row.TagNumber}): {ex.Message}");
            }
        }

        return (confirmed, errors);
    }

    // برای مقایسه‌ی تگ‌ها با نگارش‌های مختلف جداکننده - "80-ZSX-320109" و "80_ZSX_320109" باید یکی
    // تشخیص داده شوند؛ حروف/عدد نگه داشته می‌شود و بقیه حذف می‌شود
    private static string NormalizeTag(string tag) =>
        new string(tag.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
