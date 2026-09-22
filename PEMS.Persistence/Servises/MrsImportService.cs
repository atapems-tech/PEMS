using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Shared.Search;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق سند Inventory بخش ۲۰-۲۱ و ممیزی ۹ فایل واقعی MRS: این فایل‌ها دفتر تراکنش هستند نه
/// کاتالوگ متریال (نرخ تکرار ۲۳-۳۹٪ در فایل‌های بزرگ) - به همین دلیل هر ردیف قبل از تبدیل به
/// MaterialItem باید سه‌لایه تطبیق بخورد (دقیق روی ItemCode → دقیق روی شرح نرمال‌شده →
/// پیشوندی، با DocumentNumberMatcher که همین الگو را برای شماره‌ی مدرک اثبات کرده) وگرنه
/// همان متریال بارها ساخته می‌شد.
/// </summary>
public class MrsImportService : IMrsImportService
{
    private readonly PEMSDbContext _context;
    private readonly IVendorService _vendorService;

    private static readonly Dictionary<string, string[]> FieldAliases = new()
    {
        ["MrsNo"] = new[] { "mrsno", "mrs" },
        ["ItemCode"] = new[] { "itemcode", "codegoods", "coderahkaran" },
        // "PACK NO." نرمال می‌شود به "packno" - با "packingno" (نام قدیمی) یکی نیست
        ["PackingNo"] = new[] { "packingno", "packno" },
        ["PoNo"] = new[] { "pono", "p/ono" },
        ["Supplier"] = new[] { "suplyer", "supplier", "vendor" },
        ["Description"] = new[] { "description", "descriptionpackinglist" },
        ["Uom"] = new[] { "unt", "unit", "uom" },
        ["QtyReceived"] = new[] { "qtyrecived", "qtyreceived" },
        ["QtyIssued"] = new[] { "qtyissued" },
        ["Inventory"] = new[] { "inventory" },
        ["DateReceived"] = new[] { "daterecived", "datereceived" },
        ["DateIssued"] = new[] { "dateissued" },
        ["MivNo"] = new[] { "mivno" },
        ["MrvNo"] = new[] { "mrvno" },
        ["Location"] = new[] { "location" },
        ["Discipline"] = new[] { "deciplin", "discipline", "typeofmaterial", "matrial" },
        ["Size"] = new[] { "size", "size1", "size2", "size3" },
        ["EquipmentTag"] = new[] { "heat/tag/serno", "tag" },
    };

    public MrsImportService(PEMSDbContext context, IVendorService vendorService)
    {
        _context = context;
        _vendorService = vendorService;
    }

