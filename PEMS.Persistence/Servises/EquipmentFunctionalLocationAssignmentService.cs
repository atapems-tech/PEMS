using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS CMMS — Equipment Functional Location Assignment &amp; Import»: همون الگوی
/// FunctionalLocationImportService/EquipmentImportService (تشخیص خودکار Header + Staging +
/// Preview + Commit صریح) - با یک تفاوت بنیادی: این سرویس هیچ Equipment/FunctionalLocation
/// جدیدی نمی‌سازد و هیچ تطبیق Fuzzy/AI ندارد؛ فقط با تطبیق قطعی Equipment Tag و Functional
/// Location Code، رابطه‌ی Equipment.FunctionalLocationId موجود را - فقط بعد از تایید صریح
/// کاربر - به‌روز می‌کند. Audit خودکار از طریق همون Interceptor سراسری
/// PEMSDbContext.SaveChangesAsync (بدون نیاز به کد اضافه این‌جا) پوشش داده می‌شود.
/// </summary>
public class EquipmentFunctionalLocationAssignmentService : IEquipmentFunctionalLocationAssignmentService
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly PEMSDbContext _context;

    public EquipmentFunctionalLocationAssignmentService(PEMSDbContext context)
    {
        _context = context;
    }

    // ==============================================================================
    // Stage
    // ==============================================================================

    public async Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int? importedById)
    {
        using var workbook = new XLWorkbook(fileStream);

        var batch = new EquipmentFunctionalLocationAssignmentBatch
        {
            FileName = fileName,
            ImportedById = importedById,
            ImportDate = DateTime.Now,
            Status = "Staging"
        };

        await _context.EquipmentFunctionalLocationAssignmentBatches.AddAsync(batch);
        await _context.SaveChangesAsync();

        var sheet = workbook.Worksheets.First();
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

        if (lastCol == 0 || lastRow < 2)
            return batch.EquipmentFunctionalLocationAssignmentBatchId;

        var headerRow = FindHeaderRow(sheet, lastCol, Math.Min(lastRow, 6));
        if (headerRow == 0)
            return batch.EquipmentFunctionalLocationAssignmentBatchId;

        var headerMap = BuildHeaderMap(sheet, headerRow, lastCol);

        // طبق بند «Import Size and Performance»: کل Equipment/FunctionalLocation یک‌بار پیش‌
        // بارگذاری می‌شود (نه یک Query جدا برای هر ردیف اکسل) - فقط ستون‌های لازم، نه موجودیت کامل.
        var equipmentByTag = await BuildEquipmentLookupAsync();
        var flByCode = await BuildFunctionalLocationLookupAsync();

        var stagingRowNumber = 0;

        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var tagRaw = GetByHeader(sheet, r, headerMap, "equipmenttag", "tag", "tagnumber");
            var codeRaw = GetByHeader(sheet, r, headerMap, "functionallocationcode", "flcode", "code", "functionallocation");
            var plantRaw = GetByHeader(sheet, r, headerMap, "plant");
            var areaRaw = GetByHeader(sheet, r, headerMap, "area");
            var unitRaw = GetByHeader(sheet, r, headerMap, "unit");

            if (string.IsNullOrWhiteSpace(tagRaw) && string.IsNullOrWhiteSpace(codeRaw))
                continue; // ردیف کاملاً خالی - بی‌صدا رد می‌شود

            stagingRowNumber++;

            var payload = new EquipmentFunctionalLocationAssignmentRowPayload
            {
                EquipmentTagRaw = tagRaw ?? "",
                FunctionalLocationCodeRaw = codeRaw ?? "",
                PlantRaw = plantRaw,
                AreaRaw = areaRaw,
                UnitRaw = unitRaw
            };

            var resolved = Resolve(payload, equipmentByTag, flByCode);

            await _context.EquipmentFunctionalLocationAssignmentRows.AddAsync(new EquipmentFunctionalLocationAssignmentRow
            {
                EquipmentFunctionalLocationAssignmentBatchId = batch.EquipmentFunctionalLocationAssignmentBatchId,
                RowNumber = stagingRowNumber,
                SourceRowNumber = r,
                PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
                ValidationStatus = resolved.ValidationStatus,
                ValidationMessage = resolved.Message,
                IncludeInCommit = resolved.IncludeInCommit,
                Status = resolved.Status,
                ResolvedEquipmentId = resolved.EquipmentId,
                ResolvedFunctionalLocationId = resolved.FunctionalLocationId,
                PreviousFunctionalLocationId = resolved.PreviousFunctionalLocationId
            });
        }

        await _context.SaveChangesAsync();

        return batch.EquipmentFunctionalLocationAssignmentBatchId;
    }

    // ==============================================================================
    // Resolution (Deterministic only - طبق بند «Matching Rules»/«Fuzzy / AI Suggestions»:
    // بدون Fuzzy/AI؛ فقط تطبیق دقیق Tag/Code)
    // ==============================================================================

    private sealed class EquipmentLookupEntry
    {
        public int EquipmentId { get; set; }
        public int? FunctionalLocationId { get; set; }
        public string? Description { get; set; }
    }

    private sealed class FunctionalLocationLookupEntry
    {
        public int FunctionalLocationId { get; set; }
        public string? Plant { get; set; }
        public string? Area { get; set; }
        public string? Unit { get; set; }
    }

    private sealed class ResolveResult
    {
        public string Status { get; set; } = "";
        public string ValidationStatus { get; set; } = "Valid";
        public string? Message { get; set; }
        public bool IncludeInCommit { get; set; }
        public int? EquipmentId { get; set; }
        public int? FunctionalLocationId { get; set; }
        public int? PreviousFunctionalLocationId { get; set; }
    }

    // طبق بند «Matching Rules»: هر Tag/Code می‌تواند صفر (Unresolved)، یک (Resolved)، یا چند
    // (Conflict) بار در دیتابیس تکرار شده باشد - چون هیچ محدودیت Uniqueness واقعی روی
    // Equipment.TagNumber/FunctionalLocation.Code در Schema فعلی وجود ندارد (طبق ممیزی مستقیم).
    private static ResolveResult Resolve(
        EquipmentFunctionalLocationAssignmentRowPayload payload,
        Dictionary<string, List<EquipmentLookupEntry>> equipmentByTag,
        Dictionary<string, List<FunctionalLocationLookupEntry>> flByCode)
    {
        var tag = payload.EquipmentTagRaw?.Trim() ?? "";
        var code = payload.FunctionalLocationCodeRaw?.Trim() ?? "";

        if (tag.Length == 0 || code.Length == 0)
        {
            return new ResolveResult
            {
                Status = "ValidationError",
                ValidationStatus = "Error",
                Message = tag.Length == 0 ? "Equipment Tag خالی است." : "Functional Location Code خالی است.",
                IncludeInCommit = false
            };
        }

        if (!equipmentByTag.TryGetValue(tag, out var equipmentMatches) || equipmentMatches.Count == 0)
        {
            return new ResolveResult
            {
                Status = "UnresolvedEquipment",
                ValidationStatus = "Error",
                Message = $"تجهیزی با Tag «{tag}» پیدا نشد.",
                IncludeInCommit = false
            };
        }

        if (equipmentMatches.Count > 1)
        {
            return new ResolveResult
            {
                Status = "ConflictEquipment",
                ValidationStatus = "Error",
                Message = $"چند تجهیز با Tag «{tag}» پیدا شد ({equipmentMatches.Count} مورد) - تطبیق مطمئن ممکن نیست.",
                IncludeInCommit = false
            };
        }

        if (!flByCode.TryGetValue(code, out var flMatches) || flMatches.Count == 0)
        {
            return new ResolveResult
            {
                Status = "UnresolvedFunctionalLocation",
                ValidationStatus = "Error",
                Message = $"Functional Location با کد «{code}» پیدا نشد.",
                IncludeInCommit = false,
                EquipmentId = equipmentMatches[0].EquipmentId,
                PreviousFunctionalLocationId = equipmentMatches[0].FunctionalLocationId
            };
        }

        if (flMatches.Count > 1)
        {
            return new ResolveResult
            {
                Status = "ConflictFunctionalLocation",
                ValidationStatus = "Error",
                Message = $"چند Functional Location با کد «{code}» پیدا شد ({flMatches.Count} مورد) - تطبیق مطمئن ممکن نیست.",
                IncludeInCommit = false,
                EquipmentId = equipmentMatches[0].EquipmentId,
                PreviousFunctionalLocationId = equipmentMatches[0].FunctionalLocationId
            };
        }

        var equipment = equipmentMatches[0];
        var fl = flMatches[0];

        // طبق بند «Optional contextual validation»: فقط اگر ستون‌های Plant/Area/Unit واقعاً در
        // فایل موجود باشند و با FL تطبیق‌یافته ناسازگار باشند - وگرنه بی‌صدا رد می‌شود.
        var contextIssue = ValidateContext(payload, fl);
        if (contextIssue != null)
        {
            return new ResolveResult
            {
                Status = "ValidationError",
                ValidationStatus = "Warning",
                Message = contextIssue,
                IncludeInCommit = false,
                EquipmentId = equipment.EquipmentId,
                FunctionalLocationId = fl.FunctionalLocationId,
                PreviousFunctionalLocationId = equipment.FunctionalLocationId
            };
        }

        if (equipment.FunctionalLocationId == fl.FunctionalLocationId)
        {
            // طبق بند «Idempotency»: از قبل دقیقاً همین تخصیص برقرار است - نیازی به تغییر نیست
            return new ResolveResult
            {
                Status = "NoChange",
                ValidationStatus = "Valid",
                Message = "این تجهیز از قبل دقیقاً به همین Functional Location متصل است.",
                IncludeInCommit = false,
                EquipmentId = equipment.EquipmentId,
                FunctionalLocationId = fl.FunctionalLocationId,
                PreviousFunctionalLocationId = equipment.FunctionalLocationId
            };
        }

        if (equipment.FunctionalLocationId.HasValue)
        {
            // طبق بند «Existing Assignment Protection»: تخصیص موجود هرگز خاموش بازنویسی نمی‌شود -
            // پیش‌فرض IncludeInCommit=false تا کاربر صریحاً ReplaceExistingAssignment را فعال کند.
            return new ResolveResult
            {
                Status = "AlreadyAssigned",
                ValidationStatus = "Warning",
                Message = "این تجهیز از قبل به یک Functional Location متفاوت متصل است - جایگزینی نیازمند تایید صریح است.",
                IncludeInCommit = false,
                EquipmentId = equipment.EquipmentId,
                FunctionalLocationId = fl.FunctionalLocationId,
                PreviousFunctionalLocationId = equipment.FunctionalLocationId
            };
        }

        return new ResolveResult
        {
            Status = "Exact",
            ValidationStatus = "Valid",
            Message = null,
            IncludeInCommit = true,
            EquipmentId = equipment.EquipmentId,
            FunctionalLocationId = fl.FunctionalLocationId,
            PreviousFunctionalLocationId = null
        };
    }

    private static string? ValidateContext(EquipmentFunctionalLocationAssignmentRowPayload payload, FunctionalLocationLookupEntry fl)
    {
        if (!string.IsNullOrWhiteSpace(payload.PlantRaw) && !string.IsNullOrWhiteSpace(fl.Plant) &&
            !string.Equals(payload.PlantRaw.Trim(), fl.Plant.Trim(), StringComparison.OrdinalIgnoreCase))
            return $"Plant فایل («{payload.PlantRaw}») با Plant واقعی این Functional Location («{fl.Plant}») مطابقت ندارد.";

        if (!string.IsNullOrWhiteSpace(payload.AreaRaw) && !string.IsNullOrWhiteSpace(fl.Area) &&
            !string.Equals(payload.AreaRaw.Trim(), fl.Area.Trim(), StringComparison.OrdinalIgnoreCase))
            return $"Area فایل («{payload.AreaRaw}») با Area واقعی این Functional Location («{fl.Area}») مطابقت ندارد.";

        if (!string.IsNullOrWhiteSpace(payload.UnitRaw) && !string.IsNullOrWhiteSpace(fl.Unit) &&
            !string.Equals(payload.UnitRaw.Trim(), fl.Unit.Trim(), StringComparison.OrdinalIgnoreCase))
            return $"Unit فایل («{payload.UnitRaw}») با Unit واقعی این Functional Location («{fl.Unit}») مطابقت ندارد.";

        return null;
    }

    private async Task<Dictionary<string, List<EquipmentLookupEntry>>> BuildEquipmentLookupAsync()
    {
        var rows = await _context.Equipments
            .Where(x => x.IsActive)
            .Select(x => new { x.EquipmentId, x.TagNumber, x.FunctionalLocationId, x.Description })
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

            list.Add(new EquipmentLookupEntry { EquipmentId = row.EquipmentId, FunctionalLocationId = row.FunctionalLocationId, Description = row.Description });
        }

        return map;
    }

    private async Task<Dictionary<string, List<FunctionalLocationLookupEntry>>> BuildFunctionalLocationLookupAsync()
    {
        var rows = await _context.FunctionalLocations
            .Where(x => x.IsActive)
            .Select(x => new { x.FunctionalLocationId, x.Code, x.Plant, x.Area, x.Unit })
            .ToListAsync();

        var map = new Dictionary<string, List<FunctionalLocationLookupEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var key = row.Code?.Trim() ?? "";
            if (key.Length == 0) continue;

            if (!map.TryGetValue(key, out var list))
            {
                list = new List<FunctionalLocationLookupEntry>();
                map[key] = list;
            }

            list.Add(new FunctionalLocationLookupEntry { FunctionalLocationId = row.FunctionalLocationId, Plant = row.Plant, Area = row.Area, Unit = row.Unit });
        }

        return map;
    }

    // ==============================================================================
    // Review / Query
    // ==============================================================================

    public async Task<EquipmentFunctionalLocationAssignmentBatchSummaryDto?> GetBatchSummaryAsync(int batchId)
    {
        var batch = await _context.EquipmentFunctionalLocationAssignmentBatches
            .Include(x => x.ImportedBy)
            .FirstOrDefaultAsync(x => x.EquipmentFunctionalLocationAssignmentBatchId == batchId);

        if (batch == null) return null;

        var rows = await _context.EquipmentFunctionalLocationAssignmentRows
            .Where(x => x.EquipmentFunctionalLocationAssignmentBatchId == batchId)
            .Select(x => x.Status)
            .ToListAsync();

        return BuildSummary(batch, rows);
    }

    public async Task<List<EquipmentFunctionalLocationAssignmentBatchSummaryDto>> GetRecentBatchesAsync()
    {
        var batches = await _context.EquipmentFunctionalLocationAssignmentBatches
            .Include(x => x.ImportedBy)
            .OrderByDescending(x => x.ImportDate)
            .Take(20)
            .ToListAsync();

        var result = new List<EquipmentFunctionalLocationAssignmentBatchSummaryDto>();
        foreach (var batch in batches)
        {
            var rows = await _context.EquipmentFunctionalLocationAssignmentRows
                .Where(x => x.EquipmentFunctionalLocationAssignmentBatchId == batch.EquipmentFunctionalLocationAssignmentBatchId)
                .Select(x => x.Status)
                .ToListAsync();

            result.Add(BuildSummary(batch, rows));
        }

        return result;
    }

    private static EquipmentFunctionalLocationAssignmentBatchSummaryDto BuildSummary(EquipmentFunctionalLocationAssignmentBatch batch, List<string> statuses) => new()
    {
        EquipmentFunctionalLocationAssignmentBatchId = batch.EquipmentFunctionalLocationAssignmentBatchId,
        FileName = batch.FileName,
        ImportDate = batch.ImportDate,
        Status = batch.Status,
        ImportedByName = batch.ImportedBy != null ? $"{batch.ImportedBy.FirstName} {batch.ImportedBy.LastName}" : null,
        TotalRows = statuses.Count,
        ExactCount = statuses.Count(x => x == "Exact"),
        AlreadyAssignedCount = statuses.Count(x => x == "AlreadyAssigned"),
        NoChangeCount = statuses.Count(x => x == "NoChange"),
        UnresolvedCount = statuses.Count(x => x is "UnresolvedEquipment" or "UnresolvedFunctionalLocation"),
        ConflictCount = statuses.Count(x => x is "ConflictEquipment" or "ConflictFunctionalLocation"),
        ValidationErrorCount = statuses.Count(x => x == "ValidationError")
    };

    // طبق بند «Import Size and Performance»: صفحه‌بندی واقعی سمت-SQL روی ردیف‌های همین Batch -
    // نه بارگذاری همه‌ی ردیف‌ها و صفحه‌بندی در حافظه.
    public async Task<EquipmentFunctionalLocationAssignmentBatchDto?> GetBatchAsync(int batchId, int page, int pageSize, string? statusFilter)
    {
        var batch = await _context.EquipmentFunctionalLocationAssignmentBatches
            .Include(x => x.ImportedBy)
            .FirstOrDefaultAsync(x => x.EquipmentFunctionalLocationAssignmentBatchId == batchId);

        if (batch == null) return null;

        var allStatuses = await _context.EquipmentFunctionalLocationAssignmentRows
            .Where(x => x.EquipmentFunctionalLocationAssignmentBatchId == batchId)
            .Select(x => x.Status)
            .ToListAsync();

        var query = _context.EquipmentFunctionalLocationAssignmentRows
            .Where(x => x.EquipmentFunctionalLocationAssignmentBatchId == batchId);

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

        // Breadcrumb فقط برای همین صفحه‌ی جاری از ردیف‌ها Resolve می‌شود - نه کل Batch.
        var flIds = pageRows.SelectMany(x => new[] { x.ResolvedFunctionalLocationId, x.PreviousFunctionalLocationId })
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();

        var breadcrumbs = await BuildBreadcrumbsAsync(flIds);

        var equipmentIds = pageRows.Where(x => x.ResolvedEquipmentId.HasValue).Select(x => x.ResolvedEquipmentId!.Value).Distinct().ToList();
        var descriptionsById = await _context.Equipments
            .Where(x => equipmentIds.Contains(x.EquipmentId))
            .Select(x => new { x.EquipmentId, x.Description })
            .ToDictionaryAsync(x => x.EquipmentId, x => x.Description);

        var rowDtos = pageRows.Select(r =>
        {
            var payload = JsonSerializer.Deserialize<EquipmentFunctionalLocationAssignmentRowPayload>(r.PayloadJson, JsonOptions);

            return new EquipmentFunctionalLocationAssignmentRowDto
            {
                EquipmentFunctionalLocationAssignmentRowId = r.EquipmentFunctionalLocationAssignmentRowId,
                RowNumber = r.RowNumber,
                SourceRowNumber = r.SourceRowNumber,
                EquipmentTag = payload?.EquipmentTagRaw ?? "",
                FunctionalLocationCode = payload?.FunctionalLocationCodeRaw ?? "",
                ResolvedEquipmentId = r.ResolvedEquipmentId,
                EquipmentDescription = r.ResolvedEquipmentId.HasValue && descriptionsById.TryGetValue(r.ResolvedEquipmentId.Value, out var d) ? d : null,
                ResolvedFunctionalLocationId = r.ResolvedFunctionalLocationId,
                CurrentFunctionalLocationBreadcrumb = r.PreviousFunctionalLocationId.HasValue ? breadcrumbs.GetValueOrDefault(r.PreviousFunctionalLocationId.Value) : null,
                ProposedFunctionalLocationBreadcrumb = r.ResolvedFunctionalLocationId.HasValue ? breadcrumbs.GetValueOrDefault(r.ResolvedFunctionalLocationId.Value) : null,
                ValidationStatus = r.ValidationStatus,
                ValidationMessage = r.ValidationMessage,
                IncludeInCommit = r.IncludeInCommit,
                Status = r.Status,
                ReplaceExistingAssignment = r.ReplaceExistingAssignment,
                CommittedAssignment = r.CommittedAssignment
            };
        }).ToList();

        var summary = BuildSummary(batch, allStatuses);

        return new EquipmentFunctionalLocationAssignmentBatchDto
        {
            EquipmentFunctionalLocationAssignmentBatchId = summary.EquipmentFunctionalLocationAssignmentBatchId,
            FileName = summary.FileName,
            ImportDate = summary.ImportDate,
            Status = summary.Status,
            ImportedByName = summary.ImportedByName,
            TotalRows = summary.TotalRows,
            ExactCount = summary.ExactCount,
            AlreadyAssignedCount = summary.AlreadyAssignedCount,
            NoChangeCount = summary.NoChangeCount,
            UnresolvedCount = summary.UnresolvedCount,
            ConflictCount = summary.ConflictCount,
            ValidationErrorCount = summary.ValidationErrorCount,
            Rows = new PagedResult<EquipmentFunctionalLocationAssignmentRowDto>
            {
                Items = rowDtos,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalCount = totalCount
            }
        };
    }

    // طبق همون منطق IFunctionalLocationService.GetBreadcrumbAsync ("Plant / Area / .../ Code")
    // ولی برای چند شناسه هم‌زمان، از یک بارگذاری کامل درخت (جدول کوچک) - نه N فراخوانی جدا.
    private async Task<Dictionary<int, string>> BuildBreadcrumbsAsync(List<int> ids)
    {
        var result = new Dictionary<int, string>();
        if (ids.Count == 0) return result;

        var all = await _context.FunctionalLocations
            .Select(x => new { x.FunctionalLocationId, x.Code, x.ParentId })
            .ToListAsync();
        var byId = all.ToDictionary(x => x.FunctionalLocationId);

        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var node)) continue;

            var segments = new List<string> { node.Code };
            var cursor = node.ParentId;
            var guard = 0;
            while (cursor.HasValue && guard++ < 20)
            {
                if (!byId.TryGetValue(cursor.Value, out var parent)) break;
                segments.Insert(0, parent.Code);
                cursor = parent.ParentId;
            }

            result[id] = string.Join(" / ", segments);
        }

        return result;
    }

    // ==============================================================================
    // Approve / Commit / Discard
    // ==============================================================================

    public async Task SetRowIncludeAsync(int rowId, bool include)
    {
        var row = await _context.EquipmentFunctionalLocationAssignmentRows.FirstOrDefaultAsync(x => x.EquipmentFunctionalLocationAssignmentRowId == rowId);
        if (row == null) return;

        // طبق بند «Do not commit invalid rows»: هرگز نمی‌توان یک ردیف Unresolved/Conflict/
        // ValidationError را با روشن‌کردن این پرچم به Commit اضافه کرد - این وضعیت‌ها فقط با
        // بازبینی/اصلاح فایل منبع و Import دوباره قابل حل‌اند.
        if (include && row.Status is "UnresolvedEquipment" or "UnresolvedFunctionalLocation" or "ConflictEquipment" or "ConflictFunctionalLocation" or "ValidationError")
            return;

        row.IncludeInCommit = include;
        await _context.SaveChangesAsync();
    }

    public async Task SetReplaceExistingAssignmentAsync(int rowId, bool replace)
    {
        var row = await _context.EquipmentFunctionalLocationAssignmentRows.FirstOrDefaultAsync(x => x.EquipmentFunctionalLocationAssignmentRowId == rowId);
        if (row == null) return;

        if (row.Status != "AlreadyAssigned") return;

        row.ReplaceExistingAssignment = replace;
        row.IncludeInCommit = replace;
        await _context.SaveChangesAsync();
    }

    public async Task<EquipmentFunctionalLocationAssignmentCommitResultDto> CommitBatchAsync(int batchId)
    {
        var batch = await _context.EquipmentFunctionalLocationAssignmentBatches
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.EquipmentFunctionalLocationAssignmentBatchId == batchId);

        if (batch == null) throw new InvalidOperationException("Batch یافت نشد.");

        var result = new EquipmentFunctionalLocationAssignmentCommitResultDto();

        // طبق بند «Transaction Safety»: بدون foreach { SaveChanges() } - یک Transaction واحد
        // برای کل Commit، دقیقاً مثل الگوی موجود FunctionalLocationImportService/
        // EquipmentImportService (که خودشان صریحاً از یک Transaction دیتابیسی اضافه استفاده
        // نمی‌کنند چون EF ChangeTracker همه‌ی تغییرات این Context را در یک SaveChanges واحد
        // اتمی می‌کند؛ این‌جا هم همون قرارداد رعایت شده - یک SaveChangesAsync در پایان، نه به‌ازای هر ردیف).
        var committableRows = batch.Rows.Where(x => x.IncludeInCommit).ToList();

        if (committableRows.Count > 0)
        {
            var equipmentIds = committableRows.Where(x => x.ResolvedEquipmentId.HasValue).Select(x => x.ResolvedEquipmentId!.Value).Distinct().ToList();
            var equipmentById = await _context.Equipments
                .Where(x => equipmentIds.Contains(x.EquipmentId))
                .ToDictionaryAsync(x => x.EquipmentId);

            foreach (var row in committableRows)
            {
                if (row.Status is "UnresolvedEquipment" or "UnresolvedFunctionalLocation" or "ConflictEquipment" or "ConflictFunctionalLocation" or "ValidationError")
                {
                    // محافظت دفاعی - این ردیف‌ها هرگز نباید IncludeInCommit=true داشته باشند
                    // (SetRowIncludeAsync هم همینو رد می‌کند)، ولی این‌جا هم دوباره رد می‌شوند.
                    CountRejected(row, result);
                    continue;
                }

                if (!row.ResolvedEquipmentId.HasValue || !row.ResolvedFunctionalLocationId.HasValue ||
                    !equipmentById.TryGetValue(row.ResolvedEquipmentId.Value, out var equipment))
                {
                    result.Unresolved++;
                    continue;
                }

                // طبق بند «Idempotency»/«Existing Assignment Protection»: وضعیت واقعی تجهیز در
                // لحظه‌ی Commit دوباره بررسی می‌شود - نه فقط عکس فوری زمان Staging.
                if (equipment.FunctionalLocationId == row.ResolvedFunctionalLocationId)
                {
                    result.Skipped++; // از قبل همین بود - چیزی برای تغییر نیست
                    continue;
                }

                if (equipment.FunctionalLocationId.HasValue && !row.ReplaceExistingAssignment)
                {
                    result.Skipped++; // از آخرین Staging یک تخصیص دیگر ثبت شده و کاربر صریحاً اجازه‌ی جایگزینی نداده
                    continue;
                }

                equipment.FunctionalLocationId = row.ResolvedFunctionalLocationId;
                row.CommittedAssignment = true;
                result.Assigned++;
            }
        }

        foreach (var row in batch.Rows.Where(x => !x.IncludeInCommit))
            CountRejected(row, result);

        batch.Status = "Committed";

        await _context.SaveChangesAsync();

        return result;
    }

    private static void CountRejected(EquipmentFunctionalLocationAssignmentRow row, EquipmentFunctionalLocationAssignmentCommitResultDto result)
    {
        switch (row.Status)
        {
            case "UnresolvedEquipment" or "UnresolvedFunctionalLocation": result.Unresolved++; break;
            case "ConflictEquipment" or "ConflictFunctionalLocation": result.Conflicts++; break;
            case "ValidationError": result.ValidationErrors++; break;
            default: result.Skipped++; break; // NoChange یا AlreadyAssigned که کاربر تایید نکرد یا Exact که خودش خاموش کرد
        }
    }

    public async Task DiscardBatchAsync(int batchId)
    {
        var batch = await _context.EquipmentFunctionalLocationAssignmentBatches
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.EquipmentFunctionalLocationAssignmentBatchId == batchId);

        if (batch == null) return;

        _context.EquipmentFunctionalLocationAssignmentRows.RemoveRange(batch.Rows);
        _context.EquipmentFunctionalLocationAssignmentBatches.Remove(batch);

        await _context.SaveChangesAsync();
    }

    // ==============================================================================
    // Excel Header Parsing (عیناً همون الگوی FunctionalLocationImportService)
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
                if (h.Contains("tag") || h.Contains("functionallocation") || h == "code" || h == "flcode")
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
