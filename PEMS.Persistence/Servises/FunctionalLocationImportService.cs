using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «Functional Location Master Data Import»: همان الگوی EquipmentImportService
/// (تشخیص خودکار Header + Staging + Preview + Commit دومرحله‌ای) - با یک تفاوت کلیدی:
/// یک سطر منبع با چند AREA CODE به چند FunctionalLocation مجزا (یک والد سطح-Area + N فرزند)
/// تجزیه می‌شود، نه یک رکورد با کد چندمقداری.
/// </summary>
public class FunctionalLocationImportService : IFunctionalLocationImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new();
    private static readonly char[] CodeSeparators = { ',', ';', '/' };

    private readonly PEMSDbContext _context;

    public FunctionalLocationImportService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int? importedById)
    {
        using var workbook = new XLWorkbook(fileStream);

        var batch = new FunctionalLocationImportBatch
        {
            FileName = fileName,
            ImportedById = importedById,
            ImportDate = DateTime.Now,
            Status = "Staging"
        };

        await _context.FunctionalLocationImportBatches.AddAsync(batch);
        await _context.SaveChangesAsync();

        // طبق بند ۸ (Duplicate Strategy): وضعیت فعلی دیتابیس یک‌بار پیش‌بارگذاری می‌شود -
        // نه یک Query جدا برای هر کد.
        var existingByCode = await _context.FunctionalLocations
            .Where(x => x.IsActive)
            .Select(x => new ExistingFlSnapshot { Code = x.Code, Name = x.Name, Area = x.Area })
            .ToDictionaryAsync(x => x.Code.Trim(), StringComparer.OrdinalIgnoreCase);

        // طبق بند ۸: تشخیص تناقض درون همین فایل - همان کد توسط دو Area Name متفاوت ادعا شده
        var codeFirstSeenInFile = new Dictionary<string, (int SourceRow, string AreaName)>(StringComparer.OrdinalIgnoreCase);

        var stagingRowNumber = 0;

        var sheet = workbook.Worksheets.First();
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

        if (lastCol == 0 || lastRow < 2)
            return batch.FunctionalLocationImportBatchId;

        var headerRow = FindHeaderRow(sheet, lastCol, Math.Min(lastRow, 6));
        if (headerRow == 0)
            return batch.FunctionalLocationImportBatchId;

        var headerMap = BuildHeaderMap(sheet, headerRow, lastCol);

        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var areaNameRaw = GetByHeader(sheet, r, headerMap, "areaname", "area");
            var areaCodeRaw = GetByHeader(sheet, r, headerMap, "areacode", "code");

            // ردیف کاملاً خالی یا زیرعنوان (مثل "No.") - بی‌صدا رد می‌شود، نه Invalid
            if (string.IsNullOrWhiteSpace(areaNameRaw) && string.IsNullOrWhiteSpace(areaCodeRaw))
                continue;

            var areaName = areaNameRaw?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(areaNameRaw) || string.IsNullOrWhiteSpace(areaCodeRaw))
            {
                stagingRowNumber++;
                await AddRowAsync(batch, stagingRowNumber, r,
                    new FunctionalLocationImportRowPayload
                    {
                        Code = "", Name = areaName, SourceAreaNameRaw = areaNameRaw ?? "", SourceAreaCodeRaw = areaCodeRaw ?? ""
                    },
                    "Error", string.IsNullOrWhiteSpace(areaNameRaw) ? "Area Name خالی است." : "Area Code خالی است.",
                    includeInCommit: false, isUpdate: false, displayStatus: "Invalid", displayAction: "Reject");
                continue;
            }

            // طبق بند ۴: نرمال‌سازی - جداسازی چند کد، Trim، حذف مقادیر خالی/تکراری - بدون تغییر
            // خودِ کد (بند ۲۵: 85A07 باید 85A07 بماند)
            var codes = areaCodeRaw.Split(CodeSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (codes.Count == 0)
            {
                stagingRowNumber++;
                await AddRowAsync(batch, stagingRowNumber, r,
                    new FunctionalLocationImportRowPayload { Code = "", Name = areaName, SourceAreaNameRaw = areaNameRaw, SourceAreaCodeRaw = areaCodeRaw },
                    "Error", "پس از نرمال‌سازی، هیچ کد معتبری در این سطر باقی نماند.", includeInCommit: false, isUpdate: false,
                    displayStatus: "Invalid", displayAction: "Reject");
                continue;
            }

            var isMultiCode = codes.Count > 1;

            if (isMultiCode)
            {
                // ردیف والد سطح-Area - طبق بند ۴/۵: هیچ کد جداگانه‌ای برای خودِ Area در منبع
                // وجود ندارد، پس نام Area به‌عنوان کد والد استفاده می‌شود (مستند و قابل ردیابی)
                stagingRowNumber++;
                var parentPayload = new FunctionalLocationImportRowPayload
                {
                    Code = areaName, Name = areaName, Area = areaName, IsAreaParentRow = true,
                    SourceAreaNameRaw = areaNameRaw, SourceAreaCodeRaw = areaCodeRaw
                };
                await StageOneCodeAsync(batch, stagingRowNumber, r, parentPayload, existingByCode, codeFirstSeenInFile);
            }

            foreach (var code in codes)
            {
                stagingRowNumber++;
                var childPayload = new FunctionalLocationImportRowPayload
                {
                    Code = code, Name = areaName, Area = areaName,
                    ParentAreaName = isMultiCode ? areaName : null,
                    SourceAreaNameRaw = areaNameRaw, SourceAreaCodeRaw = areaCodeRaw
                };
                await StageOneCodeAsync(batch, stagingRowNumber, r, childPayload, existingByCode, codeFirstSeenInFile);
            }
        }

        await _context.SaveChangesAsync();

        return batch.FunctionalLocationImportBatchId;
    }

    private sealed class ExistingFlSnapshot
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Area { get; set; }
    }

    private async Task StageOneCodeAsync(FunctionalLocationImportBatch batch, int stagingRowNumber, int sourceRow,
        FunctionalLocationImportRowPayload payload,
        Dictionary<string, ExistingFlSnapshot> existingByCode,
        Dictionary<string, (int SourceRow, string AreaName)> codeFirstSeenInFile)
    {
        string validationStatus;
        string? message = null;
        bool includeInCommit;
        bool isUpdate = false;
        string displayStatus;
        string displayAction;

        if (existingByCode.TryGetValue(payload.Code, out var existing))
        {
            isUpdate = true;
            var sameData = string.Equals(existing.Name.Trim(), payload.Name.Trim(), StringComparison.OrdinalIgnoreCase);
            if (sameData)
            {
                validationStatus = "Valid";
                includeInCommit = false;
                message = $"از قبل با همین داده موجود است (Code={payload.Code}) - نیازی به تغییر نیست.";
                displayStatus = "Existing";
                displayAction = "Skip";
            }
            else
            {
                validationStatus = "Warning";
                includeInCommit = false;
                message = $"این کد از قبل با Area Name متفاوت موجود است (موجود: «{existing.Name}»، منبع: «{payload.Name}») - قبل از Commit بررسی کنید.";
                displayStatus = "Conflict";
                displayAction = "Conflict";
            }
        }
        else if (codeFirstSeenInFile.TryGetValue(payload.Code, out var firstSeen))
        {
            if (string.Equals(firstSeen.AreaName, payload.Name, StringComparison.OrdinalIgnoreCase))
            {
                validationStatus = "Warning";
                includeInCommit = false;
                message = $"این کد در همین فایل تکراری است (اولین بار در ردیف منبع {firstSeen.SourceRow}).";
                displayStatus = "Conflict";
                displayAction = "Conflict";
            }
            else
            {
                validationStatus = "Error";
                includeInCommit = false;
                message = $"تناقض داده‌ی منبع: این کد قبلاً در همین فایل توسط Area Name متفاوتی ادعا شده («{firstSeen.AreaName}» در ردیف {firstSeen.SourceRow}) - نیاز به بررسی/اصلاح فایل منبع دارد.";
                displayStatus = "Conflict";
                displayAction = "Conflict";
            }
        }
        else
        {
            validationStatus = "Valid";
            includeInCommit = true;
            codeFirstSeenInFile[payload.Code] = (sourceRow, payload.Name);
            displayStatus = "New";
            displayAction = "Create";
        }

        await AddRowAsync(batch, stagingRowNumber, sourceRow, payload, validationStatus, message, includeInCommit, isUpdate, displayStatus, displayAction);
    }

    private async Task AddRowAsync(FunctionalLocationImportBatch batch, int rowNumber, int sourceRowNumber,
        FunctionalLocationImportRowPayload payload, string validationStatus, string? message, bool includeInCommit, bool isUpdate,
        string displayStatus, string displayAction)
    {
        await _context.FunctionalLocationImportRows.AddAsync(new FunctionalLocationImportRow
        {
            FunctionalLocationImportBatchId = batch.FunctionalLocationImportBatchId,
            RowNumber = rowNumber,
            SourceRowNumber = sourceRowNumber,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            ValidationStatus = validationStatus,
            ValidationMessage = message,
            IncludeInCommit = includeInCommit,
            IsUpdate = isUpdate,
            Status = displayStatus,
            Action = displayAction
        });
    }

    public async Task<FunctionalLocationImportBatchDto?> GetBatchAsync(int batchId)
    {
        var batch = await _context.FunctionalLocationImportBatches
            .Include(x => x.ImportedBy)
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.FunctionalLocationImportBatchId == batchId);

        return batch == null ? null : MapBatch(batch);
    }

    public async Task<List<FunctionalLocationImportBatchDto>> GetRecentBatchesAsync()
    {
        var batches = await _context.FunctionalLocationImportBatches
            .Include(x => x.ImportedBy)
            .Include(x => x.Rows)
            .OrderByDescending(x => x.ImportDate)
            .Take(20)
            .ToListAsync();

        return batches.Select(MapBatch).ToList();
    }

    public async Task SetRowIncludeAsync(int rowId, bool include)
    {
        var row = await _context.FunctionalLocationImportRows.FirstOrDefaultAsync(x => x.FunctionalLocationImportRowId == rowId);
        if (row == null) return;

        row.IncludeInCommit = include;
        await _context.SaveChangesAsync();
    }

    public async Task<FunctionalLocationImportCommitResultDto> CommitBatchAsync(int batchId)
    {
        var batch = await _context.FunctionalLocationImportBatches
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.FunctionalLocationImportBatchId == batchId);

        if (batch == null) throw new InvalidOperationException("Batch یافت نشد.");

        var result = new FunctionalLocationImportCommitResultDto();

        var existingByCode = await _context.FunctionalLocations
            .Where(x => x.IsActive)
            .ToDictionaryAsync(x => x.Code.Trim(), StringComparer.OrdinalIgnoreCase);

        // نام Area -> Id واقعی FunctionalLocation والد، برای Resolve کردن ParentId ردیف‌های فرزند
        var areaParentIdByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var allRows = batch.Rows.Select(r => (Row: r, Payload: JsonSerializer.Deserialize<FunctionalLocationImportRowPayload>(r.PayloadJson, JsonOptions)))
            .Where(x => x.Payload != null)
            .ToList();

        // پاس اول: ردیف‌های والد سطح-Area + ردیف‌های تک‌کد مستقل (بدون والد)
        foreach (var (row, payload) in allRows.Where(x => x.Payload!.IsAreaParentRow || x.Payload!.ParentAreaName == null))
        {
            if (!row.IncludeInCommit)
            {
                CountExcludedRow(row, result);
                continue;
            }

            var fl = await CommitOneRowAsync(row, payload!, existingByCode, parentId: null);
            if (payload!.IsAreaParentRow)
                areaParentIdByName[payload.Name] = fl.FunctionalLocationId;

            if (row.IsUpdate) result.Updated++; else result.Created++;
        }

        await _context.SaveChangesAsync();

        // پاس دوم: ردیف‌های فرزند - حالا ParentId واقعی از پاس اول (یا از دیتابیس موجود) در دسترس است
        foreach (var (row, payload) in allRows.Where(x => x.Payload!.ParentAreaName != null))
        {
            if (!row.IncludeInCommit)
            {
                CountExcludedRow(row, result);
                continue;
            }

            int? parentId = null;
            if (areaParentIdByName.TryGetValue(payload!.ParentAreaName!, out var pid))
            {
                parentId = pid;
            }
            else if (existingByCode.TryGetValue(payload.ParentAreaName!, out var existingParent))
            {
                // والد این فرزند در همین Batch شامل نشد (کاربر آن را از Commit حذف کرد) -
                // اگر از قبل در دیتابیس موجود باشد، همان استفاده می‌شود
                parentId = existingParent.FunctionalLocationId;
            }

            var fl = await CommitOneRowAsync(row, payload, existingByCode, parentId);
            if (row.IsUpdate) result.Updated++; else result.Created++;
        }

        await _context.SaveChangesAsync();

        batch.Status = "Committed";
        await _context.SaveChangesAsync();

        return result;
    }

    private static void CountExcludedRow(FunctionalLocationImportRow row, FunctionalLocationImportCommitResultDto result)
    {
        switch (row.Status)
        {
            case "Conflict": result.Conflicts++; break;
            case "Existing": result.Skipped++; break;
            case "Invalid": result.Rejected++; break;
            default: result.Skipped++; break; // "New" ردیف‌هایی که کاربر خودش IncludeInCommit را خاموش کرده
        }
    }

    private async Task<FunctionalLocation> CommitOneRowAsync(FunctionalLocationImportRow row, FunctionalLocationImportRowPayload payload,
        Dictionary<string, FunctionalLocation> existingByCode, int? parentId)
    {
        if (existingByCode.TryGetValue(payload.Code, out var existing))
        {
            existing.Name = payload.Name;
            existing.Area = payload.Area;
            if (parentId.HasValue) existing.ParentId = parentId.Value;
            row.CreatedFunctionalLocationId = existing.FunctionalLocationId;
            return existing;
        }

        var newFl = new FunctionalLocation
        {
            Code = payload.Code,
            Name = payload.Name,
            Area = payload.Area,
            ParentId = parentId,
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        await _context.FunctionalLocations.AddAsync(newFl);
        await _context.SaveChangesAsync(); // Id واقعی فوراً لازم است (برای Resolve کردن فرزندان همین پاس)

        existingByCode[payload.Code] = newFl;
        row.CreatedFunctionalLocationId = newFl.FunctionalLocationId;

        return newFl;
    }

    public async Task DiscardBatchAsync(int batchId)
    {
        var batch = await _context.FunctionalLocationImportBatches
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.FunctionalLocationImportBatchId == batchId);

        if (batch == null) return;

        _context.FunctionalLocationImportRows.RemoveRange(batch.Rows);
        _context.FunctionalLocationImportBatches.Remove(batch);

        await _context.SaveChangesAsync();
    }

    private static FunctionalLocationImportBatchDto MapBatch(FunctionalLocationImportBatch batch)
    {
        var rows = batch.Rows.OrderBy(x => x.RowNumber).Select(r =>
        {
            var payload = JsonSerializer.Deserialize<FunctionalLocationImportRowPayload>(r.PayloadJson, JsonOptions);

            return new FunctionalLocationImportRowDto
            {
                FunctionalLocationImportRowId = r.FunctionalLocationImportRowId,
                RowNumber = r.RowNumber,
                SourceRowNumber = r.SourceRowNumber,
                Code = payload?.Code ?? "",
                Name = payload?.Name ?? "",
                ParentAreaName = payload?.ParentAreaName,
                ValidationStatus = r.ValidationStatus,
                ValidationMessage = r.ValidationMessage,
                IncludeInCommit = r.IncludeInCommit,
                IsUpdate = r.IsUpdate,
                Status = r.Status,
                Action = r.Action,
                Payload = payload
            };
        }).ToList();

        return new FunctionalLocationImportBatchDto
        {
            FunctionalLocationImportBatchId = batch.FunctionalLocationImportBatchId,
            FileName = batch.FileName,
            ImportDate = batch.ImportDate,
            Status = batch.Status,
            ImportedByName = batch.ImportedBy != null ? $"{batch.ImportedBy.FirstName} {batch.ImportedBy.LastName}" : null,
            Rows = rows,
            SourceRowCount = rows.Select(x => x.SourceRowNumber).Distinct().Count(),
            ExpandedLocationCount = rows.Count,
            NewCount = rows.Count(x => x.Status == "New"),
            ExistingCount = rows.Count(x => x.Status == "Existing"),
            ConflictCount = rows.Count(x => x.Status == "Conflict"),
            InvalidCount = rows.Count(x => x.Status == "Invalid")
        };
    }

    private static int FindHeaderRow(IXLWorksheet sheet, int lastCol, int maxRowsToScan)
    {
        var bestRow = 0;
        var bestScore = 0;

        for (var r = 1; r <= maxRowsToScan; r++)
        {
            var score = 0;
            for (var c = 1; c <= lastCol; c++)
            {
                var h = NormalizeHeader(sheet.Cell(r, c).GetString());
                if (h.Contains("areaname") || h.Contains("areacode"))
                    score++;
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
        for (var c = 1; c <= lastCol; c++)
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
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            }
        }
        return null;
    }

    private static string NormalizeHeader(string raw) =>
        raw.Trim().ToLowerInvariant().Replace(" ", "").Replace(".", "").Replace("_", "").Replace("-", "");
}
