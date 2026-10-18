using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۹:
/// دقیقاً همون الگوی MaintenanceHistoryImportService (تشخیص خودکار Header + Staging + Preview +
/// Commit صریح، بدون Fuzzy/AI). برخلاف MaintenanceHistory (که Equipment را مستقیم تطبیق می‌دهد)،
/// این‌جا کلید تطبیق ترکیبی (EquipmentTag, PointTag) است - چون ConditionMonitoringPoint.Tag فقط
/// در سطح یک Equipment یکتاست، نه کل سیستم.
/// </summary>
public class ConditionReadingImportService : IConditionReadingImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly PEMSDbContext _context;

    public ConditionReadingImportService(PEMSDbContext context)
    {
        _context = context;
    }

    // ==============================================================================
    // Stage
    // ==============================================================================

    public async Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int? importedById)
    {
        using var workbook = new XLWorkbook(fileStream);

        var batch = new ConditionReadingImportBatch
        {
            FileName = fileName,
            ImportedById = importedById,
            ImportDate = DateTime.Now,
            Status = "Staging"
        };

        await _context.ConditionReadingImportBatches.AddAsync(batch);
        await _context.SaveChangesAsync();

        var sheet = workbook.Worksheets.First();
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

        if (lastCol == 0 || lastRow < 2)
            return batch.ConditionReadingImportBatchId;

        var headerRow = FindHeaderRow(sheet, lastCol, Math.Min(lastRow, 6));
        if (headerRow == 0)
            return batch.ConditionReadingImportBatchId;

        var headerMap = BuildHeaderMap(sheet, headerRow, lastCol);

        // طبق بند «Performance and Scale»: نقاط پایش فقط یک‌بار برای کل فایل پیش‌بارگذاری می‌شوند.
        var pointsByKey = await BuildPointLookupAsync();

        var rawRows = ReadRawRows(sheet, headerRow, lastRow, headerMap);

        var candidatePointIds = rawRows
            .Select(r => pointsByKey.TryGetValue(PointKey(r.EquipmentTagRaw, r.PointTagRaw), out var matches) && matches.Count == 1 ? matches[0].PointId : (int?)null)
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var existingKeys = await BuildExistingReadingKeysAsync(candidatePointIds);
        var seenInBatch = new HashSet<(int PointId, DateTime ReadingDate)>();

        var stagingRowNumber = 0;

        foreach (var raw in rawRows)
        {
            if (string.IsNullOrWhiteSpace(raw.EquipmentTagRaw) && string.IsNullOrWhiteSpace(raw.PointTagRaw) && string.IsNullOrWhiteSpace(raw.DateRaw))
                continue; // ردیف کاملاً خالی - بی‌صدا رد می‌شود

            stagingRowNumber++;

            var payload = new ConditionReadingImportRowPayload
            {
                EquipmentTagRaw = raw.EquipmentTagRaw ?? "",
                PointTagRaw = raw.PointTagRaw ?? "",
                ReadingDateRaw = raw.DateRaw ?? "",
                ValueRaw = raw.ValueRaw ?? "",
                SourceRaw = raw.SourceRaw,
                NotesRaw = raw.NotesRaw
            };

            var resolved = Resolve(payload, raw.ParsedDate, pointsByKey, existingKeys, seenInBatch);

            await _context.ConditionReadingImportRows.AddAsync(new ConditionReadingImportRow
            {
                ConditionReadingImportBatchId = batch.ConditionReadingImportBatchId,
                RowNumber = stagingRowNumber,
                SourceRowNumber = raw.SourceRowNumber,
                PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
                ValidationStatus = resolved.ValidationStatus,
                ValidationMessage = resolved.Message,
                IncludeInCommit = resolved.IncludeInCommit,
                Status = resolved.Status,
                ResolvedConditionMonitoringPointId = resolved.PointId,
                DuplicateOfConditionReadingId = resolved.DuplicateOfId
            });
        }

        await _context.SaveChangesAsync();

        return batch.ConditionReadingImportBatchId;
    }

    // ==============================================================================
    // Raw row extraction
    // ==============================================================================

    private sealed class RawRow
    {
        public int SourceRowNumber { get; set; }
        public string? EquipmentTagRaw { get; set; }
        public string? PointTagRaw { get; set; }
        public string? DateRaw { get; set; }
        public DateTime? ParsedDate { get; set; }
        public string? ValueRaw { get; set; }
        public string? SourceRaw { get; set; }
        public string? NotesRaw { get; set; }
    }

    private static List<RawRow> ReadRawRows(IXLWorksheet sheet, int headerRow, int lastRow, Dictionary<string, int> headerMap)
    {
        var result = new List<RawRow>();

        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var equipmentTagRaw = GetByHeader(sheet, r, headerMap, "equipmenttag", "tag", "tagnumber", "equipment");
            var pointTagRaw = GetByHeader(sheet, r, headerMap, "pointtag", "point", "monitoringpoint", "cmpoint");
            var dateRaw = GetDateStringByHeader(sheet, r, headerMap, out var parsedDate, "readingdate", "date", "measurementdate");

            if (string.IsNullOrWhiteSpace(equipmentTagRaw) && string.IsNullOrWhiteSpace(pointTagRaw) && string.IsNullOrWhiteSpace(dateRaw))
                continue;

            result.Add(new RawRow
            {
                SourceRowNumber = r,
                EquipmentTagRaw = equipmentTagRaw,
                PointTagRaw = pointTagRaw,
                DateRaw = dateRaw,
                ParsedDate = parsedDate,
                ValueRaw = GetByHeader(sheet, r, headerMap, "value", "reading", "measurementvalue"),
                SourceRaw = GetByHeader(sheet, r, headerMap, "source"),
                NotesRaw = GetByHeader(sheet, r, headerMap, "notes", "remarks", "description")
            });
        }

        return result;
    }

    // ==============================================================================
    // Resolution (Deterministic only - بدون Fuzzy/AI)
    // ==============================================================================

    private sealed class PointLookupEntry
    {
        public int PointId { get; set; }
        public string? VariableType { get; set; }
    }

    private sealed class ResolveResult
    {
        public string Status { get; set; } = "";
        public string ValidationStatus { get; set; } = "Valid";
        public string? Message { get; set; }
        public bool IncludeInCommit { get; set; }
        public int? PointId { get; set; }
        public int? DuplicateOfId { get; set; }
    }

    private static string PointKey(string? equipmentTag, string? pointTag) =>
        $"{(equipmentTag ?? "").Trim()}||{(pointTag ?? "").Trim()}".ToLowerInvariant();

    private static ResolveResult Resolve(
        ConditionReadingImportRowPayload payload,
        DateTime? parsedDate,
        Dictionary<string, List<PointLookupEntry>> pointsByKey,
        Dictionary<(int PointId, DateTime ReadingDate), int> existingKeys,
        HashSet<(int PointId, DateTime ReadingDate)> seenInBatch)
    {
        var equipmentTag = payload.EquipmentTagRaw.Trim();
        var pointTag = payload.PointTagRaw.Trim();

        if (equipmentTag.Length == 0)
            return new ResolveResult { Status = "ValidationError", ValidationStatus = "Error", Message = "Equipment Tag خالی است.", IncludeInCommit = false };

        if (pointTag.Length == 0)
            return new ResolveResult { Status = "ValidationError", ValidationStatus = "Error", Message = "Point Tag خالی است.", IncludeInCommit = false };

        if (!parsedDate.HasValue)
            return new ResolveResult { Status = "ValidationError", ValidationStatus = "Error", Message = "تاریخ قرائت (Reading Date) خالی یا نامعتبر است.", IncludeInCommit = false };

        if (string.IsNullOrWhiteSpace(payload.ValueRaw) || !decimal.TryParse(payload.ValueRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            return new ResolveResult { Status = "ValidationError", ValidationStatus = "Error", Message = "مقدار (Value) خالی یا عددی نامعتبر است.", IncludeInCommit = false };

        var key = PointKey(equipmentTag, pointTag);
        if (!pointsByKey.TryGetValue(key, out var matches) || matches.Count == 0)
            return new ResolveResult { Status = "UnresolvedPoint", ValidationStatus = "Error", Message = $"نقطه‌ی پایشی با Equipment Tag «{equipmentTag}» و Point Tag «{pointTag}» پیدا نشد.", IncludeInCommit = false };

        if (matches.Count > 1)
            return new ResolveResult { Status = "ConflictPoint", ValidationStatus = "Error", Message = $"چند نقطه‌ی پایش با همین ترکیب Tag پیدا شد ({matches.Count} مورد) - تطبیق مطمئن ممکن نیست.", IncludeInCommit = false };

        var pointId = matches[0].PointId;
        var readingKey = (pointId, parsedDate.Value);

        if (existingKeys.TryGetValue(readingKey, out var existingId))
        {
            return new ResolveResult
            {
                Status = "Duplicate",
                ValidationStatus = "Warning",
                Message = "یک قرائت با همین نقطه‌ی پایش و تاریخ دقیق از قبل وجود دارد - برای جلوگیری از تکرار، پیش‌فرض ثبت نمی‌شود.",
                IncludeInCommit = false,
                PointId = pointId,
                DuplicateOfId = existingId
            };
        }

        if (seenInBatch.Contains(readingKey))
        {
            return new ResolveResult
            {
                Status = "Duplicate",
                ValidationStatus = "Warning",
                Message = "یک ردیف دیگر در همین فایل با همین نقطه‌ی پایش/تاریخ دقیق از قبل پردازش شده است.",
                IncludeInCommit = false,
                PointId = pointId
            };
        }

        seenInBatch.Add(readingKey);

        return new ResolveResult { Status = "New", ValidationStatus = "Valid", IncludeInCommit = true, PointId = pointId };
    }

    private async Task<Dictionary<string, List<PointLookupEntry>>> BuildPointLookupAsync()
    {
        var rows = await _context.ConditionMonitoringPoints
            .Where(x => x.IsActive)
            .Include(x => x.Equipment)
            .Select(x => new { x.ConditionMonitoringPointId, EquipmentTag = x.Equipment!.TagNumber, x.Tag, x.VariableType })
            .ToListAsync();

        var map = new Dictionary<string, List<PointLookupEntry>>();
        foreach (var row in rows)
        {
            var key = PointKey(row.EquipmentTag, row.Tag);
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<PointLookupEntry>();
                map[key] = list;
            }
            list.Add(new PointLookupEntry { PointId = row.ConditionMonitoringPointId, VariableType = row.VariableType });
        }
        return map;
    }

    // طبق بند «Performance and Scale»: فقط ConditionReading متعلق به نقاطی که واقعاً در این فایل
    // ظاهر شده‌اند بارگذاری می‌شود.
    private async Task<Dictionary<(int PointId, DateTime ReadingDate), int>> BuildExistingReadingKeysAsync(List<int> pointIds)
    {
        var result = new Dictionary<(int, DateTime), int>();
        if (pointIds.Count == 0) return result;

        var rows = await _context.ConditionReadings
            .Where(x => pointIds.Contains(x.ConditionMonitoringPointId))
            .Select(x => new { x.ConditionReadingId, x.ConditionMonitoringPointId, x.ReadingDate })
            .ToListAsync();

        foreach (var row in rows)
            result.TryAdd((row.ConditionMonitoringPointId, row.ReadingDate), row.ConditionReadingId);

        return result;
    }

    // ==============================================================================
    // Review / Query
    // ==============================================================================

    public async Task<ConditionReadingImportBatchSummaryDto?> GetBatchSummaryAsync(int batchId)
    {
        var batch = await _context.ConditionReadingImportBatches
            .Include(x => x.ImportedBy)
            .FirstOrDefaultAsync(x => x.ConditionReadingImportBatchId == batchId);

        if (batch == null) return null;

        var rows = await _context.ConditionReadingImportRows
            .Where(x => x.ConditionReadingImportBatchId == batchId)
            .Select(x => x.Status)
            .ToListAsync();

        return BuildSummary(batch, rows);
    }

    public async Task<List<ConditionReadingImportBatchSummaryDto>> GetRecentBatchesAsync()
    {
        var batches = await _context.ConditionReadingImportBatches
            .Include(x => x.ImportedBy)
            .OrderByDescending(x => x.ImportDate)
            .Take(20)
            .ToListAsync();

        var result = new List<ConditionReadingImportBatchSummaryDto>();
        foreach (var batch in batches)
        {
            var rows = await _context.ConditionReadingImportRows
                .Where(x => x.ConditionReadingImportBatchId == batch.ConditionReadingImportBatchId)
                .Select(x => x.Status)
                .ToListAsync();

            result.Add(BuildSummary(batch, rows));
        }

        return result;
    }

    private static ConditionReadingImportBatchSummaryDto BuildSummary(ConditionReadingImportBatch batch, List<string> statuses) => new()
    {
        ConditionReadingImportBatchId = batch.ConditionReadingImportBatchId,
        FileName = batch.FileName,
        ImportDate = batch.ImportDate,
        Status = batch.Status,
        ImportedByName = batch.ImportedBy != null ? $"{batch.ImportedBy.FirstName} {batch.ImportedBy.LastName}" : null,
        TotalRows = statuses.Count,
        NewCount = statuses.Count(x => x == "New"),
        DuplicateCount = statuses.Count(x => x == "Duplicate"),
        UnresolvedCount = statuses.Count(x => x == "UnresolvedPoint"),
        ConflictCount = statuses.Count(x => x == "ConflictPoint"),
        ValidationErrorCount = statuses.Count(x => x == "ValidationError")
    };

    public async Task<ConditionReadingImportBatchDto?> GetBatchAsync(int batchId, int page, int pageSize, string? statusFilter)
    {
        var batch = await _context.ConditionReadingImportBatches
            .Include(x => x.ImportedBy)
            .FirstOrDefaultAsync(x => x.ConditionReadingImportBatchId == batchId);

        if (batch == null) return null;

        var allStatuses = await _context.ConditionReadingImportRows
            .Where(x => x.ConditionReadingImportBatchId == batchId)
            .Select(x => x.Status)
            .ToListAsync();

        var query = _context.ConditionReadingImportRows
            .Where(x => x.ConditionReadingImportBatchId == batchId);

        if (!string.IsNullOrWhiteSpace(statusFilter))
            query = query.Where(x => x.Status == statusFilter);

        var totalCount = await query.CountAsync();

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        var pageRows = await query
            .OrderBy(x => x.RowNumber)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

        var pointIds = pageRows.Where(x => x.ResolvedConditionMonitoringPointId.HasValue).Select(x => x.ResolvedConditionMonitoringPointId!.Value).Distinct().ToList();
        var pointsById = await _context.ConditionMonitoringPoints
            .Where(x => pointIds.Contains(x.ConditionMonitoringPointId))
            .Select(x => new { x.ConditionMonitoringPointId, x.VariableType })
            .ToDictionaryAsync(x => x.ConditionMonitoringPointId, x => x.VariableType);

        var rowDtos = pageRows.Select(r =>
        {
            var payload = JsonSerializer.Deserialize<ConditionReadingImportRowPayload>(r.PayloadJson, JsonOptions);
            DateTime.TryParse(payload?.ReadingDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var readingDate);
            decimal.TryParse(payload?.ValueRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value);

            return new ConditionReadingImportRowDto
            {
                ConditionReadingImportRowId = r.ConditionReadingImportRowId,
                RowNumber = r.RowNumber,
                SourceRowNumber = r.SourceRowNumber,
                EquipmentTag = payload?.EquipmentTagRaw ?? "",
                PointTag = payload?.PointTagRaw ?? "",
                VariableType = r.ResolvedConditionMonitoringPointId.HasValue && pointsById.TryGetValue(r.ResolvedConditionMonitoringPointId.Value, out var vt) ? vt : null,
                ReadingDate = readingDate == default ? null : readingDate,
                Value = value == default && string.IsNullOrWhiteSpace(payload?.ValueRaw) ? null : value,
                Source = payload?.SourceRaw,
                ValidationStatus = r.ValidationStatus,
                ValidationMessage = r.ValidationMessage,
                IncludeInCommit = r.IncludeInCommit,
                Status = r.Status,
                DuplicateOfConditionReadingId = r.DuplicateOfConditionReadingId,
                CommittedConditionReadingId = r.CommittedConditionReadingId
            };
        }).ToList();

        var summary = BuildSummary(batch, allStatuses);

        return new ConditionReadingImportBatchDto
        {
            ConditionReadingImportBatchId = summary.ConditionReadingImportBatchId,
            FileName = summary.FileName,
            ImportDate = summary.ImportDate,
            Status = summary.Status,
            ImportedByName = summary.ImportedByName,
            TotalRows = summary.TotalRows,
            NewCount = summary.NewCount,
            DuplicateCount = summary.DuplicateCount,
            UnresolvedCount = summary.UnresolvedCount,
            ConflictCount = summary.ConflictCount,
            ValidationErrorCount = summary.ValidationErrorCount,
            Rows = new PagedResult<ConditionReadingImportRowDto>
            {
                Items = rowDtos,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalCount = totalCount
            }
        };
    }

    // ==============================================================================
    // Approve / Commit / Discard
    // ==============================================================================

    public async Task SetRowIncludeAsync(int rowId, bool include)
    {
        var row = await _context.ConditionReadingImportRows.FirstOrDefaultAsync(x => x.ConditionReadingImportRowId == rowId);
        if (row == null) return;

        if (include && row.Status is "UnresolvedPoint" or "ConflictPoint" or "ValidationError")
            return;

        row.IncludeInCommit = include;
        await _context.SaveChangesAsync();
    }

    public async Task<ConditionReadingImportCommitResultDto> CommitBatchAsync(int batchId)
    {
        var batch = await _context.ConditionReadingImportBatches
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.ConditionReadingImportBatchId == batchId);

        if (batch == null) throw new InvalidOperationException("Batch یافت نشد.");

        var result = new ConditionReadingImportCommitResultDto();

        var committableRows = batch.Rows.Where(x => x.IncludeInCommit).ToList();
        var createdPairs = new List<(ConditionReadingImportRow Row, ConditionReading Reading)>();

        if (committableRows.Count > 0)
        {
            var pointIds = committableRows.Where(x => x.ResolvedConditionMonitoringPointId.HasValue).Select(x => x.ResolvedConditionMonitoringPointId!.Value).Distinct().ToList();
            var existingKeys = await BuildExistingReadingKeysAsync(pointIds);
            var committedInThisRun = new HashSet<(int, DateTime)>();

            // طبق فاز ۵: نقاط پایش با پروفایل ارزیابی (برای ارزیابی چندسطحی دقیقاً همون منطق
            // RecordReadingAsync) - یک‌بار برای کل Batch بارگذاری می‌شوند.
            var points = await _context.ConditionMonitoringPoints
                .Where(x => pointIds.Contains(x.ConditionMonitoringPointId))
                .Include(x => x.EvaluationProfile).ThenInclude(p => p!.Limits)
                .ToDictionaryAsync(x => x.ConditionMonitoringPointId);

            foreach (var row in committableRows)
            {
                if (row.Status is "UnresolvedPoint" or "ConflictPoint" or "ValidationError")
                {
                    CountRejected(row, result);
                    continue;
                }

                if (!row.ResolvedConditionMonitoringPointId.HasValue || !points.TryGetValue(row.ResolvedConditionMonitoringPointId.Value, out var point))
                {
                    result.Unresolved++;
                    continue;
                }

                var payload = JsonSerializer.Deserialize<ConditionReadingImportRowPayload>(row.PayloadJson, JsonOptions);
                if (!DateTime.TryParse(payload?.ReadingDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var readingDate) ||
                    !decimal.TryParse(payload?.ValueRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                {
                    result.ValidationErrors++;
                    continue;
                }

                var key = (point.ConditionMonitoringPointId, readingDate);
                if (existingKeys.ContainsKey(key) || committedInThisRun.Contains(key))
                {
                    result.Skipped++;
                    continue;
                }

                var isAlarm = (point.AlarmThresholdLow.HasValue && value < point.AlarmThresholdLow.Value) ||
                              (point.AlarmThresholdHigh.HasValue && value > point.AlarmThresholdHigh.Value);

                var reading = new ConditionReading
                {
                    ConditionMonitoringPointId = point.ConditionMonitoringPointId,
                    ReadingDate = readingDate,
                    Value = value,
                    Source = string.IsNullOrWhiteSpace(payload?.SourceRaw) ? "Manual" : payload.SourceRaw,
                    Provenance = "OBSERVED",
                    IsAlarm = isAlarm,
                    Notes = payload?.NotesRaw,
                    CreatedDate = DateTime.Now
                };

                if (point.EvaluationProfile != null)
                {
                    reading.EvaluationProfileId = point.EvaluationProfile.EvaluationProfileId;
                    reading.EvaluationProfileVersion = point.EvaluationProfile.Version;
                    reading.EvaluationStatus = ConditionMonitoringService.EvaluateAgainstProfile(point.EvaluationProfile, value);
                }

                _context.ConditionReadings.Add(reading);
                committedInThisRun.Add(key);
                createdPairs.Add((row, reading));
                result.Created++;
            }
        }

        foreach (var row in batch.Rows.Where(x => !x.IncludeInCommit))
            CountRejected(row, result);

        batch.Status = "Committed";

        await _context.SaveChangesAsync();

        if (createdPairs.Count > 0)
        {
            foreach (var (row, reading) in createdPairs)
                row.CommittedConditionReadingId = reading.ConditionReadingId;

            await _context.SaveChangesAsync();
        }

        return result;
    }

    private static void CountRejected(ConditionReadingImportRow row, ConditionReadingImportCommitResultDto result)
    {
        switch (row.Status)
        {
            case "UnresolvedPoint": result.Unresolved++; break;
            case "ConflictPoint": result.Conflicts++; break;
            case "ValidationError": result.ValidationErrors++; break;
            default: result.Skipped++; break;
        }
    }

    public async Task DiscardBatchAsync(int batchId)
    {
        var batch = await _context.ConditionReadingImportBatches
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.ConditionReadingImportBatchId == batchId);

        if (batch == null) return;

        _context.ConditionReadingImportRows.RemoveRange(batch.Rows);
        _context.ConditionReadingImportBatches.Remove(batch);

        await _context.SaveChangesAsync();
    }

    // ==============================================================================
    // Excel Header Parsing (عیناً همون الگوی MaintenanceHistoryImportService)
    // ==============================================================================

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
                if (h.Contains("tag") || h.Contains("date") || h.Contains("value") || h.Contains("point"))
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

    private static string? GetDateStringByHeader(IXLWorksheet sheet, int row, Dictionary<string, int> map, out DateTime? parsed, params string[] keys)
    {
        parsed = null;

        foreach (var key in keys)
        {
            if (!map.TryGetValue(key, out var col)) continue;

            var cell = sheet.Cell(row, col);
            if (cell.DataType == XLDataType.DateTime)
            {
                var dt = cell.GetDateTime();
                parsed = dt;
                return dt.ToString("O", CultureInfo.InvariantCulture);
            }

            var v = cell.GetString();
            if (string.IsNullOrWhiteSpace(v)) continue;

            v = v.Trim();
            if (DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedText) ||
                DateTime.TryParse(v, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsedText))
            {
                parsed = parsedText;
                return parsedText.ToString("O", CultureInfo.InvariantCulture);
            }

            return v;
        }

        return null;
    }

    private static string NormalizeHeader(string raw) =>
        raw.Trim().ToLowerInvariant().Replace(" ", "").Replace(".", "").Replace("_", "").Replace("-", "");
}
