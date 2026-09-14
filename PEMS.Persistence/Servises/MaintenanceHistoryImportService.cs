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
/// طبق «PEMS CMMS — Data Migration &amp; Controlled Data Onboarding»: همون الگوی
/// EquipmentFunctionalLocationAssignmentService (تشخیص خودکار Header + Staging + Preview +
/// Commit صریح، بدون Fuzzy/AI) - برای MaintenanceHistory. Audit خودکار از طریق همون Interceptor
/// سراسری PEMSDbContext.SaveChangesAsync پوشش داده می‌شود (بدون کد اضافه این‌جا).
/// </summary>
public class MaintenanceHistoryImportService : IMaintenanceHistoryImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly PEMSDbContext _context;

    public MaintenanceHistoryImportService(PEMSDbContext context)
    {
        _context = context;
    }

    // ==============================================================================
    // Stage
    // ==============================================================================

    public async Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int? importedById)
    {
        using var workbook = new XLWorkbook(fileStream);

        var batch = new MaintenanceHistoryImportBatch
        {
            FileName = fileName,
            ImportedById = importedById,
            ImportDate = DateTime.Now,
            Status = "Staging"
        };

        await _context.MaintenanceHistoryImportBatches.AddAsync(batch);
        await _context.SaveChangesAsync();

        var sheet = workbook.Worksheets.First();
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

        if (lastCol == 0 || lastRow < 2)
            return batch.MaintenanceHistoryImportBatchId;

        var headerRow = FindHeaderRow(sheet, lastCol, Math.Min(lastRow, 6));
        if (headerRow == 0)
            return batch.MaintenanceHistoryImportBatchId;

        var headerMap = BuildHeaderMap(sheet, headerRow, lastCol);

        // طبق بند «Performance and Scale»: Equipment/WorkOrder فقط یک‌بار برای کل فایل
        // پیش‌بارگذاری می‌شوند (نه یک Query جدا برای هر ردیف اکسل).
        var equipmentByTag = await BuildEquipmentLookupAsync();
        var workOrderByNumber = await BuildWorkOrderLookupAsync();

        // طبق بند «Idempotency»: تشخیص تکرار محدود به Equipmentهایی می‌شود که واقعاً در همین
        // فایل ظاهر شده‌اند - نه بارگذاری کل جدول MaintenanceHistories که می‌تواند به‌مراتب
        // بزرگ‌تر از Equipment (۴۰٬۸۰۹ ردیف) باشد.
        var rawRows = ReadRawRows(sheet, headerRow, lastRow, headerMap);
        var candidateEquipmentIds = rawRows
            .Select(r => equipmentByTag.TryGetValue((r.TagRaw ?? "").Trim(), out var matches) && matches.Count == 1 ? matches[0].EquipmentId : (int?)null)
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var existingKeys = await BuildExistingHistoryKeysAsync(candidateEquipmentIds);
        var seenInBatch = new HashSet<(int EquipmentId, DateTime Date, string Action, string Technician)>();

        var stagingRowNumber = 0;

        foreach (var raw in rawRows)
        {
            if (string.IsNullOrWhiteSpace(raw.TagRaw) && string.IsNullOrWhiteSpace(raw.DateRaw))
                continue; // ردیف کاملاً خالی - بی‌صدا رد می‌شود

            stagingRowNumber++;

            var payload = new MaintenanceHistoryImportRowPayload
            {
                EquipmentTagRaw = raw.TagRaw ?? "",
                ExecutionDateRaw = raw.DateRaw ?? "",
                WorkOrderNumberRaw = raw.WorkOrderRaw,
                FindingRaw = raw.FindingRaw,
                ActionTakenRaw = raw.ActionTakenRaw,
                TechnicianRaw = raw.TechnicianRaw,
                DurationHoursRaw = raw.DurationHoursRaw,
                CostRaw = raw.CostRaw,
                DescriptionRaw = raw.DescriptionRaw
            };

            var resolved = Resolve(payload, raw.ParsedDate, equipmentByTag, workOrderByNumber, existingKeys, seenInBatch);

            await _context.MaintenanceHistoryImportRows.AddAsync(new MaintenanceHistoryImportRow
            {
                MaintenanceHistoryImportBatchId = batch.MaintenanceHistoryImportBatchId,
                RowNumber = stagingRowNumber,
                SourceRowNumber = raw.SourceRowNumber,
                PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
                ValidationStatus = resolved.ValidationStatus,
                ValidationMessage = resolved.Message,
                IncludeInCommit = resolved.IncludeInCommit,
                Status = resolved.Status,
                ResolvedEquipmentId = resolved.EquipmentId,
                ResolvedWorkOrderId = resolved.WorkOrderId,
                DuplicateOfMaintenanceHistoryId = resolved.DuplicateOfId
            });
        }

        await _context.SaveChangesAsync();

        return batch.MaintenanceHistoryImportBatchId;
    }

    // ==============================================================================
    // Raw row extraction (جدا از Resolve - برای این‌که تاریخ یک‌بار Parse شود، نه دوباره)
    // ==============================================================================

    private sealed class RawRow
    {
        public int SourceRowNumber { get; set; }
        public string? TagRaw { get; set; }
        public string? DateRaw { get; set; }
        public DateTime? ParsedDate { get; set; }
        public string? WorkOrderRaw { get; set; }
        public string? FindingRaw { get; set; }
        public string? ActionTakenRaw { get; set; }
        public string? TechnicianRaw { get; set; }
        public string? DurationHoursRaw { get; set; }
        public string? CostRaw { get; set; }
        public string? DescriptionRaw { get; set; }
    }

    private static List<RawRow> ReadRawRows(IXLWorksheet sheet, int headerRow, int lastRow, Dictionary<string, int> headerMap)
    {
        var result = new List<RawRow>();

        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var tagRaw = GetByHeader(sheet, r, headerMap, "equipmenttag", "tag", "tagnumber", "equipment");
            var dateRaw = GetDateStringByHeader(sheet, r, headerMap, out var parsedDate, "executiondate", "date", "maintenancedate", "servicedate");

            if (string.IsNullOrWhiteSpace(tagRaw) && string.IsNullOrWhiteSpace(dateRaw))
                continue;

            result.Add(new RawRow
            {
                SourceRowNumber = r,
                TagRaw = tagRaw,
                DateRaw = dateRaw,
                ParsedDate = parsedDate,
                WorkOrderRaw = GetByHeader(sheet, r, headerMap, "workordernumber", "workorder", "wo", "wonumber"),
                FindingRaw = GetByHeader(sheet, r, headerMap, "finding"),
                ActionTakenRaw = GetByHeader(sheet, r, headerMap, "actiontaken", "action"),
                TechnicianRaw = GetByHeader(sheet, r, headerMap, "technician", "performedby", "executedby"),
                DurationHoursRaw = GetByHeader(sheet, r, headerMap, "durationhours", "duration"),
                CostRaw = GetByHeader(sheet, r, headerMap, "cost"),
                DescriptionRaw = GetByHeader(sheet, r, headerMap, "description", "remarks", "notes")
            });
        }

        return result;
    }

    // ==============================================================================
    // Resolution (Deterministic only - بدون Fuzzy/AI؛ فقط تطبیق دقیق Tag/WorkOrderNumber و
    // کلید ترکیبی قطعی برای تشخیص تکرار)
    // ==============================================================================

    private sealed class EquipmentLookupEntry
    {
        public int EquipmentId { get; set; }
        public string? Description { get; set; }
    }

    private sealed class ResolveResult
    {
        public string Status { get; set; } = "";
        public string ValidationStatus { get; set; } = "Valid";
        public string? Message { get; set; }
        public bool IncludeInCommit { get; set; }
        public int? EquipmentId { get; set; }
        public int? WorkOrderId { get; set; }
        public int? DuplicateOfId { get; set; }
    }

    private static ResolveResult Resolve(
        MaintenanceHistoryImportRowPayload payload,
        DateTime? parsedDate,
        Dictionary<string, List<EquipmentLookupEntry>> equipmentByTag,
        Dictionary<string, List<int>> workOrderByNumber,
        Dictionary<(int EquipmentId, DateTime Date, string Action, string Technician), int> existingKeys,
        HashSet<(int EquipmentId, DateTime Date, string Action, string Technician)> seenInBatch)
    {
        var tag = payload.EquipmentTagRaw?.Trim() ?? "";

        if (tag.Length == 0)
            return new ResolveResult { Status = "ValidationError", ValidationStatus = "Error", Message = "Equipment Tag خالی است.", IncludeInCommit = false };

        if (!parsedDate.HasValue)
            return new ResolveResult { Status = "ValidationError", ValidationStatus = "Error", Message = "تاریخ انجام (Execution Date) خالی یا نامعتبر است.", IncludeInCommit = false };

        if (!equipmentByTag.TryGetValue(tag, out var equipmentMatches) || equipmentMatches.Count == 0)
            return new ResolveResult { Status = "UnresolvedEquipment", ValidationStatus = "Error", Message = $"تجهیزی با Tag «{tag}» پیدا نشد.", IncludeInCommit = false };

        if (equipmentMatches.Count > 1)
            return new ResolveResult { Status = "ConflictEquipment", ValidationStatus = "Error", Message = $"چند تجهیز با Tag «{tag}» پیدا شد ({equipmentMatches.Count} مورد) - تطبیق مطمئن ممکن نیست.", IncludeInCommit = false };

        var equipmentId = equipmentMatches[0].EquipmentId;

        // طبق بند «WorkOrder Soft Reference»: هیچ‌وقت Row را رد نمی‌کند - فقط هشدار می‌دهد و
        // WorkOrderId خالی می‌ماند (schema هم nullable است).
        int? workOrderId = null;
        string? workOrderWarning = null;
        var woRaw = payload.WorkOrderNumberRaw?.Trim();
        if (!string.IsNullOrEmpty(woRaw))
        {
            if (workOrderByNumber.TryGetValue(woRaw, out var woMatches) && woMatches.Count == 1)
                workOrderId = woMatches[0];
            else if (woMatches is { Count: > 1 })
                workOrderWarning = $"چند Work Order با شماره «{woRaw}» پیدا شد - رکورد بدون پیوند Work Order ثبت می‌شود.";
            else
                workOrderWarning = $"Work Order با شماره «{woRaw}» پیدا نشد - رکورد بدون پیوند Work Order ثبت می‌شود.";
        }

        var action = NormalizeKeyPart(payload.ActionTakenRaw);
        var technician = NormalizeKeyPart(payload.TechnicianRaw);
        var key = (equipmentId, parsedDate.Value.Date, action, technician);

        if (existingKeys.TryGetValue(key, out var existingId))
        {
            return new ResolveResult
            {
                Status = "Duplicate",
                ValidationStatus = "Warning",
                Message = "یک رکورد MaintenanceHistory با همین تجهیز/تاریخ/اقدام/تکنسین از قبل وجود دارد - برای جلوگیری از تکرار، پیش‌فرض ثبت نمی‌شود.",
                IncludeInCommit = false,
                EquipmentId = equipmentId,
                WorkOrderId = workOrderId,
                DuplicateOfId = existingId
            };
        }

        if (seenInBatch.Contains(key))
        {
            return new ResolveResult
            {
                Status = "Duplicate",
                ValidationStatus = "Warning",
                Message = "یک ردیف دیگر در همین فایل با همین کلید ترکیبی (تجهیز/تاریخ/اقدام/تکنسین) از قبل پردازش شده است.",
                IncludeInCommit = false,
                EquipmentId = equipmentId,
                WorkOrderId = workOrderId
            };
        }

        seenInBatch.Add(key);

        return new ResolveResult
        {
            Status = "New",
            ValidationStatus = workOrderWarning != null ? "Warning" : "Valid",
            Message = workOrderWarning,
            IncludeInCommit = true,
            EquipmentId = equipmentId,
            WorkOrderId = workOrderId
        };
    }

    // کلید ترکیبی Idempotency باید مستقل از تفاوت‌های صرفاً نگارشی (Case/فاصله‌ی اضافه) باشد -
    // نه یک تطبیق Fuzzy، فقط نرمال‌سازی قطعی (Trim + ToLowerInvariant).
    private static string NormalizeKeyPart(string? raw) => (raw ?? "").Trim().ToLowerInvariant();

    private async Task<Dictionary<string, List<EquipmentLookupEntry>>> BuildEquipmentLookupAsync()
    {
        var rows = await _context.Equipments
            .Where(x => x.IsActive)
            .Select(x => new { x.EquipmentId, x.TagNumber, x.Description })
            .ToListAsync();

        var map = new Dictionary<string, List<EquipmentLookupEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var key = row.TagNumber?.Trim() ?? "";
            if (key.Length == 0) continue;

            if (!map.TryGetValue(key, out var list))
            {
                list = new List<EquipmentLookupEntry>();
                map[key] = list;
            }

            list.Add(new EquipmentLookupEntry { EquipmentId = row.EquipmentId, Description = row.Description });
        }

        return map;
    }

    private async Task<Dictionary<string, List<int>>> BuildWorkOrderLookupAsync()
    {
        var rows = await _context.WorkOrders
            .Select(x => new { x.WorkOrderId, x.WorkOrderNumber })
            .ToListAsync();

        var map = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var key = row.WorkOrderNumber?.Trim() ?? "";
            if (key.Length == 0) continue;

            if (!map.TryGetValue(key, out var list))
            {
                list = new List<int>();
                map[key] = list;
            }

            list.Add(row.WorkOrderId);
        }

        return map;
    }

    // طبق بند «Performance and Scale»: فقط MaintenanceHistory متعلق به Equipmentهایی که واقعاً
    // در این فایل ظاهر شده‌اند بارگذاری می‌شود - نه کل جدول (که می‌تواند خیلی بزرگ‌تر از
    // Equipment باشد).
    private async Task<Dictionary<(int EquipmentId, DateTime Date, string Action, string Technician), int>> BuildExistingHistoryKeysAsync(List<int> equipmentIds)
    {
        var result = new Dictionary<(int, DateTime, string, string), int>();
        if (equipmentIds.Count == 0) return result;

        var rows = await _context.MaintenanceHistories
            .Where(x => equipmentIds.Contains(x.EquipmentId))
            .Select(x => new { x.MaintenanceHistoryId, x.EquipmentId, x.ExecutionDate, x.ActionTaken, x.Technician })
            .ToListAsync();

        foreach (var row in rows)
        {
            var key = (row.EquipmentId, row.ExecutionDate.Date, NormalizeKeyPart(row.ActionTaken), NormalizeKeyPart(row.Technician));
            result.TryAdd(key, row.MaintenanceHistoryId); // اولین رخداد کافی است - فقط برای تشخیص وجود لازم است
        }

        return result;
    }

    // ==============================================================================
    // Review / Query
    // ==============================================================================

    public async Task<MaintenanceHistoryImportBatchSummaryDto?> GetBatchSummaryAsync(int batchId)
    {
        var batch = await _context.MaintenanceHistoryImportBatches
            .Include(x => x.ImportedBy)
            .FirstOrDefaultAsync(x => x.MaintenanceHistoryImportBatchId == batchId);

        if (batch == null) return null;

        var rows = await _context.MaintenanceHistoryImportRows
            .Where(x => x.MaintenanceHistoryImportBatchId == batchId)
            .Select(x => x.Status)
            .ToListAsync();

        return BuildSummary(batch, rows);
    }

    public async Task<List<MaintenanceHistoryImportBatchSummaryDto>> GetRecentBatchesAsync()
    {
        var batches = await _context.MaintenanceHistoryImportBatches
            .Include(x => x.ImportedBy)
            .OrderByDescending(x => x.ImportDate)
            .Take(20)
            .ToListAsync();

        var result = new List<MaintenanceHistoryImportBatchSummaryDto>();
        foreach (var batch in batches)
        {
            var rows = await _context.MaintenanceHistoryImportRows
                .Where(x => x.MaintenanceHistoryImportBatchId == batch.MaintenanceHistoryImportBatchId)
                .Select(x => x.Status)
                .ToListAsync();

            result.Add(BuildSummary(batch, rows));
        }

        return result;
    }

    private static MaintenanceHistoryImportBatchSummaryDto BuildSummary(MaintenanceHistoryImportBatch batch, List<string> statuses) => new()
    {
        MaintenanceHistoryImportBatchId = batch.MaintenanceHistoryImportBatchId,
        FileName = batch.FileName,
        ImportDate = batch.ImportDate,
        Status = batch.Status,
        ImportedByName = batch.ImportedBy != null ? $"{batch.ImportedBy.FirstName} {batch.ImportedBy.LastName}" : null,
        TotalRows = statuses.Count,
        NewCount = statuses.Count(x => x == "New"),
        DuplicateCount = statuses.Count(x => x == "Duplicate"),
        UnresolvedCount = statuses.Count(x => x == "UnresolvedEquipment"),
        ConflictCount = statuses.Count(x => x == "ConflictEquipment"),
        ValidationErrorCount = statuses.Count(x => x == "ValidationError")
    };

    public async Task<MaintenanceHistoryImportBatchDto?> GetBatchAsync(int batchId, int page, int pageSize, string? statusFilter)
    {
        var batch = await _context.MaintenanceHistoryImportBatches
            .Include(x => x.ImportedBy)
            .FirstOrDefaultAsync(x => x.MaintenanceHistoryImportBatchId == batchId);

        if (batch == null) return null;

        var allStatuses = await _context.MaintenanceHistoryImportRows
            .Where(x => x.MaintenanceHistoryImportBatchId == batchId)
            .Select(x => x.Status)
            .ToListAsync();

        var query = _context.MaintenanceHistoryImportRows
            .Where(x => x.MaintenanceHistoryImportBatchId == batchId);

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

        var equipmentIds = pageRows.Where(x => x.ResolvedEquipmentId.HasValue).Select(x => x.ResolvedEquipmentId!.Value).Distinct().ToList();
        var descriptionsById = await _context.Equipments
            .Where(x => equipmentIds.Contains(x.EquipmentId))
            .Select(x => new { x.EquipmentId, x.Description })
            .ToDictionaryAsync(x => x.EquipmentId, x => x.Description);

        var workOrderIds = pageRows.Where(x => x.ResolvedWorkOrderId.HasValue).Select(x => x.ResolvedWorkOrderId!.Value).Distinct().ToList();
        var workOrderNumbersById = await _context.WorkOrders
            .Where(x => workOrderIds.Contains(x.WorkOrderId))
            .Select(x => new { x.WorkOrderId, x.WorkOrderNumber })
            .ToDictionaryAsync(x => x.WorkOrderId, x => x.WorkOrderNumber);

        var rowDtos = pageRows.Select(r =>
        {
            var payload = JsonSerializer.Deserialize<MaintenanceHistoryImportRowPayload>(r.PayloadJson, JsonOptions);
            DateTime.TryParse(payload?.ExecutionDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var execDate);

            return new MaintenanceHistoryImportRowDto
            {
                MaintenanceHistoryImportRowId = r.MaintenanceHistoryImportRowId,
                RowNumber = r.RowNumber,
                SourceRowNumber = r.SourceRowNumber,
                EquipmentTag = payload?.EquipmentTagRaw ?? "",
                EquipmentDescription = r.ResolvedEquipmentId.HasValue && descriptionsById.TryGetValue(r.ResolvedEquipmentId.Value, out var d) ? d : null,
                ExecutionDate = execDate == default ? null : execDate,
                WorkOrderNumber = r.ResolvedWorkOrderId.HasValue && workOrderNumbersById.TryGetValue(r.ResolvedWorkOrderId.Value, out var wo) ? wo : payload?.WorkOrderNumberRaw,
                ActionTaken = payload?.ActionTakenRaw,
                Technician = payload?.TechnicianRaw,
                ValidationStatus = r.ValidationStatus,
                ValidationMessage = r.ValidationMessage,
                IncludeInCommit = r.IncludeInCommit,
                Status = r.Status,
                DuplicateOfMaintenanceHistoryId = r.DuplicateOfMaintenanceHistoryId,
                CommittedMaintenanceHistoryId = r.CommittedMaintenanceHistoryId
            };
        }).ToList();

        var summary = BuildSummary(batch, allStatuses);

        return new MaintenanceHistoryImportBatchDto
        {
            MaintenanceHistoryImportBatchId = summary.MaintenanceHistoryImportBatchId,
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
            Rows = new PagedResult<MaintenanceHistoryImportRowDto>
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
        var row = await _context.MaintenanceHistoryImportRows.FirstOrDefaultAsync(x => x.MaintenanceHistoryImportRowId == rowId);
        if (row == null) return;

        // طبق بند «Do not commit invalid rows»: هرگز نمی‌توان یک ردیف Unresolved/Conflict/
        // ValidationError را با روشن‌کردن این پرچم به Commit اضافه کرد.
        if (include && row.Status is "UnresolvedEquipment" or "ConflictEquipment" or "ValidationError")
            return;

        row.IncludeInCommit = include;
        await _context.SaveChangesAsync();
    }

    public async Task<MaintenanceHistoryImportCommitResultDto> CommitBatchAsync(int batchId)
    {
        var batch = await _context.MaintenanceHistoryImportBatches
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.MaintenanceHistoryImportBatchId == batchId);

        if (batch == null) throw new InvalidOperationException("Batch یافت نشد.");

        var result = new MaintenanceHistoryImportCommitResultDto();

        // طبق بند «Commit Safety»: بدون foreach { SaveChanges() } به‌ازای هر ردیف - فقط دو
        // SaveChangesAsync برای کل Batch (نه به تعداد ردیف‌ها): یکی برای درج رکوردهای جدید
        // (تا شناسه‌ی Identity تولید شود)، یکی برای ثبت CommittedMaintenanceHistoryId روی
        // ردیف‌های Staging - دقیقاً همون قید کارایی، نه یک استثنا برایش.
        var committableRows = batch.Rows.Where(x => x.IncludeInCommit).ToList();
        var createdPairs = new List<(MaintenanceHistoryImportRow Row, MaintenanceHistory History)>();

        if (committableRows.Count > 0)
        {
            // طبق بند «Idempotency در لحظه‌ی Commit»: وضعیت واقعی دیتابیس دوباره بررسی می‌شود -
            // نه فقط عکس فوری زمان Staging (ممکن است بین Staging و Commit، یک Import دیگر
            // همین رکوردها را ثبت کرده باشد).
            var equipmentIds = committableRows.Where(x => x.ResolvedEquipmentId.HasValue).Select(x => x.ResolvedEquipmentId!.Value).Distinct().ToList();
            var existingKeys = await BuildExistingHistoryKeysAsync(equipmentIds);
            var committedInThisRun = new HashSet<(int, DateTime, string, string)>();

            foreach (var row in committableRows)
            {
                if (row.Status is "UnresolvedEquipment" or "ConflictEquipment" or "ValidationError")
                {
                    // محافظت دفاعی - این ردیف‌ها هرگز نباید IncludeInCommit=true داشته باشند
                    CountRejected(row, result);
                    continue;
                }

                if (!row.ResolvedEquipmentId.HasValue)
                {
                    result.Unresolved++;
                    continue;
                }

                var payload = JsonSerializer.Deserialize<MaintenanceHistoryImportRowPayload>(row.PayloadJson, JsonOptions);
                if (!DateTime.TryParse(payload?.ExecutionDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var executionDate))
                {
                    result.ValidationErrors++;
                    continue;
                }

                var key = (row.ResolvedEquipmentId.Value, executionDate.Date, NormalizeKeyPart(payload?.ActionTakenRaw), NormalizeKeyPart(payload?.TechnicianRaw));
                if (existingKeys.ContainsKey(key) || committedInThisRun.Contains(key))
                {
                    result.Skipped++; // از قبل ثبت شده - چیزی برای تکرار نیست، هرگز بازنویسی نمی‌شود
                    continue;
                }

                decimal? duration = null;
                if (!string.IsNullOrWhiteSpace(payload?.DurationHoursRaw) && decimal.TryParse(payload.DurationHoursRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    duration = d;

                decimal? cost = null;
                if (!string.IsNullOrWhiteSpace(payload?.CostRaw) && decimal.TryParse(payload.CostRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var c))
                    cost = c;

                var history = new MaintenanceHistory
                {
                    EquipmentId = row.ResolvedEquipmentId.Value,
                    WorkOrderId = row.ResolvedWorkOrderId,
                    ExecutionDate = executionDate,
                    Finding = payload?.FindingRaw,
                    ActionTaken = payload?.ActionTakenRaw,
                    Technician = payload?.TechnicianRaw,
                    DurationHours = duration,
                    Cost = cost,
                    Description = payload?.DescriptionRaw,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                _context.MaintenanceHistories.Add(history);
                committedInThisRun.Add(key);
                createdPairs.Add((row, history));
                result.Created++;
            }
        }

        foreach (var row in batch.Rows.Where(x => !x.IncludeInCommit))
            CountRejected(row, result);

        batch.Status = "Committed";

        await _context.SaveChangesAsync(); // این‌جا Identity واقعی هر MaintenanceHistory تولید می‌شود

        if (createdPairs.Count > 0)
        {
            foreach (var (row, history) in createdPairs)
                row.CommittedMaintenanceHistoryId = history.MaintenanceHistoryId;

            await _context.SaveChangesAsync();
        }

        return result;
    }

    private static void CountRejected(MaintenanceHistoryImportRow row, MaintenanceHistoryImportCommitResultDto result)
    {
        switch (row.Status)
        {
            case "UnresolvedEquipment": result.Unresolved++; break;
            case "ConflictEquipment": result.Conflicts++; break;
            case "ValidationError": result.ValidationErrors++; break;
            default: result.Skipped++; break; // Duplicate یا New که کاربر خودش خاموش کرد
        }
    }

    public async Task DiscardBatchAsync(int batchId)
    {
        var batch = await _context.MaintenanceHistoryImportBatches
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.MaintenanceHistoryImportBatchId == batchId);

        if (batch == null) return;

        _context.MaintenanceHistoryImportRows.RemoveRange(batch.Rows);
        _context.MaintenanceHistoryImportBatches.Remove(batch);

        await _context.SaveChangesAsync();
    }

    // ==============================================================================
    // Excel Header Parsing (عیناً همون الگوی EquipmentFunctionalLocationAssignmentService)
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
                if (h.Contains("tag") || h.Contains("date") || h.Contains("workorder") || h.Contains("action"))
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

    // اکسل تاریخ را یا به‌صورت مقدار واقعی DateTime سلول (فرمت تاریخ اکسل) یا به‌صورت متن خام
    // ذخیره می‌کند - هر دو حالت پشتیبانی می‌شود، بدون هیچ حدس Fuzzy.
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

            return v; // مقدار خام برگردانده می‌شود تا در پیام خطا/بازبینی قابل مشاهده باشد، حتی اگر Parse نشود
        }

        return null;
    }

    private static string NormalizeHeader(string raw) =>
        raw.Trim().ToLowerInvariant().Replace(" ", "").Replace(".", "").Replace("_", "").Replace("-", "");
}
