using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق درخواست: "بخش CMMS باید بتواند تجهیزات را با فایل اکسل تشخیص دهد" - بر اساس دو نمونه‌ی
/// واقعی فایل (Mechanical/Instrument) که ستون‌بندی‌شان کاملاً متفاوت است، و حتی سطر عنوان
/// (Header) در یکی از آن‌ها ردیف ۲ است نه ۱ - تشخیص خودکار ردیف Header اضافه شد.
/// </summary>
public class EquipmentImportService : IEquipmentImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly PEMSDbContext _context;

    public EquipmentImportService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int projectId, int? importedById)
    {
        using var workbook = new XLWorkbook(fileStream);

        var batch = new EquipmentImportBatch
        {
            ProjectId = projectId,
            FileName = fileName,
            ImportedById = importedById,
            ImportDate = DateTime.Now,
            Status = "Staging"
        };

        await _context.EquipmentImportBatches.AddAsync(batch);
        await _context.SaveChangesAsync();

        // ==============================================================================
        // طبق توضیح کاربر: شماره‌تگ به‌تنهایی کلید یکتایی نیست - ممکن است یک تگ چند بار با
        // Description های متفاوت تکرار شود (مثلاً چند قلم زیرمجموعه‌ی یک تگ مشترک) که همه‌شان
        // باید جدا ثبت بشن. کلید یکتایی واقعی: ترکیب Tag+Description.
        // ==============================================================================
        var existingTagDescPairs = await _context.Equipments
            .Where(x => x.ProjectId == projectId)
            .Select(x => new { x.TagNumber, x.Description })
            .ToListAsync();

        var existingKeys = new HashSet<string>(
            existingTagDescPairs.Select(x => BuildEquipmentKey(x.TagNumber, x.Description)),
            StringComparer.OrdinalIgnoreCase);

        // در همین فایل هم فقط ترکیب کاملاً یکسان (Tag+Description) داپلیکیت واقعی حساب می‌شود
        var keyFirstSeenAtRow = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var rowNumber = 0;

        foreach (var sheet in workbook.Worksheets)
        {
            var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

            if (lastCol == 0 || lastRow < 2) continue;

            // طبق باگ واقعی که در فایل نمونه‌ی Mechanical دیدیم: سطر عنوان همیشه ردیف ۱ نیست -
            // اولین ۶ ردیف را بررسی می‌کنیم و ردیفی که بیشترین ستون "شبیه Tag" دارد را
            // به‌عنوان Header انتخاب می‌کنیم
            var headerRow = FindHeaderRow(sheet, lastCol, Math.Min(lastRow, 6));
            if (headerRow == 0) continue; // هیچ ردیف Header قابل‌تشخیصی پیدا نشد، این شیت رد شود

            var headerMap = BuildHeaderMap(sheet, headerRow, lastCol);

            for (int r = headerRow + 1; r <= lastRow; r++)
            {
                var tagNo = GetByHeader(sheet, r, headerMap, "tagno", "tag");
                if (string.IsNullOrWhiteSpace(tagNo)) continue; // ردیف خالی یا بدون Tag

                var payload = new EquipmentImportRowPayload
                {
                    TagNumber = tagNo,
                    Name = GetByHeader(sheet, r, headerMap, "itemname", "description", "packagesubequipment"),
                    EquipmentTypeCode = GetByHeader(sheet, r, headerMap, "eqtype", "type", "devicetype"),
                    DisciplineCodeRaw = GetByHeader(sheet, r, headerMap, "disc", "discipline"),
                    Area = GetByHeader(sheet, r, headerMap, "area", "unit"),
                    PIDNumber = GetByHeader(sheet, r, headerMap, "pidnumber", "pid"),
                    Description = GetByHeader(sheet, r, headerMap, "description"),
                    Manufacturer = GetByHeader(sheet, r, headerMap, "vendor", "manufacturer"),
                    MRNumber = GetByHeader(sheet, r, headerMap, "mrno", "mrqno"),
                    PONumber = GetByHeader(sheet, r, headerMap, "pono", "ponumber", "purchaseorderno")
                };

                rowNumber++;

                var normalizedTag = tagNo.Trim();
                var key = BuildEquipmentKey(normalizedTag, payload.Description ?? payload.Name);

                var isUpdate = existingKeys.Contains(key);

                var isDuplicateInFile = keyFirstSeenAtRow.TryGetValue(key, out var firstRow);
                if (!isDuplicateInFile) keyFirstSeenAtRow[key] = rowNumber;

                await _context.EquipmentImportRows.AddAsync(new EquipmentImportRow
                {
                    EquipmentImportBatchId = batch.EquipmentImportBatchId,
                    RowNumber = rowNumber,
                    PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
                    // طبق درخواست: داپلیکیت درون‌فایل خودکار Commit نمی‌شود - کاربر باید
                    // صریحاً بعد از بررسی، تیک Include را بزند. اگه فقط Tag تکراری باشه ولی
                    // Description فرق کنه، اصلاً داپلیکیت حساب نمی‌شه (رکورد جدا و معتبره)
                    IncludeInCommit = !isDuplicateInFile,
                    ValidationStatus = isDuplicateInFile ? "Warning" : "Valid",
                    ValidationMessage = isDuplicateInFile
                        ? $"ترکیب «{normalizedTag}» با همین توضیح، در همین فایل تکراری است (اولین بار در ردیف {firstRow}) - قبل از Commit بررسی کنید."
                        : null,
                    IsUpdate = isUpdate
                });
            }
        }

        await _context.SaveChangesAsync();

        return batch.EquipmentImportBatchId;
    }

    public async Task<EquipmentImportBatchDto?> GetBatchAsync(int batchId)
    {
        var batch = await _context.EquipmentImportBatches
            .Include(x => x.Project)
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.EquipmentImportBatchId == batchId);

        if (batch == null) return null;

        return MapBatch(batch);
    }

    public async Task<List<EquipmentImportBatchDto>> GetRecentBatchesAsync()
    {
        var batches = await _context.EquipmentImportBatches
            .Include(x => x.Project)
            .Include(x => x.Rows)
            .OrderByDescending(x => x.ImportDate)
            .Take(20)
            .ToListAsync();

        return batches.Select(MapBatch).ToList();
    }

    public async Task SetRowIncludeAsync(int rowId, bool include)
    {
        var row = await _context.EquipmentImportRows.FirstOrDefaultAsync(x => x.EquipmentImportRowId == rowId);
        if (row == null) return;

        row.IncludeInCommit = include;
        await _context.SaveChangesAsync();
    }

    private const int CommitFlushBatchSize = 200;

    public async Task<(int created, int updated, int skipped)> CommitBatchAsync(int batchId)
    {
        var batch = await _context.EquipmentImportBatches
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.EquipmentImportBatchId == batchId);

        if (batch == null) throw new Exception("Batch یافت نشد");

        int created = 0, updated = 0, skipped = 0;

        var allDisciplines = await _context.Disciplines.Where(x => x.IsActive).ToListAsync();

        // ==============================================================================
        // همون تکنیک MDR Import: به‌جای یک Query جداگانه برای هر ردیف (که برای فایل‌های
        // چندهزارردیفی یعنی چند ساعت طول می‌کشید)، همه‌ی تجهیزات موجود این پروژه یک‌جا
        // پیش‌بارگذاری می‌شوند و SaveChanges به‌جای هر ردیف، هر ۲۰۰ ردیف یک‌بار زده می‌شود.
        // ==============================================================================
        var existingEquipments = (await _context.Equipments
            .Where(x => x.ProjectId == batch.ProjectId)
            .ToListAsync())
            .GroupBy(x => BuildEquipmentKey(x.TagNumber, x.Description))
            .ToDictionary(g => g.Key, g => g.First());

        var pendingRowEquipmentPairs = new List<(EquipmentImportRow Row, Equipment Equipment)>();
        var rowsSinceFlush = 0;

        foreach (var row in batch.Rows.Where(x => x.IncludeInCommit))
        {
            var payload = JsonSerializer.Deserialize<EquipmentImportRowPayload>(row.PayloadJson, JsonOptions);
            if (payload == null || string.IsNullOrWhiteSpace(payload.TagNumber))
            {
                skipped++;
                continue;
            }

            var tag = payload.TagNumber.Trim();
            var equipmentKey = BuildEquipmentKey(tag, payload.Description ?? payload.Name);

            var isNew = !existingEquipments.TryGetValue(equipmentKey, out var equipment);

            int? disciplineId = null;
            if (!string.IsNullOrWhiteSpace(payload.DisciplineCodeRaw))
            {
                var discipline = DisciplineMatcher.Match(allDisciplines, payload.DisciplineCodeRaw);
                disciplineId = discipline?.DisciplineId;
            }

            if (isNew)
            {
                equipment = new Equipment
                {
                    TagNumber = tag,
                    ProjectId = batch.ProjectId,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                _context.Equipments.Add(equipment);
                existingEquipments[equipmentKey] = equipment;
                created++;
            }
            else
            {
                updated++;
            }

            equipment!.Name = payload.Name ?? equipment.Name;
            equipment.EquipmentType = payload.EquipmentTypeCode ?? equipment.EquipmentType;
            equipment.Area = payload.Area;
            equipment.PIDNumber = payload.PIDNumber;
            equipment.Description = payload.Description;
            equipment.Manufacturer = payload.Manufacturer;

            // اگر دیسیپلین از ستون فایل تشخیص داده نشد و این تجهیز جدید است، به اولین
            // دیسیپلین فعال نسبت داده می‌شود (چون DisciplineId اجباری است) - کاربر بعداً اصلاح می‌کند
            if (disciplineId.HasValue)
                equipment.DisciplineId = disciplineId.Value;
            else if (isNew)
                equipment.DisciplineId = allDisciplines.FirstOrDefault()?.DisciplineId ?? 0;

            // طبق رفع باگ قبلی: فقط متن خام ذخیره می‌شود، هیچ MR/Vendor/VDR خودکار ساخته نمی‌شود
            equipment.MRNumberRaw = payload.MRNumber;
            equipment.PONumberRaw = payload.PONumber;

            pendingRowEquipmentPairs.Add((row, equipment));
            rowsSinceFlush++;

            if (rowsSinceFlush >= CommitFlushBatchSize)
            {
                await _context.SaveChangesAsync();

                foreach (var (pendingRow, pendingEquipment) in pendingRowEquipmentPairs)
                    pendingRow.CreatedEquipmentId = pendingEquipment.EquipmentId;

                pendingRowEquipmentPairs.Clear();
                rowsSinceFlush = 0;
            }
        }

        await _context.SaveChangesAsync();

        foreach (var (pendingRow, pendingEquipment) in pendingRowEquipmentPairs)
            pendingRow.CreatedEquipmentId = pendingEquipment.EquipmentId;

        batch.Status = "Committed";
        await _context.SaveChangesAsync();

        return (created, updated, skipped);
    }

    public async Task DiscardBatchAsync(int batchId)
    {
        var batch = await _context.EquipmentImportBatches
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.EquipmentImportBatchId == batchId);

        if (batch == null) return;

        _context.EquipmentImportRows.RemoveRange(batch.Rows);
        _context.EquipmentImportBatches.Remove(batch);

        await _context.SaveChangesAsync();
    }

    private static EquipmentImportBatchDto MapBatch(EquipmentImportBatch batch)
    {
        return new EquipmentImportBatchDto
        {
            EquipmentImportBatchId = batch.EquipmentImportBatchId,
            FileName = batch.FileName,
            ProjectId = batch.ProjectId,
            ProjectName = batch.Project?.ProjectName,
            ImportDate = batch.ImportDate,
            Status = batch.Status,
            Rows = batch.Rows.OrderBy(x => x.RowNumber).Select(r =>
            {
                var payload = JsonSerializer.Deserialize<EquipmentImportRowPayload>(r.PayloadJson, JsonOptions);
                return new EquipmentImportRowDto
                {
                    EquipmentImportRowId = r.EquipmentImportRowId,
                    RowNumber = r.RowNumber,
                    TagNumber = payload?.TagNumber ?? "",
                    Name = payload?.Name,
                    DisciplineCodeRaw = payload?.DisciplineCodeRaw,
                    ValidationStatus = r.ValidationStatus,
                    ValidationMessage = r.ValidationMessage,
                    IncludeInCommit = r.IncludeInCommit,
                    IsUpdate = r.IsUpdate,
                    Payload = payload
                };
            }).ToList()
        };
    }

    // طبق باگ واقعی گزارش‌شده روی فایل نمونه: سطر عنوان همیشه ردیف ۱ نیست
    // طبق توضیح کاربر: کلید یکتایی واقعی تجهیز، ترکیب Tag+Description است - نه فقط Tag
    private static string BuildEquipmentKey(string tagNumber, string? description)
        => $"{tagNumber.Trim().ToLower()}|{(description ?? "").Trim().ToLower()}";

    private static int FindHeaderRow(IXLWorksheet sheet, int lastCol, int maxRowsToScan)
    {
        var bestRow = 0;
        var bestScore = 0;

        for (int r = 1; r <= maxRowsToScan; r++)
        {
            var score = 0;

            for (int c = 1; c <= lastCol; c++)
            {
                var h = NormalizeHeader(sheet.Cell(r, c).GetString());
                if (h.Contains("tag") || h.Contains("itemname") || h.Contains("description") ||
                    h.Contains("eqtype") || h.Contains("devicetype"))
                {
                    score++;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestRow = r;
            }
        }

        return bestScore > 0 ? bestRow : 0;
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet sheet, int headerRow, int lastCol)
    {
        var map = new Dictionary<string, int>();

        for (int c = 1; c <= lastCol; c++)
        {
            var h = NormalizeHeader(sheet.Cell(headerRow, c).GetString());

            if (!string.IsNullOrEmpty(h) && !map.ContainsKey(h))
                map[h] = c;
        }

        return map;
    }

    private static string? GetByHeader(IXLWorksheet sheet, int row, Dictionary<string, int> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (map.TryGetValue(key, out var col))
            {
                var v = sheet.Cell(row, col).GetString();

                if (!string.IsNullOrWhiteSpace(v))
                    return v.Trim();
            }
        }

        return null;
    }

    private static string NormalizeHeader(string raw)
    {
        return raw
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "")
            .Replace(".", "")
            .Replace("&", "")
            .Replace("\n", "")
            .Replace("\r", "")
            .Replace("_", "")
            .Replace("-", "");
    }
}