    public async Task<(bool Success, string? Error, int? BatchId)> StageFromExcelAsync(
        Stream fileStream, string fileName, string? sheetName, int? importedById, int? sourceAttachmentId = null)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(fileStream);
        }
        catch (Exception ex)
        {
            // فایل‌های واقعی گاهی Conditional Formatting یا Style خراب دارند که ClosedXML
            // نمی‌تواند Parse کند - به‌جای کرش کل درخواست، خطای واضح برمی‌گردد.
            return (false, $"باز کردن فایل Excel با خطا مواجه شد (ممکنه فرمت‌بندی شرطی یا ساختار فایل مشکل داشته باشه): {ex.Message}", null);
        }
        using var _ = workbook;

        var sheet = !string.IsNullOrWhiteSpace(sheetName)
            ? workbook.Worksheets.FirstOrDefault(x => x.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
            : workbook.Worksheets.OrderByDescending(x => x.LastRowUsed()?.RowNumber() ?? 0).FirstOrDefault();

        if (sheet == null) return (false, "شیت موردنظر در فایل پیدا نشد.", null);

        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastCol == 0 || lastRow < 2) return (false, "شیت انتخاب‌شده داده‌ای ندارد.", null);

        var headerRow = FindHeaderRow(sheet);
        var headerMap = BuildHeaderMap(sheet, headerRow, lastCol);

        var descCol = ResolveFirstAliasColumn(headerMap, FieldAliases["Description"]);
        if (descCol == null) return (false, "ستون شرح متریال (Description) در این شیت پیدا نشد.", null);

        // طبق سند Metadata Extraction بخش ۱: متن دقیق سرستون هر ستون واقعی (نه نسخه‌ی Normalize
        // شده) - برای این‌که هر ستون (شناخته‌شده یا نه) بعداً به‌ازای هر ردیف به Metadata تبدیل شود.
        var originalHeadersByColumn = new Dictionary<int, string>();
        for (int c = 1; c <= lastCol; c++)
        {
            var raw = sheet.Cell(headerRow, c).GetString().Trim();
            if (!string.IsNullOrEmpty(raw)) originalHeadersByColumn[c] = raw;
        }

        var batch = new MrsImportBatch
        {
            SourceFileName = fileName,
            SourceSheet = sheet.Name,
            ImportedById = importedById,
            ImportDate = DateTime.Now,
            Status = "Staged",
            SourceAttachmentId = sourceAttachmentId
        };
        // طبق سند Metadata Extraction بخش ۸ (Transaction Safety): قبلاً Batch با یک SaveChanges
        // جدا زودتر Commit می‌شد، بعد ردیف‌ها با SaveChanges دوم - اگر ساخت ردیف‌ها وسط راه
        // Exception می‌داد، یک Batch خالی/یتیم (بدون هیچ ردیفی) در دیتابیس می‌ماند. حالا batch
        // فقط از طریق Navigation Property (row.Batch) به ردیف‌ها وصل می‌شود و همه با هم در یک
        // SaveChangesAsync در انتها Commit می‌شوند - یا همه با هم، یا هیچ‌کدام.
        await _context.MrsImportBatches.AddAsync(batch);

        var existingItems = await _context.MaterialItems
            .Select(x => new { x.MaterialItemId, x.MaterialCode, x.Description }).ToListAsync();
        var warehouses = await _context.Warehouses.Where(x => x.IsActive).ToListAsync();

        var rows = new List<MrsImportRow>();

        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            var description = sheet.Cell(r, descCol.Value).GetString().Trim();
            var mrsNo = GetByAliases(sheet, r, headerMap, FieldAliases["MrsNo"]);

            if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(mrsNo))
                continue; // ردیف کاملاً خالی

            var row = new MrsImportRow
            {
                RawMrsNo = mrsNo,
                RawItemCode = GetByAliases(sheet, r, headerMap, FieldAliases["ItemCode"]),
                RawPackingNo = GetByAliases(sheet, r, headerMap, FieldAliases["PackingNo"]),
                RawPoNo = GetByAliases(sheet, r, headerMap, FieldAliases["PoNo"]),
                RawSupplier = GetByAliases(sheet, r, headerMap, FieldAliases["Supplier"]),
                RawDescription = description,
                RawUom = GetByAliases(sheet, r, headerMap, FieldAliases["Uom"]),
                RawQtyReceived = ParseDecimal(GetByAliases(sheet, r, headerMap, FieldAliases["QtyReceived"])),
                RawQtyIssued = ParseDecimal(GetByAliases(sheet, r, headerMap, FieldAliases["QtyIssued"])),
                RawInventory = ParseDecimal(GetByAliases(sheet, r, headerMap, FieldAliases["Inventory"])),
                RawDateReceived = ParseDate(GetByAliases(sheet, r, headerMap, FieldAliases["DateReceived"])),
                RawDateIssued = ParseDate(GetByAliases(sheet, r, headerMap, FieldAliases["DateIssued"])),
                RawMivNo = GetByAliases(sheet, r, headerMap, FieldAliases["MivNo"]),
                RawMrvNo = GetByAliases(sheet, r, headerMap, FieldAliases["MrvNo"]),
                RawLocation = GetByAliases(sheet, r, headerMap, FieldAliases["Location"]),
                RawDiscipline = GetByAliases(sheet, r, headerMap, FieldAliases["Discipline"]),
                RawSize = GetByAliases(sheet, r, headerMap, FieldAliases["Size"]),
                RawEquipmentTag = GetByAliases(sheet, r, headerMap, FieldAliases["EquipmentTag"]),
                Batch = batch,
                CreatedDate = DateTime.Now
            };

            // طبق سند Metadata Extraction بخش ۱-۴: هر ستون واقعی این ردیف (شناخته‌شده یا نه) یک
            // ردیف Metadata مستقل می‌گیرد - حتی ستون‌هایی که به فیلد Typed بالا هم نگاشت شدند،
            // چون سند صراحتاً می‌خواهد "Every detected Excel column/header must be persisted as
            // metadata"، نه فقط ستون‌های ناشناخته.
            foreach (var (colIdx, originalHeader) in originalHeadersByColumn)
            {
                var cellValue = sheet.Cell(r, colIdx).GetString().Trim();
                if (string.IsNullOrEmpty(cellValue)) continue;

                row.Metadata.Add(new MrsImportRowMetadata
                {
                    ColumnIndex = colIdx,
                    OriginalHeader = originalHeader,
                    CanonicalField = MrsMetadataCanonicalMap.Resolve(originalHeader),
                    Value = cellValue,
                    CreatedDate = DateTime.Now
                });
            }

            row.NormalizedDescription = NormalizeDescription(row.RawDescription);

            var warehouseToken = ExtractWarehouseToken(row.RawMrsNo);
            var matchedWarehouse = warehouseToken == null ? null
                : warehouses.FirstOrDefault(w => w.Code.Replace("WH-", "", StringComparison.OrdinalIgnoreCase)
                    .Equals(warehouseToken, StringComparison.OrdinalIgnoreCase));
            row.ResolvedWarehouseCode = warehouseToken;
            row.ResolvedWarehouseId = matchedWarehouse?.WarehouseId;

            if (string.IsNullOrWhiteSpace(row.RawDescription))
            {
                row.MatchStatus = "Invalid";
                row.ErrorMessage = "شرح متریال خالی است.";
            }
            else
            {
                var itemCode = row.RawItemCode?.Trim();
                var exactMatch = !string.IsNullOrWhiteSpace(itemCode)
                    ? existingItems.FirstOrDefault(x => x.MaterialCode.Equals(itemCode, StringComparison.OrdinalIgnoreCase))
                    : null;

                if (exactMatch != null)
                {
                    row.MatchStatus = "MatchedExisting";
                    row.MatchedMaterialItemId = exactMatch.MaterialItemId;
                }
                else
                {
                    var fuzzyMatch = DocumentNumberMatcher.FindBestMatch(
                        existingItems, row.NormalizedDescription, x => NormalizeDescription(x.Description));

                    if (fuzzyMatch != null)
                    {
                        row.MatchStatus = "MatchedExisting";
                        row.MatchedMaterialItemId = fuzzyMatch.MaterialItemId;
                    }
                    else
                    {
                        row.MatchStatus = "New";
                    }
                }
            }

            rows.Add(row);
        }

        // تطبیق تکرار داخل همین Batch: بین ردیف‌هایی که به هیچ متریال موجودی وصل نشدند
        // (هنوز New هستند)، اولین نمونه از هر شرح نرمال‌شده New می‌ماند، بقیه DuplicateInBatch.
        var seenNormalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Where(x => x.MatchStatus == "New"))
        {
            if (string.IsNullOrWhiteSpace(row.NormalizedDescription)) continue;
            if (!seenNormalized.Add(row.NormalizedDescription))
                row.MatchStatus = "DuplicateInBatch";
        }

        batch.TotalRows = rows.Count;
        await _context.MrsImportRows.AddRangeAsync(rows);
        await _context.SaveChangesAsync();

        return (true, null, batch.MrsImportBatchId);
    }

    public async Task<List<MrsImportBatchSummaryDto>> GetBatchesAsync()
    {
        var batches = await _context.MrsImportBatches.OrderByDescending(x => x.ImportDate).ToListAsync();
        var result = new List<MrsImportBatchSummaryDto>();

        foreach (var b in batches)
        {
            var rowStats = await _context.MrsImportRows.Where(x => x.MrsImportBatchId == b.MrsImportBatchId)
                .GroupBy(x => 1)
                .Select(g => new
                {
                    New = g.Count(x => x.MatchStatus == "New"),
                    Matched = g.Count(x => x.MatchStatus == "MatchedExisting"),
                    Duplicate = g.Count(x => x.MatchStatus == "DuplicateInBatch"),
                    Invalid = g.Count(x => x.MatchStatus == "Invalid"),
                    Approved = g.Count(x => x.ReviewStatus == "Approved")
                }).FirstOrDefaultAsync();

            result.Add(new MrsImportBatchSummaryDto
            {
                MrsImportBatchId = b.MrsImportBatchId,
                SourceFileName = b.SourceFileName,
                SourceSheet = b.SourceSheet,
                Status = b.Status,
                TotalRows = b.TotalRows,
                NewCount = rowStats?.New ?? 0,
                MatchedCount = rowStats?.Matched ?? 0,
                DuplicateCount = rowStats?.Duplicate ?? 0,
                InvalidCount = rowStats?.Invalid ?? 0,
                ApprovedCount = rowStats?.Approved ?? 0,
                ImportDate = b.ImportDate,
                SourceAttachmentId = b.SourceAttachmentId,
                // طبق بررسی: EngineeringDocumentsController.DownloadAttachment یک اکشن عمومی است
                // (فقط AttachmentId+IsActive چک می‌کند، به DocumentId خاصی وابسته نیست) - همان
                // مسیر دانلود موجود استفاده می‌شود، نه Route جدید تکراری.
                SourceFileDownloadUrl = b.SourceAttachmentId.HasValue ? $"/EngineeringDocuments/DownloadAttachment/{b.SourceAttachmentId}" : null
            });
        }

        return result;
    }

    public async Task<MrsImportBatchDetailDto?> GetBatchDetailAsync(int batchId, string? matchStatusFilter = null, int page = 1, int pageSize = 50)
    {
        var batch = await _context.MrsImportBatches.FirstOrDefaultAsync(x => x.MrsImportBatchId == batchId);
        if (batch == null) return null;

        var query = _context.MrsImportRows.Include(x => x.MatchedMaterialItem).Include(x => x.Metadata)
            .Where(x => x.MrsImportBatchId == batchId).AsQueryable();

        if (!string.IsNullOrWhiteSpace(matchStatusFilter)) query = query.Where(x => x.MatchStatus == matchStatusFilter);

        // طبق ممیزی «Global 500-Row Batch Limitation Remediation»: TotalCount از روی همون فیلتر
        // (نه کل Batch) محاسبه می‌شود - Take(500) قبلی حذف شد، اینجا صفحه‌بندی واقعی است، نه
        // یک سقف بی‌صدا؛ همه‌ی ردیف‌های یک Batch بزرگ، صرف‌نظر از تعداد، قابل‌دسترسی می‌مانند.
        var totalCount = await query.CountAsync();
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = DataGridPageSize.Normalize(pageSize);

        var rows = await query.OrderBy(x => x.MrsImportRowId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync();

        var summaries = await GetBatchesAsync();
        var summary = summaries.First(x => x.MrsImportBatchId == batchId);

        return new MrsImportBatchDetailDto
        {
            Batch = summary,
            Rows = new PagedResult<MrsImportRowDto>
            {
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalCount = totalCount,
                Items = rows.Select(x => new MrsImportRowDto
                {
                    MrsImportRowId = x.MrsImportRowId,
                    RawMrsNo = x.RawMrsNo,
                    RawItemCode = x.RawItemCode,
                    RawSupplier = x.RawSupplier,
                    RawPoNo = x.RawPoNo,
                    RawPackingNo = x.RawPackingNo,
                    RawDescription = x.RawDescription,
                    RawUom = x.RawUom,
                    RawQtyReceived = x.RawQtyReceived,
                    RawQtyIssued = x.RawQtyIssued,
                    RawInventory = x.RawInventory,
                    RawDateReceived = x.RawDateReceived,
                    RawMivNo = x.RawMivNo,
                    RawMrvNo = x.RawMrvNo,
                    RawLocation = x.RawLocation,
                    RawDiscipline = x.RawDiscipline,
                    RawSize = x.RawSize,
                    RawEquipmentTag = x.RawEquipmentTag,
                    ResolvedWarehouseCode = x.ResolvedWarehouseCode,
                    MatchStatus = x.MatchStatus,
                    MatchedMaterialCode = x.MatchedMaterialItem?.MaterialCode,
                    MatchedMaterialDescription = x.MatchedMaterialItem?.Description,
                    ReviewStatus = x.ReviewStatus,
                    ErrorMessage = x.ErrorMessage,
                    Metadata = x.Metadata
                        .OrderBy(m => m.ColumnIndex)
                        .Select(m => new MrsImportRowMetadataDto
                        {
                            OriginalHeader = m.OriginalHeader,
                            CanonicalField = m.CanonicalField,
                            Value = m.Value
                        }).ToList()
                }).ToList()
            }
        };
    }

    private static readonly string[] KnownMatchStatuses = { "New", "MatchedExisting", "DuplicateInBatch", "Invalid" };

    // باید با HasMaxLength(100) روی MaterialItem.MaterialCode در
    // PEMS.Persistence/Configurations/MaterialMasterConfigurations.cs هماهنگ بماند.
    private const int MaterialCodeMaxLength = 100;

    public Task<(bool Success, string? Error, int Committed, int Skipped)> CommitAsync(
        int batchId, List<int> rowIds, int? reviewedById)
        => CommitCoreAsync(batchId, reviewedById, x => rowIds.Contains(x.MrsImportRowId));

    // طبق ممیزی «PEMS Import Center — Global 500-Row Batch Limitation Remediation» بند ۳/۴/۶:
    // این متد کل ردیف‌های Pending یک دسته‌ی Match Status را در سراسر Batch پردازش می‌کند - نه
    // فقط ردیف‌های صفحه‌ی جاری. کاربر همچنان باید یک دسته‌ی مشخص (New/MatchedExisting/
    // DuplicateInBatch) را صریحاً انتخاب کند (بررسی انسانی حفظ می‌شود، طبق طراحی اصلی این
    // Import که در کامنت بالای فایل مستند است) - فقط دیوار مصنوعی ۵۰۰ ردیف حذف شده، نه قضاوت
    // ردیف‌به‌ردیف کاربر.
    public async Task<(bool Success, string? Error, int Committed, int Skipped)> CommitByStatusAsync(
        int batchId, string matchStatusFilter, int? reviewedById)
    {
        if (string.IsNullOrWhiteSpace(matchStatusFilter) || !KnownMatchStatuses.Contains(matchStatusFilter))
            return (false, "یک دسته‌ی Match Status مشخص (New / MatchedExisting / DuplicateInBatch) انتخاب کنید.", 0, 0);
        if (matchStatusFilter == "Invalid")
            return (false, "ردیف‌های Invalid قابل Commit نیستند - فقط قابل Reject هستند.", 0, 0);

        return await CommitCoreAsync(batchId, reviewedById, x => x.MatchStatus == matchStatusFilter);
    }

    private async Task<(bool Success, string? Error, int Committed, int Skipped)> CommitCoreAsync(
        int batchId, int? reviewedById, System.Linq.Expressions.Expression<Func<MrsImportRow, bool>> rowSelector)
    {
        var batch = await _context.MrsImportBatches.FirstOrDefaultAsync(x => x.MrsImportBatchId == batchId);
        if (batch == null) return (false, "Batch پیدا نشد.", 0, 0);

        // طبق ممیزی: قبلاً این‌جا Take(500) روی صفحه‌ی نمایشی بود که Commit را هم عملاً به همون
        // سقف محدود می‌کرد (چون Checkbox فقط برای ردیف‌های رندرشده وجود داشت). اینجا هیچ Take/Skip
        // ای روی مجموعه‌ی واقعی Commit نیست - کل ردیف‌های منطبق، در همین یک عملیات پردازش می‌شوند.
        var rows = await _context.MrsImportRows
            .Where(x => x.MrsImportBatchId == batchId && x.ReviewStatus == "Pending")
            .Where(rowSelector)
            .ToListAsync();

        var uoms = await _context.MaterialUOMs.Where(x => x.IsActive).ToListAsync();
        var disciplines = await _context.Disciplines.Where(x => x.IsActive).ToListAsync();

        // برای ردیف‌های New/DuplicateInBatch که در همین Commit با هم پردازش می‌شوند، تا
        // متریال یکسان دوبار ساخته نشود ولو کاربر آن‌ها را جدا انتخاب کرده باشد.
        var createdInThisCommit = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // طبق ممیزی: ITEM CODE در فایل‌های واقعی گاهی برای چند شرح مختلف تکرار شده (شناسه‌ی
        // پایدار نیست) - اگر همون کد قبلاً برای یک متریال دیگه در همین Commit یا در دیتابیس
        // استفاده شده، به‌جای کرش خوردن روی Unique Index، کد تولیدشده جایگزین می‌شود.
        var usedMaterialCodes = new HashSet<string>(
            await _context.MaterialItems.Select(x => x.MaterialCode).ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        // اندازه‌گیری واقعی نشان داد Commit روی SQL Server واقعی رشد نمایی داشت (۱۵۰۰ ردیف=۱۱ ثانیه،
        // ۵۰۰۰ ردیف=۱۲۴ ثانیه - نسبت ۱۱x برای ۳.۳x حجم، دقیقاً منطبق با رشد n^2). علت: کوئری بالا
        // (rows) به‌صورت پیش‌فرض همه‌ی n ردیف را در ChangeTracker ردیابی‌شده نگه می‌دارد، و چون batch
        // هم ردیابی‌شده و رابطه‌ی Collection به MrsImportRows دارد، هر SaveChangesAsync در همون بلوک
        // اول باید Relationship Fixup را روی کل n ردیف انجام دهد (نه فقط اندازه‌ی بلوک) - هزینه‌ای
        // که با مربع n رشد می‌کند. چون هر ردیف به‌صورت جداگانه و امن دوباره در حلقه‌ی زیر Attach
        // می‌شود، همین‌جا با خیال راحت از ردیابی خارج می‌شوند تا بلوک اول هم فقط اندازه‌ی بلوک را
        // ردیابی کند - نه کل Batch را.
        _context.ChangeTracker.Clear();

        int committed = 0, skipped = 0;

        // طبق ممیزی «Global 500-Row Batch Limitation Remediation» بند ۴/۶/۱۱ (Chunking داخلی
        // نامرئی به کاربر + شواهد واقعیِ کارایی، نه فرض): اندازه‌گیری واقعی (هم InMemory هم SQL
        // Server واقعی) نشان داد پردازش یک-SaveChangesAsync-به‌ازای-هر-ردیف برای Batch های بزرگ
        // (۱۵۰۰ ردیف) چند دقیقه طول می‌کشد - هزینه‌ی واقعی هزاران Round-trip شبکه‌ی جداگانه به SQL
        // Server، نه فقط رشد ChangeTracker. الان ردیف‌ها در بلوک‌های CommitChunkSize تایی پردازش
        // می‌شوند: درج MaterialItem های جدید یک‌جا (یک SaveChanges برای کل بلوک)، با بازگشت امن
        // به همون منطق یکی‌یکیِ قبلی (Retry روی برخورد Unique Index) فقط اگر همون یک SaveChanges
        // دسته‌ای برخورد کرد - نتیجه/تضمین‌های Commit کاملاً یکسان می‌ماند، فقط تعداد Round-trip
        // کم می‌شود.
        const int chunkSize = 100;

        foreach (var chunk in rows.Chunk(chunkSize))
        {
            var pendingNew = new List<(MrsImportRow Row, MaterialItem Item)>();
            var resolved = new List<(MrsImportRow Row, int MaterialItemId)>();
            // طبق ممیزی: اگر دو ردیف با همون شرح‌نرمال‌شده هر دو در همین بلوک تازه ساخته می‌شوند
            // (مثلاً یک ردیف New + برادرِ DuplicateInBatch آن، در یک Commit ترکیبی با هم انتخاب
            // شده باشند - همون سناریویی که createdInThisCommit اصلاً برایش طراحی شده بود)، دومی
            // باید صبر کند تا Item واقعی اولی بعد از Save ساخته شود - نه این‌که یک ID موقتِ ۰
            // بگیرد.
            var sameAsPendingInChunk = new List<(MrsImportRow Row, string NormalizedDescription)>();

            foreach (var row in chunk)
            {
                if (_context.Entry(row).State == EntityState.Detached)
                    _context.MrsImportRows.Attach(row);

                if (row.MatchStatus == "Invalid") { skipped++; continue; }

                if (row.MatchStatus == "MatchedExisting" && row.MatchedMaterialItemId.HasValue)
                {
                    resolved.Add((row, row.MatchedMaterialItemId.Value));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(row.NormalizedDescription)
                    && createdInThisCommit.TryGetValue(row.NormalizedDescription, out var existingNewId))
                {
                    resolved.Add((row, existingNewId));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(row.NormalizedDescription)
                    && pendingNew.Any(p => string.Equals(p.Row.NormalizedDescription, row.NormalizedDescription, StringComparison.OrdinalIgnoreCase)))
                {
                    sameAsPendingInChunk.Add((row, row.NormalizedDescription));
                    continue;
                }

                var uom = uoms.FirstOrDefault(u => !string.IsNullOrWhiteSpace(row.RawUom)
                    && (u.Code.Equals(row.RawUom, StringComparison.OrdinalIgnoreCase)
                        || u.Name.Equals(row.RawUom, StringComparison.OrdinalIgnoreCase)));
                var discipline = DisciplineMatcher.Match(disciplines, row.RawDiscipline);

                var materialCode = !string.IsNullOrWhiteSpace(row.RawItemCode) && !usedMaterialCodes.Contains(row.RawItemCode.Trim())
                    ? row.RawItemCode!.Trim()
                    : $"MRS-{row.ResolvedWarehouseCode ?? "GEN"}-{row.MrsImportRowId}";

                // طبق ممیزی «MaterialItems.Description String-Length Integrity» بخش ۳ (ممیزی کامل
                // نگاشت‌های رشته‌ای MRS→MaterialItems، نه فقط Description): MaterialCode بر خلاف
                // Description یک شناسه‌ی کسب‌وکار واقعی است (Unique Index دارد، طبیعتاً کوتاه) - سقف
                // ۱۰۰ کاراکتری‌اش یک قاعده‌ی واقعی می‌ماند، نه یک محدودیت تصادفی مثل Description.
                // داده‌ی واقعی MRS نشان داد ۲ ردیف واقعی Item Code بیش از ۱۰۰ کاراکتر دارند - اگر
                // بدون بررسی به SaveChanges می‌رسید، همون کلاس Exception (String or binary data would
                // be truncated) را روی ستون MaterialCode می‌داد. به‌جای Truncate یا crash، این‌جا
                // همون مکانیزم موجود ردیف Invalid/Skipped استفاده می‌شود - مقدار اصلی دست‌نخورده در
                // ErrorMessage/RawItemCode می‌ماند تا کاربر تصمیم بگیرد (اصلاح کد یا Reject).
                if (materialCode.Length > MaterialCodeMaxLength)
                {
                    row.MatchStatus = "Invalid";
                    row.ErrorMessage = $"کد متریال «{materialCode}» به {materialCode.Length} کاراکتر می‌رسد - بیش از سقف مجاز {MaterialCodeMaxLength} کاراکتری. این ردیف Commit نشد؛ کد را در فایل اصلاح کنید یا ردیف را Reject کنید.";
                    skipped++;
                    continue;
                }

                usedMaterialCodes.Add(materialCode);

                // طبق سند Metadata Extraction بخش ۳ + کامنت طراحی خودِ MaterialItem.cs («Manufacturer
                // و Supplier یکی نیستند - Supplier به Vendor واقعی وصل می‌شود»): Vendor واقعی
                // پیدا/ساخته می‌شود (همان IVendorService.FindOrCreateByNameAsync که در ماژول VDR
                // استفاده می‌شود)، نه فقط متن آزاد.
                int? vendorId = null;
                if (!string.IsNullOrWhiteSpace(row.RawSupplier))
                    vendorId = await _vendorService.FindOrCreateByNameAsync(row.RawSupplier);

                var newItem = new MaterialItem
                {
                    MaterialCode = materialCode,
                    Description = row.RawDescription!.Trim(),
                    UomId = uom?.MaterialUOMId,
                    DisciplineId = discipline?.DisciplineId,
                    VendorId = vendorId,
                    Size = row.RawSize,
                    IsStockItem = true,
                    IsActive = true,
                    CreatedDate = DateTime.Now,
                    CreatedBy = "MrsImport"
                };
                _context.MaterialItems.Add(newItem);
                pendingNew.Add((row, newItem));
            }

            if (pendingNew.Count > 0)
            {
                try
                {
                    await _context.SaveChangesAsync();
                    foreach (var (row, item) in pendingNew) resolved.Add((row, item.MaterialItemId));
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    // طبق ممیزی: برخورد Unique Index روی MaterialCode در کل بلوک نادر است (چون
                    // usedMaterialCodes از قبل تکراری‌های شناخته‌شده را رد می‌کند - این مسیر فقط
                    // برای رقابت هم‌زمان بین دو Commit مستقل رخ می‌دهد). به‌جای شکست کل بلوک، همون
                    // منطق ایمنِ یکی‌یکیِ قبلی (Retry با کد جایگزین) فقط برای همین بلوک اجرا می‌شود.
                    foreach (var (_, item) in pendingNew)
                        _context.Entry(item).State = EntityState.Detached;

                    foreach (var (row, item) in pendingNew)
                    {
                        const int maxCodeAttempts = 5;
                        for (var attempt = 1; ; attempt++)
                        {
                            _context.MaterialItems.Add(item);
                            try
                            {
                                await _context.SaveChangesAsync();
                                break;
                            }
                            catch (DbUpdateException retryEx) when (IsUniqueConstraintViolation(retryEx) && attempt < maxCodeAttempts)
                            {
                                _context.Entry(item).State = EntityState.Detached;
                                var altCode = $"MRS-{row.ResolvedWarehouseCode ?? "GEN"}-{row.MrsImportRowId}-{attempt}";
                                usedMaterialCodes.Add(altCode);
                                item.MaterialCode = altCode;
                            }
                        }
                        resolved.Add((row, item.MaterialItemId));
                    }
                }

                foreach (var (row, item) in pendingNew)
                    if (!string.IsNullOrWhiteSpace(row.NormalizedDescription))
                        createdInThisCommit[row.NormalizedDescription] = item.MaterialItemId;

                // ردیف‌های هم‌شرح که در همین بلوک منتظر مانده بودند، حالا به Item واقعیِ
                // هم‌بلوکی‌شان (نه یک ID موقت) وصل می‌شوند.
                foreach (var (row, desc) in sameAsPendingInChunk)
                    resolved.Add((row, createdInThisCommit[desc]));
            }

            // پیش‌بارگذاری یک‌جای موجودی‌های فعلی برای کل بلوک - به‌جای یک Query جدا برای هر ردیف
            var materialItemIdsInChunk = resolved.Select(r => r.MaterialItemId).Distinct().ToList();
            var existingStocks = materialItemIdsInChunk.Count == 0
                ? new List<MaterialStock>()
                : await _context.MaterialStocks
                    .Where(x => materialItemIdsInChunk.Contains(x.MaterialItemId) && x.StorageLocationId == null)
                    .ToListAsync();

            var pendingTransactions = new List<(MrsImportRow Row, InventoryTransaction Tx)>();

            foreach (var (row, materialItemId) in resolved)
            {
                InventoryTransaction? lastTransaction = null;

                if (row.ResolvedWarehouseId.HasValue)
                {
                    var stock = existingStocks.FirstOrDefault(x => x.MaterialItemId == materialItemId && x.WarehouseId == row.ResolvedWarehouseId.Value);

                    if (stock == null)
                    {
                        stock = new MaterialStock
                        {
                            MaterialItemId = materialItemId,
                            WarehouseId = row.ResolvedWarehouseId.Value,
                            OnHandQuantity = 0,
                            ReservedQuantity = 0,
                            IsActive = true,
                            CreatedDate = DateTime.Now
                        };
                        _context.MaterialStocks.Add(stock);
                        existingStocks.Add(stock);
                    }

                    if (row.RawQtyReceived is > 0)
                    {
                        lastTransaction = new InventoryTransaction
                        {
                            MaterialItemId = materialItemId,
                            WarehouseId = row.ResolvedWarehouseId.Value,
                            TransactionType = "Receipt",
                            Quantity = row.RawQtyReceived.Value,
                            ReferenceType = "MrsImport",
                            ReferenceId = batchId,
                            TransactionDate = row.RawDateReceived ?? batch.ImportDate,
                            Reason = $"MRS Import - {row.RawMrsNo}"
                        };
                        _context.InventoryTransactions.Add(lastTransaction);
                        stock.OnHandQuantity += row.RawQtyReceived.Value;
                    }

                    if (row.RawQtyIssued is > 0)
                    {
                        lastTransaction = new InventoryTransaction
                        {
                            MaterialItemId = materialItemId,
                            WarehouseId = row.ResolvedWarehouseId.Value,
                            TransactionType = "Issue",
                            Quantity = row.RawQtyIssued.Value,
                            ReferenceType = "MrsImport",
                            ReferenceId = batchId,
                            TransactionDate = row.RawDateIssued ?? batch.ImportDate,
                            Reason = $"MRS Import - {row.RawMrsNo}"
                        };
                        _context.InventoryTransactions.Add(lastTransaction);
                        stock.OnHandQuantity -= row.RawQtyIssued.Value;
                    }

                    stock.LastUpdated = DateTime.Now;
                }
                else
                {
                    // MaterialItem ساخته شد ولی چون انبار از روی MRS NO تشخیص داده نشد، هیچ
                    // MaterialStock/InventoryTransaction ای ثبت نشد - این‌جا شفاف اعلام می‌شود
                    // تا کاربر بی‌صدا فکر نکنه موجودی درست ثبت شده.
                    row.ErrorMessage = $"انبار از روی «{row.RawMrsNo}» تشخیص داده نشد - متریال ساخته شد ولی موجودی/تراکنشی ثبت نشد.";
                }

                row.ResultMaterialItemId = materialItemId;
                row.ReviewStatus = "Approved";
                row.ReviewedById = reviewedById;
                row.ReviewedDate = DateTime.Now;

                if (lastTransaction != null) pendingTransactions.Add((row, lastTransaction));
                committed++;
            }

            // یک Flush برای کل بلوک: وضعیت ردیف‌ها + Stock/Transaction های جدید
            await _context.SaveChangesAsync();

            // InventoryTransactionId فقط بعد از Save بالا (Identity) در دسترس است - یک Flush کوچک
            // دوم فقط همین فیلد را برای کل بلوک ثبت می‌کند.
            if (pendingTransactions.Count > 0)
            {
                foreach (var (row, tx) in pendingTransactions) row.ResultInventoryTransactionId = tx.InventoryTransactionId;
                await _context.SaveChangesAsync();
            }

            // پاک‌سازی دوره‌ای ChangeTracker به‌ازای هر بلوک - جلوی رشد نامحدود Entity های
            // ردیابی‌شده‌ی قبلی (Add/Save شده) را در Batch های خیلی بزرگ می‌گیرد.
            _context.ChangeTracker.Clear();
        }

        // batch ممکن است بالا Clear شده باشد (Detached) - دوباره واقعی و ردیابی‌شده خوانده می‌شود
        // تا تغییر Status واقعاً Persist شود.
        var freshBatch = await _context.MrsImportBatches.FirstAsync(x => x.MrsImportBatchId == batchId);
        var remainingPending = await _context.MrsImportRows.CountAsync(x => x.MrsImportBatchId == batchId && x.ReviewStatus == "Pending");
        freshBatch.Status = remainingPending == 0 ? "Committed" : "InReview";

        await _context.SaveChangesAsync();
        return (true, null, committed, skipped);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx
            && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }

    public async Task<(bool Success, string? Error)> RejectRowsAsync(int batchId, List<int> rowIds, int? reviewedById)
    {
        var (success, error, _) = await RejectCoreAsync(batchId, reviewedById, x => rowIds.Contains(x.MrsImportRowId));
        return (success, error);
    }

    // طبق همون ممیزی: نسخه‌ی سراسر-Batch از Reject - برای مثال رد کردن همه‌ی ردیف‌های Invalid
    // در یک فایل ۵۰۰۰ ردیفی، بدون نیاز به صفحه‌به‌صفحه Checkbox زدن.
    public async Task<(bool Success, string? Error, int Count)> RejectByStatusAsync(int batchId, string matchStatusFilter, int? reviewedById)
    {
        if (string.IsNullOrWhiteSpace(matchStatusFilter) || !KnownMatchStatuses.Contains(matchStatusFilter))
            return (false, "یک دسته‌ی Match Status مشخص انتخاب کنید.", 0);

        return await RejectCoreAsync(batchId, reviewedById, x => x.MatchStatus == matchStatusFilter);
    }

    private async Task<(bool Success, string? Error, int Count)> RejectCoreAsync(
        int batchId, int? reviewedById, System.Linq.Expressions.Expression<Func<MrsImportRow, bool>> rowSelector)
    {
        var rows = await _context.MrsImportRows
            .Where(x => x.MrsImportBatchId == batchId && x.ReviewStatus == "Pending")
            .Where(rowSelector)
            .ToListAsync();

        foreach (var row in rows)
        {
            row.ReviewStatus = "Rejected";
            row.ReviewedById = reviewedById;
            row.ReviewedDate = DateTime.Now;
        }

        await _context.SaveChangesAsync();
        return (true, null, rows.Count);
    }

    private static string NormalizeDescription(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var collapsed = string.Join(" ", raw.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return collapsed;
    }

    private static string? ExtractWarehouseToken(string? mrsNo)
    {
        if (string.IsNullOrWhiteSpace(mrsNo)) return null;
        var parts = mrsNo.Trim().ToUpperInvariant().Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;
        if (parts[0] == "WH" && parts.Length > 1) return parts[1];
        if (parts[0].All(char.IsLetter)) return parts[0];
        return null;
    }

    private static int FindHeaderRow(IXLWorksheet sheet, int maxRowsToCheck = 10)
    {
        for (int r = 1; r <= maxRowsToCheck; r++)
        {
            for (int c = 1; c <= 40; c++)
            {
                var cellVal = NormalizeHeader(sheet.Cell(r, c).GetString());
                if (FieldAliases["Description"].Contains(cellVal)) return r;
            }
        }
        return 1;
    }

    private static Dictionary<string, List<int>> BuildHeaderMap(IXLWorksheet sheet, int headerRow, int lastCol)
    {
        var map = new Dictionary<string, List<int>>();
        for (int c = 1; c <= lastCol; c++)
        {
            var h = NormalizeHeader(sheet.Cell(headerRow, c).GetString());
            if (string.IsNullOrEmpty(h)) continue;
            if (!map.TryGetValue(h, out var cols)) map[h] = cols = new List<int>();
            cols.Add(c);
        }
        return map;
    }

    private static int? ResolveFirstAliasColumn(Dictionary<string, List<int>> map, string[] aliases)
    {
        foreach (var alias in aliases)
            if (map.TryGetValue(alias, out var cols) && cols.Count > 0) return cols[0];
        return null;
    }

    private static string? GetByAliases(IXLWorksheet sheet, int row, Dictionary<string, List<int>> map, string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (map.TryGetValue(alias, out var columns))
            {
                foreach (var col in columns)
                {
                    var v = sheet.Cell(row, col).GetString();
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
            }
        }
        return null;
    }

    private static string NormalizeHeader(string raw)
    {
        return raw.Trim().ToLowerInvariant()
            .Replace(" ", "").Replace(".", "").Replace("\n", "").Replace("\r", "")
            .Replace("_", "").Replace("(", "").Replace(")", "");
    }

    private static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return decimal.TryParse(raw.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static DateTime? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();

        if (DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out var gregorian))
            return gregorian;

        // طبق ممیزی فایل‌های واقعی: بعضی تاریخ‌ها شمسی هستند (مثلاً 1401/05/12)
        var parts = t.Split('/', '-');
        if (parts.Length == 3 && int.TryParse(parts[0], out var y) && int.TryParse(parts[1], out var m) && int.TryParse(parts[2], out var d)
            && y is > 1300 and < 1500 && m is >= 1 and <= 12 && d is >= 1 and <= 31)
        {
            try
            {
                var pc = new PersianCalendar();
                return pc.ToDateTime(y, m, d, 0, 0, 0, 0);
            }
            catch { return null; }
        }

        return null;
    }
}
