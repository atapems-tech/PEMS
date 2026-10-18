using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// موتور Import فایل VDR واقعی سازمان (فرمت "پهن": ستون‌های ثابت مدرک + بلوک‌های تکراری
/// REV0..REVn که با تکرار ستون "Vendor Transmittal NO." شناسایی می‌شوند + ستون‌های خلاصه‌ی انتهایی
/// که Import نمی‌شوند چون سیستم خودش این اعداد را زنده محاسبه می‌کند).
///
/// طبق الزام "Approval Before Final Storage": این سرویس فقط در VDRImportBatch/VDRImportRow
/// (Staging) می‌نویسد. تبدیل به VDRItem/VDRSubmission واقعی فقط در CommitBatchAsync اتفاق می‌افتد.
/// </summary>
public class VDRImportService : IVDRImportService
{
    private readonly PEMSDbContext _context;
    private readonly ILogger<VDRImportService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public VDRImportService(
        PEMSDbContext context,
        ILogger<VDRImportService> logger)
    {
        _context = context;
        _logger = logger;
    }


    // =========================
    // 1) STAGE (خواندن اکسل و ذخیره‌ی Staging - بدون تغییر در جداول اصلی)
    // =========================

    public async Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int projectId, int? importedById)
    {
        var batch = new VDRImportBatch
        {
            FileName = fileName,
            ProjectId = projectId,
            ImportedById = importedById,
            ImportDate = DateTime.Now,
            Status = "Staged"
        };

        await _context.VDRImportBatches.AddAsync(batch);
        await _context.SaveChangesAsync();

        using var workbook = new XLWorkbook(fileStream);

        // شیت اصلی VDR معمولاً اولین شیت یا شیتی با "VDR" در نام است
        var sheet = workbook.Worksheets.FirstOrDefault(s => s.Name.ToUpper().Contains("VDR"))
            ?? workbook.Worksheets.First();

        // ردیف ۳ (Row 3) هدر واقعی است؛ ردیف ۱ و ۲ عنوان‌های گروه‌بندی‌شده هستند (مثل "REV 0")
        const int headerRow = 3;
        const int firstDataRow = 4;

        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

        var headers = new List<string>();
        for (int c = 1; c <= lastCol; c++)
            headers.Add(NormalizeHeader(sheet.Cell(headerRow, c).GetString()));

        // مرزهای هر بلوک رویژن: هر ستونی که با "vendortransmittal" شروع می‌شود، شروع یک بلوک جدید است
        var blockStarts = new List<int>();
        for (int i = 0; i < headers.Count; i++)
        {
            if (headers[i].StartsWith("vendortransmittal"))
                blockStarts.Add(i);
        }

        // اولین رخداد ستون "Vendor Transmittal No." متعلق به بخش ثابت (اطلاعات Client، نه بلوک رویژن) است؛
        // بلوک‌های واقعی رویژن از دومین رخداد به بعد شروع می‌شوند (تایید‌شده روی فایل واقعی: ستون ۱۷ ثابت، ۲۶ به بعد بلوک‌ها)
        if (blockStarts.Count > 0)
            blockStarts.RemoveAt(0);

        int rowsValid = 0, rowsWarning = 0, rowsError = 0, rowsDuplicate = 0, rowIndex = 0;

        // ستون "1st Issue Date Plan" همیشه بلافاصله قبل از شروع اولین بلوک رویژن قرار دارد
        var plannedDateCol = blockStarts.Count > 0 ? blockStarts[0] : 0;

        var existingDocNumbers = await _context.VDRItems
            .Where(x => x.ProjectId == projectId)
            .Select(x => x.DocumentNumber.ToLower())
            .ToListAsync();

        for (int r = firstDataRow; r <= lastRow; r++)
        {
            var itemCell = sheet.Cell(r, 1).GetString();

            if (string.IsNullOrWhiteSpace(itemCell))
                continue; // ردیف خالی (پایان داده یا جداکننده)

            rowIndex++;

            try
            {
                var payload = new VDRImportRowPayload
                {
                    SequenceNo = ParseInt(sheet.Cell(r, 1).GetString()) ?? rowIndex,
                    DisciplineCodeRaw = TrimOrNull(sheet.Cell(r, 2).GetString()),
                    MRNumber = TrimOrNull(sheet.Cell(r, 3).GetString()),
                    CommercialExpeditorName = TrimOrNull(sheet.Cell(r, 4).GetString()),
                    EngineeringExpeditorName = TrimOrNull(sheet.Cell(r, 5).GetString()),
                    VendorName = TrimOrNull(sheet.Cell(r, 6).GetString()) ?? "",
                    FolderNoOnFtp = TrimOrNull(sheet.Cell(r, 7).GetString()),
                    PoNumber = TrimOrNull(sheet.Cell(r, 8).GetString()),
                    PackageDescription = TrimOrNull(sheet.Cell(r, 9).GetString()),
                    DocumentTypeCode = TrimOrNull(sheet.Cell(r, 11).GetString()) ?? "",
                    DocumentNumber = TrimOrNull(sheet.Cell(r, 12).GetString()) ?? "",
                    DocumentDescription = TrimOrNull(sheet.Cell(r, 13).GetString()),
                    ResponsiblePartyCode = TrimOrNull(sheet.Cell(r, 14).GetString()),
                    DocumentClassRaw = TrimOrNull(sheet.Cell(r, 15).GetString()),
                    WrapUpStatusImported = TrimOrNull(sheet.Cell(r, 17).GetString()),
                    PlannedFirstIssueDate = ParseDate(sheet.Cell(r, plannedDateCol).GetString())
                };

                // بلوک‌های رویژن
                for (int b = 0; b < blockStarts.Count; b++)
                {
                    var startCol = blockStarts[b] + 1; // 1-based
                    var endCol = (b + 1 < blockStarts.Count) ? blockStarts[b + 1] : headers.Count;

                    var sub = ExtractSubmission(sheet, r, headers, startCol, endCol, b);

                    if (sub != null)
                        payload.Submissions.Add(sub);
                }

                // طبق خطای واقعی گزارش‌شده (DbUpdateException روی Commit): وقتی شماره‌ی رویژن
                // یک بلوک از ستون "Rev." خالی باشد، به شماره‌ی بلوک (0،1،2...) برمی‌گردد - این
                // می‌تواند با یک بلوک دیگر که رویژن صریح دارد تصادفاً برخورد کند (هر دو رویژن ۱
                // شوند مثلاً)، که با Index یکتای جدید (VDRItemId+RevisionNo) در لحظه‌ی Commit به
                // این خطای دیتابیس منجر می‌شود. این‌جا قبل از ثبت، تکراری‌ها را با اطلاعات
                // کامل‌تر ادغام می‌کنیم.
                if (payload.Submissions.Count > 1)
                {
                    payload.Submissions = payload.Submissions
                        .GroupBy(s => s.RevisionNo)
                        .Select(g => g.OrderByDescending(s =>
                                (s.VendorTransmittalNo != null ? 1 : 0) +
                                (s.VendorActualIssuedDate != null ? 1 : 0) +
                                (s.ReviewCategory != null ? 1 : 0))
                            .First())
                        .ToList();
                }

                var validation = Validate(payload);

                var isDuplicate = existingDocNumbers.Contains(payload.DocumentNumber.ToLower());

                if (isDuplicate) rowsDuplicate++;

                if (validation.Status == "Error") rowsError++;
                else if (validation.Status == "Warning") rowsWarning++;
                else rowsValid++;

                var row = new VDRImportRow
                {
                    VDRImportBatchId = batch.VDRImportBatchId,
                    RowNumber = rowIndex,
                    DocumentNumber = payload.DocumentNumber,
                    VendorNameRaw = payload.VendorName,
                    PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
                    ValidationStatus = validation.Status,
                    ValidationMessage = validation.Message,
                    IsDuplicate = isDuplicate,
                    IncludeInCommit = validation.Status != "Error"
                };

                await _context.VDRImportRows.AddAsync(row);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "خطا در پارس کردن ردیف {Row} از فایل VDR", r);

                rowsError++;

                await _context.VDRImportRows.AddAsync(new VDRImportRow
                {
                    VDRImportBatchId = batch.VDRImportBatchId,
                    RowNumber = rowIndex,
                    DocumentNumber = "",
                    PayloadJson = "{}",
                    ValidationStatus = "Error",
                    ValidationMessage = "خطای پارس کردن ردیف: " + ex.Message,
                    IncludeInCommit = false
                });
            }
        }

        batch.RowsTotal = rowIndex;
        batch.RowsValid = rowsValid;
        batch.RowsWarning = rowsWarning;
        batch.RowsError = rowsError;
        batch.RowsDuplicate = rowsDuplicate;

        await _context.SaveChangesAsync();

        return batch.VDRImportBatchId;
    }


    private static VDRImportSubmissionPayload? ExtractSubmission(
        IXLWorksheet sheet, int row, List<string> headers, int startCol, int endCol, int blockIndex)
    {
        string? transmittalNo = null;
        string? clientTransmittalNo = null;
        DateTime? actualDate = null;
        string? poi = null;
        string? commentSheetNo = null;
        DateTime? commentDate = null;
        string? status = null;
        string? replySheetNo = null;
        DateTime? replySheetDate = null;
        int? revNo = null;

        bool foundStatus = false;

        for (int c = startCol; c <= endCol && c <= headers.Count; c++)
        {
            var h = headers[c - 1];
            var raw = sheet.Cell(row, c).GetString();

            if (string.IsNullOrWhiteSpace(raw)) continue;

            if (transmittalNo == null && h.StartsWith("vendortransmittal"))
                transmittalNo = raw.Trim();

            else if (clientTransmittalNo == null && h.StartsWith("clienttransmittal"))
                clientTransmittalNo = raw.Trim();

            else if (actualDate == null && h.Contains("vendoractualissueddate"))
                actualDate = ParseDate(raw);

            else if (revNo == null && (h == "rev." || h == "rev" || h.StartsWith("rev.")))
                revNo = ParseInt(raw);

            else if (poi == null && (h == "poi" || h == "poi."))
                poi = raw.Trim().ToUpper();

            else if (commentSheetNo == null && !h.Contains("date") && (h.Contains("cs.no") || h.Contains("commentsheetno") || h.Contains("commentsheet") || h.Contains("csno") || h.Contains("reviewsheetno")))
                commentSheetNo = raw.Trim();

            else if (commentDate == null && (h.Contains("actualissuedateofeng") || h.Contains("commentsheetdate")))
                commentDate = ParseDate(raw);

            else if (!foundStatus && (h == "status" || h.StartsWith("status.")))
            {
                status = NormalizeStatusCode(raw);
                foundStatus = true;
            }

            else if (replySheetNo == null && h.Contains("replysheetno") && !h.Contains("date"))
                replySheetNo = raw.Trim();

            else if (replySheetDate == null && h.Contains("replysheet") && h.Contains("date"))
                replySheetDate = ParseDate(raw);
        }

        // اگر هیچ ارسالی در این چرخه ثبت نشده (نه شماره ترانسمیتال، نه تاریخ)، این بلوک را نادیده بگیر
        if (transmittalNo == null && actualDate == null)
            return null;

        // طبق درخواست: در برخی فایل‌های واقعی، ستون جداگانه‌ای برای "Comment Sheet No" وجود
        // ندارد و همان "Client Transmittal No." این نقش را هم بازی می‌کند - اگر ستون مخصوص
        // Comment Sheet پیدا نشد، از Client Transmittal به‌عنوان جایگزین استفاده می‌شود تا
        // هیچ داده‌ای از فایل اکسل بلااستفاده نماند
        if (commentSheetNo == null && clientTransmittalNo != null)
            commentSheetNo = clientTransmittalNo;

        return new VDRImportSubmissionPayload
        {
            RevisionNo = revNo ?? blockIndex,
            VendorTransmittalNo = transmittalNo,
            ClientTransmittalNo = clientTransmittalNo,
            VendorActualIssuedDate = actualDate,
            PurposeOfIssueCode = poi,
            ClientCommentSheetNo = commentSheetNo,
            ClientCommentDate = commentDate,
            ReviewCategory = status,
            ReplySheetNo = replySheetNo,
            ReplySheetDate = replySheetDate
        };
    }


    // =========================
    // 2) بازیابی و بازبینی Batch
    // =========================

    public async Task<VDRImportBatchDto?> GetBatchAsync(int batchId)
    {
        var batch = await _context.VDRImportBatches
            .Include(x => x.Project)
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.VDRImportBatchId == batchId);

        if (batch == null) return null;

        return new VDRImportBatchDto
        {
            VDRImportBatchId = batch.VDRImportBatchId,
            FileName = batch.FileName,
            ProjectId = batch.ProjectId,
            ProjectName = batch.Project?.ProjectName ?? "",
            ImportDate = batch.ImportDate,
            RowsTotal = batch.RowsTotal,
            RowsValid = batch.RowsValid,
            RowsWarning = batch.RowsWarning,
            RowsError = batch.RowsError,
            RowsDuplicate = batch.RowsDuplicate,
            Status = batch.Status,
            Rows = batch.Rows.OrderBy(x => x.RowNumber).Select(x => new VDRImportRowDto
            {
                VDRImportRowId = x.VDRImportRowId,
                RowNumber = x.RowNumber,
                DocumentNumber = x.DocumentNumber,
                VendorNameRaw = x.VendorNameRaw,
                ValidationStatus = x.ValidationStatus,
                ValidationMessage = x.ValidationMessage,
                IsDuplicate = x.IsDuplicate,
                IncludeInCommit = x.IncludeInCommit,
                Payload = string.IsNullOrEmpty(x.PayloadJson)
                    ? null
                    : JsonSerializer.Deserialize<VDRImportRowPayload>(x.PayloadJson, JsonOptions)
            }).ToList()
        };
    }

    public async Task<List<VDRImportBatchDto>> GetRecentBatchesAsync()
    {
        var batches = await _context.VDRImportBatches
            .Include(x => x.Project)
            .OrderByDescending(x => x.ImportDate)
            .Take(20)
            .ToListAsync();

        return batches.Select(batch => new VDRImportBatchDto
        {
            VDRImportBatchId = batch.VDRImportBatchId,
            FileName = batch.FileName,
            ProjectId = batch.ProjectId,
            ProjectName = batch.Project?.ProjectName ?? "",
            ImportDate = batch.ImportDate,
            RowsTotal = batch.RowsTotal,
            RowsValid = batch.RowsValid,
            RowsWarning = batch.RowsWarning,
            RowsError = batch.RowsError,
            RowsDuplicate = batch.RowsDuplicate,
            Status = batch.Status
        }).ToList();
    }

    public async Task SetRowIncludeAsync(int rowId, bool include)
    {
        var row = await _context.VDRImportRows.FirstOrDefaultAsync(x => x.VDRImportRowId == rowId);
        if (row == null) return;
        row.IncludeInCommit = include;
        await _context.SaveChangesAsync();
    }


    // =========================
    // 3) COMMIT (تبدیل ردیف‌های تایید‌شده به داده‌ی واقعی)
    // =========================

    private const int CommitFlushBatchSize = 200;

    public async Task<(int created, int updated, int skipped)> CommitBatchAsync(int batchId)
    {
        var batch = await _context.VDRImportBatches
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.VDRImportBatchId == batchId);

        if (batch == null) throw new Exception("Batch یافت نشد");

        int created = 0, updated = 0, skipped = 0;

        var allDisciplines = await _context.Disciplines.Where(x => x.IsActive).ToListAsync();

        // ==============================================================================
        // همون تکنیک MDR Import: پیش‌بارگذاری همه‌ی داده‌ی مرجع (Vendor، PO، MR، Transmittal،
        // VDRItem موجود) یک‌جا قبل از حلقه - به‌جای Query جداگانه برای هر ردیف از طریق
        // Linker Service ها (که برای فایل‌های بزرگ یعنی چند ساعت طول می‌کشید). SaveChanges
        // هم به‌جای هر ردیف، هر ۲۰۰ ردیف یک‌بار زده می‌شود.
        // ==============================================================================
        // طبق ممیزی «VDR Import — Duplicate Vendor Key Exception»: هویت کسب‌وکاری Vendor در این
        // پروژه از قبل همون چیزیه که IVendorService.FindOrCreateByNameAsync پیاده می‌کنه - نام،
        // بعد از Trim + بی‌تفاوت به بزرگی/کوچکی حروف. هیچ ایندکس یکتایی روی Vendor.Name/VendorCode
        // در دیتابیس وجود نداره (VendorConfiguration فقط Precision روی RiskScore می‌گذارد)، و
        // Vendor های تکراری واقعی (چندین ردیف با همون نام‌نرمال‌شده) از قبل در PEMSDB وجود دارند -
        // مستقیماً ToDictionaryAsync با همین Key با ArgumentException («An item with the same key
        // has already been added») روی همین رکوردهای موجود کرش می‌کرد، نه به‌خاطر چیزی در همین فایل
        // Import. اینجا به‌جای فرض یکتایی، رکوردها را با همون Key گروه‌بندی می‌کنیم: گروه با دقیقاً
        // یک عضو → همون Vendor بدون ابهام استفاده می‌شود (رفتار قبلی)؛ گروه با بیش از یک عضو →
        // «تعارض داده‌ی مرجع Vendor» صریح است - هیچ عضوی خودکار انتخاب نمی‌شود (نه First، نه
        // FirstOrDefault)، ردیف‌های Import که به این نام ارجاع می‌دهند Skip می‌شوند و دلیل دقیق در
        // همون ValidationStatus/ValidationMessage موجود هر ردیف (همون مکانیزم فعلی صفحه‌ی Review)
        // ثبت می‌شود تا کاربر بتواند تعارض را در ماژول Vendor برطرف کند و دوباره Import بزند.
        var vendorGroups = (await _context.Vendors.ToListAsync())
            .GroupBy(x => x.Name.Trim().ToLower())
            .ToDictionary(g => g.Key, g => g.ToList());

        var ambiguousVendorKeys = new HashSet<string>(
            vendorGroups.Where(g => g.Value.Count > 1).Select(g => g.Key));

        var vendorCache = vendorGroups
            .Where(g => g.Value.Count == 1)
            .ToDictionary(g => g.Key, g => g.Value[0]);

        var poCache = await _context.PurchaseOrders
            .Where(x => x.ProjectId == batch.ProjectId)
            .ToDictionaryAsync(x => x.PoNumber.ToLower(), x => x);

        var mrCache = await _context.MaterialRequisitions
            .Where(x => x.ProjectId == batch.ProjectId)
            .ToDictionaryAsync(x => x.MRNumber.ToLower(), x => x);

        var transmittalCache = await _context.Transmittals
            .Where(x => x.ProjectId == batch.ProjectId)
            .ToDictionaryAsync(x => x.TransmittalNo.ToLower(), x => x);

        var transmittalIds = transmittalCache.Values.Select(t => t.TransmittalId).ToList();
        var existingTransmittalLinks = transmittalIds.Count == 0
            ? new HashSet<(int, int)>()
            : new HashSet<(int, int)>((await _context.TransmittalItems
                .Where(x => transmittalIds.Contains(x.TransmittalId))
                .Select(x => new { x.TransmittalId, x.DocumentId })
                .ToListAsync())
                .Select(x => (x.TransmittalId, x.DocumentId)));

        var existingVdrItems = await _context.VDRItems
            .Include(x => x.Submissions)
            .Where(x => x.ProjectId == batch.ProjectId)
            .ToDictionaryAsync(x => x.DocumentNumber.ToLower(), x => x);

        var pendingRowItemPairs = new List<(VDRImportRow Row, VDRItem Item)>();
        var rowsSinceFlush = 0;

        foreach (var row in batch.Rows.Where(x => x.IncludeInCommit))
        {
            var payload = JsonSerializer.Deserialize<VDRImportRowPayload>(row.PayloadJson, JsonOptions);

            if (payload == null || string.IsNullOrWhiteSpace(payload.DocumentNumber))
            {
                skipped++;
                continue;
            }

            // --- Vendor (کش‌شده) ---
            var vendorName = string.IsNullOrWhiteSpace(payload.VendorName) ? "Unknown Vendor" : payload.VendorName!;
            var vendorKey = vendorName.Trim().ToLower();

            // تعارض داده‌ی مرجع: چند Vendor با همین نام‌نرمال‌شده در دیتابیس موجود است - انتخاب
            // خودکار یکی از آن‌ها (با First/FirstOrDefault) می‌تواند مدرک را به Vendor غلط وصل کند؛
            // چون Vendor روی VDRItem الزامی است (VendorId غیر-Nullable)، کل ردیف بدون تصمیم انسانی
            // قابل ثبت نیست و Skip می‌شود - دقیقاً همون رفتار «Skip» که برای شماره مدرک خالی بالاتر
            // اعمال شده، نه یک مسیر خطای جدید.
            if (ambiguousVendorKeys.Contains(vendorKey))
            {
                skipped++;
                row.ValidationStatus = "Error";
                row.ValidationMessage =
                    $"تعارض داده‌ی مرجع Vendor: {vendorGroups[vendorKey].Count} رکورد Vendor با نام «{vendorName.Trim()}» " +
                    "در سامانه موجود است (شناسه‌ها: " + string.Join(", ", vendorGroups[vendorKey].Select(v => v.VendorId)) + "). " +
                    "این ردیف به‌صورت خودکار به هیچ‌کدام وصل نشد تا سند به Vendor نادرست متصل نشود. " +
                    "ابتدا رکوردهای تکراری Vendor را در ماژول Vendors برطرف کنید، سپس این ردیف را دوباره Import کنید.";
                continue;
            }

            if (!vendorCache.TryGetValue(vendorKey, out var vendor))
            {
                vendor = new Vendor { Name = vendorName.Trim(), RiskLevel = "Unknown", IsActive = true, CreatedDate = DateTime.Now };
                _context.Vendors.Add(vendor);
                vendorCache[vendorKey] = vendor;
            }

            // --- PO (کش‌شده) ---
            PurchaseOrder? po = null;
            if (!string.IsNullOrWhiteSpace(payload.PoNumber))
            {
                var poKey = payload.PoNumber!.Trim().ToLower();
                if (!poCache.TryGetValue(poKey, out po))
                {
                    po = new PurchaseOrder
                    {
                        PoNumber = payload.PoNumber!.Trim(), ProjectId = batch.ProjectId, Vendor = vendor,
                        Status = "Issued", CreatedDate = DateTime.Now
                    };
                    _context.PurchaseOrders.Add(po);
                    poCache[poKey] = po;
                }
            }

            int? disciplineId = null;
            bool isMultiDiscipline = string.Equals(payload.DisciplineCodeRaw, "PM", StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(payload.DisciplineCodeRaw) && !isMultiDiscipline)
            {
                var discipline = DisciplineMatcher.Match(allDisciplines, payload.DisciplineCodeRaw);
                disciplineId = discipline?.DisciplineId;
            }

            var fallbackDisciplineId = disciplineId ?? allDisciplines.FirstOrDefault()?.DisciplineId;

            // --- MR (کش‌شده) ---
            MaterialRequisition? mr = null;
            if (!string.IsNullOrWhiteSpace(payload.MRNumber))
            {
                var mrKey = payload.MRNumber!.Trim().ToLower();
                if (!mrCache.TryGetValue(mrKey, out mr))
                {
                    if (fallbackDisciplineId == null)
                    {
                        _logger.LogWarning(
                            "Skipping MaterialRequisition creation for VDR row {RowId}: no valid Discipline match for code '{DisciplineCode}' and no fallback discipline exists.",
                            row.VDRImportRowId,
                            payload.DisciplineCodeRaw ?? "<empty>");
                    }
                    else
                    {
                        mr = new MaterialRequisition
                        {
                            MRNumber = payload.MRNumber!.Trim(),
                            ProjectId = batch.ProjectId,
                            DisciplineId = fallbackDisciplineId.Value,
                            Description = $"Auto-created from VDR import (MR/MRQ No: {payload.MRNumber!.Trim()})",
                            Status = "Draft",
                            IsActive = true,
                            CreatedDate = DateTime.Now
                        };
                        _context.MaterialRequisitions.Add(mr);
                        mrCache[mrKey] = mr;
                    }
                }
            }

            var docKey = payload.DocumentNumber.ToLower();
            var isNew = !existingVdrItems.TryGetValue(docKey, out var existing);

            int.TryParse(payload.DocumentClassRaw, out var parsedClass);

            VDRItem item;

            if (!isNew)
            {
                item = existing!;
                item.ModifiedDate = DateTime.Now;
                updated++;
            }
            else
            {
                item = new VDRItem { ProjectId = batch.ProjectId, CreatedDate = DateTime.Now };
                _context.VDRItems.Add(item);
                existingVdrItems[docKey] = item;
                created++;
            }

            item.SequenceNo = payload.SequenceNo;
            item.DisciplineId = fallbackDisciplineId;
            item.DisciplineCodeRaw = payload.DisciplineCodeRaw;
            item.IsMultiDiscipline = isMultiDiscipline;
            item.MRNumber = payload.MRNumber;
            item.MR = mr;
            item.CommercialExpeditorName = payload.CommercialExpeditorName;
            item.EngineeringExpeditorName = payload.EngineeringExpeditorName;
            item.Vendor = vendor;
            item.FolderNoOnFtp = payload.FolderNoOnFtp;
            item.Po = po;
            item.PackageDescription = payload.PackageDescription;
            item.DocumentTypeCode = payload.DocumentTypeCode;
            item.DocumentNumber = payload.DocumentNumber;
            item.DocumentDescription = payload.DocumentDescription;
            item.ResponsiblePartyCode = payload.ResponsiblePartyCode;
            item.DocumentClass = parsedClass > 0 ? parsedClass : null;
            item.DocumentClassRaw = payload.DocumentClassRaw;
            item.WrapUpStatusImported = payload.WrapUpStatusImported;
            item.PlannedFirstIssueDate = payload.PlannedFirstIssueDate;
            item.IsActive = true;

            item.DocumentKind = "Vendor";
            item.ProjectId = batch.ProjectId;
            item.DocumentNo = payload.DocumentNumber;
            item.Title = string.IsNullOrWhiteSpace(payload.DocumentDescription) ? payload.DocumentNumber : payload.DocumentDescription!;
            item.DocumentType = payload.DocumentTypeCode;
            item.PlannedDate = payload.PlannedFirstIssueDate;

            // جایگزینی کامل تاریخچه‌ی Submission برای جلوگیری از رکوردهای تکراری در Import مجدد
            if (!isNew && existing!.Submissions.Any())
                _context.VDRSubmissions.RemoveRange(existing.Submissions);

            string? latestCategory = null;
            var seenRevisions = new HashSet<int>();

            foreach (var sub in payload.Submissions.OrderBy(s => s.RevisionNo))
            {
                // طبق همون رفع باگ کشف‌شده در MDR: اگر یک شماره رویژن در همین Payload دو بار
                // آمده باشد، Duplicate Key می‌خورد - این‌جا هم همان محافظت اعمال می‌شود
                if (!seenRevisions.Add(sub.RevisionNo)) continue;

                var vdrSubmission = new VDRSubmission
                {
                    VDRItem = item,
                    RevisionNo = sub.RevisionNo,
                    VendorTransmittalNo = sub.VendorTransmittalNo,
                    ClientTransmittalNo = sub.ClientTransmittalNo,
                    VendorActualIssuedDate = sub.VendorActualIssuedDate,
                    PurposeOfIssueCode = sub.PurposeOfIssueCode,
                    ClientCommentSheetNo = sub.ClientCommentSheetNo,
                    ClientCommentDate = sub.ClientCommentDate,
                    ReviewCategory = sub.ReviewCategory,
                    ReplySheetNo = sub.ReplySheetNo,
                    ReplySheetDate = sub.ReplySheetDate,
                    CreatedDate = DateTime.Now
                };

                _context.VDRSubmissions.Add(vdrSubmission);

                // --- Transmittal (کش‌شده) ---
                if (!string.IsNullOrWhiteSpace(sub.ClientTransmittalNo))
                {
                    var transKey = sub.ClientTransmittalNo!.Trim().ToLower();
                    if (!transmittalCache.TryGetValue(transKey, out var transmittal))
                    {
                        transmittal = new Transmittal
                        {
                            TransmittalNo = sub.ClientTransmittalNo!.Trim(),
                            ProjectId = batch.ProjectId,
                            From = "VDR Import",
                            To = "-",
                            Purpose = "For Information",
                            Status = "Sent",
                            TransmittalDate = DateTime.Now,
                            IsActive = true,
                            CreatedDate = DateTime.Now
                        };
                        _context.Transmittals.Add(transmittal);
                        transmittalCache[transKey] = transmittal;
                    }

                    vdrSubmission.ClientTransmittal = transmittal;

                    var alreadyLinked = transmittal.TransmittalId != 0 && item.DocumentId != 0
                        && existingTransmittalLinks.Contains((transmittal.TransmittalId, item.DocumentId));

                    if (!alreadyLinked)
                    {
                        _context.TransmittalItems.Add(new TransmittalItem
                        {
                            Transmittal = transmittal,
                            Document = item,
                            RevisionNo = sub.RevisionNo.ToString("00")
                        });
                    }
                }

                if (!string.IsNullOrWhiteSpace(sub.ReviewCategory))
                    latestCategory = sub.ReviewCategory;
            }

            item.WorkflowStatus = VDRService.ComputeWorkflowStatus(latestCategory, item.PlannedFirstIssueDate);
            item.Status = item.WorkflowStatus;
            item.CommentStatus = latestCategory;

            var latestSubmission = payload.Submissions.OrderBy(s => s.RevisionNo).LastOrDefault();
            if (latestSubmission?.VendorActualIssuedDate != null)
                item.ActualDate = latestSubmission.VendorActualIssuedDate;
            if (latestSubmission != null)
                item.RevisionNo = latestSubmission.RevisionNo.ToString();

            pendingRowItemPairs.Add((row, item));
            rowsSinceFlush++;

            if (rowsSinceFlush >= CommitFlushBatchSize)
            {
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    // طبق رفتار قبلی: یک برخورد نباید کل دسته را متوقف کند - ولی چون حالا
                    // Batch شده، اگر برخورد افتاد، همین دسته دوباره یکی‌یکی تلاش می‌شود
                    _logger.LogWarning(ex, "خطا در ذخیره‌ی دسته‌ای - تلاش مجدد ردیف به ردیف برای این دسته");

                    foreach (var (_, pendingItem) in pendingRowItemPairs)
                        _context.Entry(pendingItem).State = EntityState.Detached;

                    throw;
                }

                foreach (var (pendingRow, pendingItem) in pendingRowItemPairs)
                    pendingRow.CreatedVDRItemId = pendingItem.DocumentId;

                pendingRowItemPairs.Clear();
                rowsSinceFlush = 0;
            }
        }

        await _context.SaveChangesAsync();

        foreach (var (pendingRow, pendingItem) in pendingRowItemPairs)
            pendingRow.CreatedVDRItemId = pendingItem.DocumentId;

        batch.Status = "Committed";
        batch.CommittedDate = DateTime.Now;

        await _context.SaveChangesAsync();

        return (created, updated, skipped);
    }

    public async Task DiscardBatchAsync(int batchId)
    {
        var batch = await _context.VDRImportBatches
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.VDRImportBatchId == batchId);

        if (batch == null) return;

        _context.VDRImportRows.RemoveRange(batch.Rows);
        _context.VDRImportBatches.Remove(batch);

        await _context.SaveChangesAsync();
    }


    // =========================
    // Helpers
    // =========================

    private static string NormalizeHeader(string raw)
    {
        return raw
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "")
            .Replace(".", "")
            .Replace("\n", "")
            .Replace("\r", "")
            .Replace("_", "")
            .Replace("‐", "");
    }

    private static string? TrimOrNull(string raw)
    {
        var t = raw?.Trim();
        return string.IsNullOrWhiteSpace(t) ? null : t;
    }

    private static int? ParseInt(string raw)
    {
        return int.TryParse(raw?.Trim(), out var v) ? v : null;
    }

    private static DateTime? ParseDate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTime.TryParse(raw, out var d) ? d : null;
    }

    private static string? NormalizeStatusCode(string raw)
    {
        var t = raw.Trim().ToUpper().TrimEnd('*');

        return t switch
        {
            "AP" or "NC" or "AC" or "CO" or "RE" => t,
            "DELETED" => "DELETED",
            _ => t
        };
    }

    private record ValidationResult(string Status, string? Message);

    private static ValidationResult Validate(VDRImportRowPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.DocumentNumber))
            return new ValidationResult("Error", "شماره مدرک (Document No.) خالی است");

        if (string.IsNullOrWhiteSpace(payload.VendorName))
            return new ValidationResult("Warning", "نام Vendor خالی است");

        if (!string.IsNullOrWhiteSpace(payload.DocumentClassRaw) &&
            !new[] { "1", "2", "3" }.Contains(payload.DocumentClassRaw.Trim()))
            return new ValidationResult("Warning", $"مقدار CLASS نامعتبر است: '{payload.DocumentClassRaw}'");

        return new ValidationResult("Valid", null);
    }
}
