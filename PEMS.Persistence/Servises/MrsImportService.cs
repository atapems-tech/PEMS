using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
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

    public async Task<MrsImportBatchDetailDto?> GetBatchDetailAsync(int batchId, string? matchStatusFilter = null)
    {
        var batch = await _context.MrsImportBatches.FirstOrDefaultAsync(x => x.MrsImportBatchId == batchId);
        if (batch == null) return null;

        var query = _context.MrsImportRows.Include(x => x.MatchedMaterialItem).Include(x => x.Metadata)
            .Where(x => x.MrsImportBatchId == batchId).AsQueryable();

        if (!string.IsNullOrWhiteSpace(matchStatusFilter)) query = query.Where(x => x.MatchStatus == matchStatusFilter);

        var rows = await query.OrderBy(x => x.MrsImportRowId).Take(500).ToListAsync();

        var summaries = await GetBatchesAsync();
        var summary = summaries.First(x => x.MrsImportBatchId == batchId);

        return new MrsImportBatchDetailDto
        {
            Batch = summary,
            Rows = rows.Select(x => new MrsImportRowDto
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
        };
    }

    public async Task<(bool Success, string? Error, int Committed, int Skipped)> CommitAsync(
        int batchId, List<int> rowIds, int? reviewedById)
    {
        var batch = await _context.MrsImportBatches.FirstOrDefaultAsync(x => x.MrsImportBatchId == batchId);
        if (batch == null) return (false, "Batch پیدا نشد.", 0, 0);

        var rows = await _context.MrsImportRows
            .Where(x => x.MrsImportBatchId == batchId && rowIds.Contains(x.MrsImportRowId) && x.ReviewStatus == "Pending")
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

        int committed = 0, skipped = 0;

        foreach (var row in rows)
        {
            if (row.MatchStatus == "Invalid") { skipped++; continue; }

            int materialItemId;

            if (row.MatchStatus == "MatchedExisting" && row.MatchedMaterialItemId.HasValue)
            {
                materialItemId = row.MatchedMaterialItemId.Value;
            }
            else if (!string.IsNullOrWhiteSpace(row.NormalizedDescription)
                && createdInThisCommit.TryGetValue(row.NormalizedDescription, out var existingNewId))
            {
                materialItemId = existingNewId;
            }
            else
            {
                var uom = uoms.FirstOrDefault(u => !string.IsNullOrWhiteSpace(row.RawUom)
                    && (u.Code.Equals(row.RawUom, StringComparison.OrdinalIgnoreCase)
                        || u.Name.Equals(row.RawUom, StringComparison.OrdinalIgnoreCase)));
                var discipline = DisciplineMatcher.Match(disciplines, row.RawDiscipline);

                var materialCode = !string.IsNullOrWhiteSpace(row.RawItemCode) && !usedMaterialCodes.Contains(row.RawItemCode.Trim())
                    ? row.RawItemCode!.Trim()
                    : $"MRS-{row.ResolvedWarehouseCode ?? "GEN"}-{row.MrsImportRowId}";
                usedMaterialCodes.Add(materialCode);

                // طبق سند Metadata Extraction بخش ۳ + کامنت طراحی خودِ MaterialItem.cs («Manufacturer
                // و Supplier یکی نیستند - Supplier به Vendor واقعی وصل می‌شود»): قبلاً این‌جا
                // RawSupplier به‌اشتباه در Manufacturer (رشته‌ی آزاد) می‌نشست - نه در VendorId
                // (که از قبل روی MaterialItem وجود داشت ولی استفاده نمی‌شد). حالا Vendor واقعی
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

                // usedMaterialCodes یک اسنپ‌شات در ابتدای Commit است - اگر هم‌زمان یک Commit
                // دیگر (یا دو کلیک/دو تب کاربر) دقیقاً همین کد را زودتر درج کرده باشد، Unique
                // Index دیتابیس (IX_MaterialItems_MaterialCode) جلوی درج تکراری را می‌گیرد؛
                // به‌جای کرش کل عملیات Commit روی همین یک ردیف، کد جایگزین امن تولید و
                // دوباره تلاش می‌شود.
                const int maxCodeAttempts = 5;
                for (var attempt = 1; ; attempt++)
                {
                    await _context.MaterialItems.AddAsync(newItem);
                    try
                    {
                        await _context.SaveChangesAsync();
                        break;
                    }
                    catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt < maxCodeAttempts)
                    {
                        _context.Entry(newItem).State = EntityState.Detached;
                        materialCode = $"MRS-{row.ResolvedWarehouseCode ?? "GEN"}-{row.MrsImportRowId}-{attempt}";
                        usedMaterialCodes.Add(materialCode);
                        newItem.MaterialCode = materialCode;
                    }
                }

                materialItemId = newItem.MaterialItemId;
                if (!string.IsNullOrWhiteSpace(row.NormalizedDescription))
                    createdInThisCommit[row.NormalizedDescription] = materialItemId;
            }

            InventoryTransaction? lastTransaction = null;

            if (row.ResolvedWarehouseId.HasValue)
            {
                var stock = await _context.MaterialStocks.FirstOrDefaultAsync(x =>
                    x.MaterialItemId == materialItemId && x.WarehouseId == row.ResolvedWarehouseId.Value && x.StorageLocationId == null);

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
                    await _context.MaterialStocks.AddAsync(stock);
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
                    await _context.InventoryTransactions.AddAsync(lastTransaction);
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
                    await _context.InventoryTransactions.AddAsync(lastTransaction);
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

            await _context.SaveChangesAsync();
            if (lastTransaction != null) row.ResultInventoryTransactionId = lastTransaction.InventoryTransactionId;

            committed++;
        }

        var remainingPending = await _context.MrsImportRows.CountAsync(x => x.MrsImportBatchId == batchId && x.ReviewStatus == "Pending");
        batch.Status = remainingPending == 0 ? "Committed" : "InReview";

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
        var rows = await _context.MrsImportRows
            .Where(x => x.MrsImportBatchId == batchId && rowIds.Contains(x.MrsImportRowId) && x.ReviewStatus == "Pending")
            .ToListAsync();

        foreach (var row in rows)
        {
            row.ReviewStatus = "Rejected";
            row.ReviewedById = reviewedById;
            row.ReviewedDate = DateTime.Now;
        }

        await _context.SaveChangesAsync();
        return (true, null);
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
