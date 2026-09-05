using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class MDRImportService : IMDRImportService
{
    private readonly PEMSDbContext _context;
    private readonly ILogger<MDRImportService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly string[] SkipSheetKeywords = { "report", "annex", "sheet2" };

    // ==============================================================================
    // دیکشنری الیاس هدرها: هر فیلد منطقی می‌تواند چند نام هدر متفاوت داشته باشد که در
    // فایل‌های واقعی MDR (به‌خصوص وقتی مشاور پروژه عوض می‌شود) دیده شده‌اند. کلیدها و
    // مقادیر همگی از قبل با NormalizeHeader هم‌شکل شده‌اند (بدون فاصله/نقطه/زیرخط/پرانتز).
    // برای افزودن نام هدر جدید در آینده، کافی است به آرایه‌ی مربوطه یک مقدار اضافه شود -
    // نیازی به تغییر منطق پارس نیست.
    // ==============================================================================
    private static readonly Dictionary<string, string[]> FieldAliases = new()
    {
        ["DocumentNumber"] = new[] { "documentno", "documentnumber", "docno" },
        ["Title"] = new[] { "documenttitle", "description", "title" },
        ["DocumentType"] = new[] { "type", "doctype" },
        ["Discipline"] = new[] { "discipline", "disc" },
        ["Area"] = new[] { "area", "stage", "plantarea" },
        ["Scope"] = new[] { "scope" },
        ["ResponsibleCompany"] = new[] { "responsiblecompanycode", "resp", "respcompany" },
        ["MRQNumber"] = new[] { "mrqno", "mrq", "mrno", "mr" },
        ["Frozen"] = new[] { "freezeddocuments", "frozen" },
        ["FinalStatus"] = new[] { "finalstatus" },

        // فیلدهای سطح ردیف که قبلاً «شناسایی‌نشده» گزارش می‌شدند
        ["PlantNo"] = new[] { "plantno" },
        ["MaterialCode"] = new[] { "materialcode" },
        ["TrainUnitNo"] = new[] { "trainno+unitno" },
        ["SerialNo"] = new[] { "serialno" },
        ["Remark"] = new[] { "remark" },
        ["TransmittalLink"] = new[] { "transmittallink" },
        ["ExtraWork"] = new[] { "exrawork" },

        ["RevisionNo"] = new[] { "rev", "revision", "docrev" },
        ["TransmittalNo"] = new[] { "transmittalno" },
        ["TransmittalDate"] = new[] { "transmittaldate", "issuedate", "1stissuedate", "jscfinalissuedate", "tna/jscfinalissuedate", "date" },
        ["POI"] = new[] { "poi", "purposeofissue" },
        ["CommentNo"] = new[] { "commentno", "commentsheetno", "csno", "clientcommentno", "jsccommentno" },
        ["CommentDate"] = new[] { "commentdate", "commentsheetdate", "clientcommentdate", "jsccommentdate" },
        ["CommentStatus"] = new[] { "commentstatus", "reviewstatus" },

        // بلوک "Reply To JAM" - قبلاً کلاً نادیده گرفته می‌شد
        ["TransmittalReplyNo"] = new[] { "transmittalreply", "trreply" },
        ["ReplyDate"] = new[] { "replydate" },
        ["ReplyNo"] = new[] { "replyno" },
        ["ReplyStatus"] = new[] { "replystatus" },

        // بلوک "Send To Site" - چرخه‌ی جداگانه‌ی دیگری که در فایل واقعی چندین بار تکرار شده
        ["CommentStatusRevisionNo"] = new[] { "revcs" },
        ["DrawingSheetNo"] = new[] { "sheet" },
        ["DrawingSize"] = new[] { "size" },
    };

    public MDRImportService(PEMSDbContext context, ILogger<MDRImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> StageFromExcelAsync(Stream fileStream, string fileName, int projectId, int? importedById)
    {
        var batch = new MDRImportBatch
        {
            FileName = fileName,
            ProjectId = projectId,
            ImportedById = importedById,
            ImportDate = DateTime.Now,
            Status = "Staged"
        };

        await _context.MDRImportBatches.AddAsync(batch);
        await _context.SaveChangesAsync();

        using var workbook = new XLWorkbook(fileStream);

        int rowsValid = 0, rowsWarning = 0, rowsError = 0, rowsDuplicate = 0, rowIndex = 0;

        var existingDocNumbers = await _context.EngineeringDocuments
            .Where(x => x.ProjectId == projectId)
            .Select(x => x.DocumentNo.ToLower())
            .ToListAsync();

        var processedDocNumbersInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var headerWarnings = new List<string>();

        foreach (var sheet in workbook.Worksheets)
        {
            var sheetNameLower = sheet.Name.ToLowerInvariant();

            if (SkipSheetKeywords.Any(k => sheetNameLower.Contains(k)))
                continue;

            var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

            if (lastCol == 0 || lastRow < 2)
                continue;

            var headerRow = FindHeaderRow(sheet);
            var headerMap = BuildHeaderMap(sheet, headerRow, lastCol);

            var docNoCol = ResolveFirstAliasColumn(headerMap, FieldAliases["DocumentNumber"]);
            if (docNoCol == null)
                continue; // این شیت اصلاً ستون شماره مدرک ندارد - رد می‌شود، نه fail خاموش

            // گزارش پوشش هدر: هر ستونی که هیچ الیاسی برایش تعریف نشده، اینجا ثبت می‌شود تا
            // در آینده وقتی هدری جابجا/تغییرنام داده شد، بی‌صدا گم نشود.
            var recognizedKeys = new HashSet<string>(FieldAliases.Values.SelectMany(x => x));
            var unrecognized = headerMap.Keys.Where(k => !recognizedKeys.Contains(k)).ToList();
            if (unrecognized.Count > 0)
                headerWarnings.Add($"شیت «{sheet.Name}»: ستون‌های شناسایی‌نشده → {string.Join(", ", unrecognized)}");

            var isFreezedSheet = sheetNameLower.Contains("freez");
            var inferredArea = InferAreaFromSheetName(sheet.Name);

            for (int r = headerRow + 1; r <= lastRow; r++)
            {
                var docNo = sheet.Cell(r, docNoCol.Value).GetString().Trim();

                if (string.IsNullOrWhiteSpace(docNo))
                    continue;

                rowIndex++;

                try
                {
                    var realArea = GetByAliases(sheet, r, headerMap, FieldAliases["Area"]);

                    var payload = new MDRImportRowPayload
                    {
                        SequenceNo = rowIndex,
                        DocumentNumber = docNo,
                        Area = realArea ?? inferredArea,
                        Scope = GetByAliases(sheet, r, headerMap, FieldAliases["Scope"]),
                        DisciplineCodeRaw = GetByAliases(sheet, r, headerMap, FieldAliases["Discipline"]),
                        Title = GetByAliases(sheet, r, headerMap, FieldAliases["Title"]),
                        DocumentTypeCode = GetByAliases(sheet, r, headerMap, FieldAliases["DocumentType"]),
                        IsFrozen = isFreezedSheet || !string.IsNullOrWhiteSpace(GetByAliases(sheet, r, headerMap, FieldAliases["Frozen"])),
                        MRQNumber = GetByAliases(sheet, r, headerMap, FieldAliases["MRQNumber"]),
                        ResponsibleCompanyCode = GetByAliases(sheet, r, headerMap, FieldAliases["ResponsibleCompany"]),
                        FinalStatusRaw = GetByAliases(sheet, r, headerMap, FieldAliases["FinalStatus"]),
                        PlantNo = GetByAliases(sheet, r, headerMap, FieldAliases["PlantNo"]),
                        MaterialCode = GetByAliases(sheet, r, headerMap, FieldAliases["MaterialCode"]),
                        TrainUnitNo = GetByAliases(sheet, r, headerMap, FieldAliases["TrainUnitNo"]),
                        SerialNo = GetByAliases(sheet, r, headerMap, FieldAliases["SerialNo"]),
                        Remark = GetByAliases(sheet, r, headerMap, FieldAliases["Remark"]),
                        TransmittalLink = GetByAliases(sheet, r, headerMap, FieldAliases["TransmittalLink"]),
                        ExtraWorkRaw = GetByAliases(sheet, r, headerMap, FieldAliases["ExtraWork"])
                    };

                    // استخراج تمام تاریخچه رویژن‌ها از بلوک‌های تکراری اکسل (مرزبندی‌شده با نقطه میانی)
                    payload.Revisions = ExtractRevisions(sheet, r, headerMap, lastCol);

                    var validation = Validate(payload);

                    var isDuplicateInDb = existingDocNumbers.Contains(payload.DocumentNumber.ToLower());
                    var isDuplicateInFile = !processedDocNumbersInFile.Add(payload.DocumentNumber.ToLower());
                    var isDuplicate = isDuplicateInDb || isDuplicateInFile;

                    if (isDuplicate) rowsDuplicate++;

                    if (validation.Status == "Error") rowsError++;
                    else if (validation.Status == "Warning" || isDuplicateInFile) rowsWarning++;
                    else rowsValid++;

                    var allowCommit = validation.Status != "Error" && !isDuplicateInFile;

                    await _context.MDRImportRows.AddAsync(new MDRImportRow
                    {
                        MDRImportBatchId = batch.MDRImportBatchId,
                        RowNumber = rowIndex,
                        DocumentNumber = payload.DocumentNumber,
                        SheetName = sheet.Name,
                        PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
                        ValidationStatus = isDuplicateInFile ? "Warning" : validation.Status,
                        ValidationMessage = isDuplicateInFile ? "این مدرک قبلاً در همین فایل اکسل وارد شده است" : validation.Message,
                        IsDuplicate = isDuplicate,
                        IncludeInCommit = allowCommit
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "خطا در پارس کردن ردیف {Row} از شیت {Sheet}", r, sheet.Name);
                    rowsError++;

                    await _context.MDRImportRows.AddAsync(new MDRImportRow
                    {
                        MDRImportBatchId = batch.MDRImportBatchId,
                        RowNumber = rowIndex,
                        DocumentNumber = docNo,
                        SheetName = sheet.Name,
                        PayloadJson = "{}",
                        ValidationStatus = "Error",
                        ValidationMessage = "خطای پارس کردن ردیف: " + ex.Message,
                        IncludeInCommit = false
                    });
                }
            }
        }

        batch.RowsTotal = rowIndex;
        batch.RowsValid = rowsValid;
        batch.RowsWarning = rowsWarning;
        batch.RowsError = rowsError;
        batch.RowsDuplicate = rowsDuplicate;
        batch.HeaderWarningsJson = headerWarnings.Count > 0
            ? JsonSerializer.Serialize(headerWarnings.Distinct().ToList(), JsonOptions)
            : null;

        await _context.SaveChangesAsync();

        return batch.MDRImportBatchId;
    }

    public async Task<MDRImportBatchDto?> GetBatchAsync(int batchId)
    {
        var batch = await _context.MDRImportBatches
            .Include(x => x.Project)
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.MDRImportBatchId == batchId);

        if (batch == null) return null;

        return new MDRImportBatchDto
        {
            MDRImportBatchId = batch.MDRImportBatchId,
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
            HeaderWarnings = DeserializeHeaderWarnings(batch.HeaderWarningsJson),
            Rows = batch.Rows.OrderBy(x => x.RowNumber).Select(x => new MDRImportRowDto
            {
                MDRImportRowId = x.MDRImportRowId,
                RowNumber = x.RowNumber,
                DocumentNumber = x.DocumentNumber,
                SheetName = x.SheetName,
                ValidationStatus = x.ValidationStatus,
                ValidationMessage = x.ValidationMessage,
                IsDuplicate = x.IsDuplicate,
                IncludeInCommit = x.IncludeInCommit,
                Payload = string.IsNullOrEmpty(x.PayloadJson)
                    ? null
                    : JsonSerializer.Deserialize<MDRImportRowPayload>(x.PayloadJson, JsonOptions)
            }).ToList()
        };
    }

    public async Task<List<MDRImportBatchDto>> GetRecentBatchesAsync()
    {
        var batches = await _context.MDRImportBatches
            .Include(x => x.Project)
            .OrderByDescending(x => x.ImportDate)
            .Take(20)
            .ToListAsync();

        return batches.Select(batch => new MDRImportBatchDto
        {
            MDRImportBatchId = batch.MDRImportBatchId,
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
            HeaderWarnings = DeserializeHeaderWarnings(batch.HeaderWarningsJson)
        }).ToList();
    }

    public async Task SetRowIncludeAsync(int rowId, bool include)
    {
        var row = await _context.MDRImportRows.FirstOrDefaultAsync(x => x.MDRImportRowId == rowId);
        if (row == null) return;
        row.IncludeInCommit = include;
        await _context.SaveChangesAsync();
    }

    // ==============================================================================
    // Commit اکنون در یک تراکنش دیتابیسی واحد اجرا می‌شود (با ExecutionStrategy چون
    // EnableRetryOnFailure فعال است). قبلاً هر ردیف چند بار SaveChanges جداگانه می‌زد؛
    // یعنی اگر وسط پردازش خطا می‌خورد، بخشی از رکوردها نصفه در دیتابیس می‌ماندند.
    // ==============================================================================
    public async Task<(int created, int updated, int skipped)> CommitBatchAsync(int batchId)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var result = await CommitBatchInternalAsync(batchId);
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    private const int CommitFlushBatchSize = 200;

    private async Task<(int created, int updated, int skipped)> CommitBatchInternalAsync(int batchId)
    {
        var batch = await _context.MDRImportBatches
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.MDRImportBatchId == batchId);

        if (batch == null) throw new Exception("Batch یافت نشد");

        int created = 0, updated = 0, skipped = 0, frozenSkipped = 0;
        var allDisciplines = await _context.Disciplines.Where(x => x.IsActive).ToListAsync();

        // ==============================================================================
        // پیش‌بارگذاری کش‌ها (یک‌بار، قبل از حلقه) - قبلاً به ازای هر ردیف ~۱۵-۲۰ Query/Save
        // جداگانه به دیتابیس زده می‌شد (هر کدام یک Round-trip شبکه)؛ برای فایل‌های چندهزار
        // ردیفی این یعنی چند ساعت. حالا همه‌ی داده‌های مرجع پروژه یک‌جا خوانده می‌شوند و در
        // حافظه چک می‌شوند.
        // ==============================================================================
        var existingDocs = await _context.EngineeringDocuments
            .Where(x => x.ProjectId == batch.ProjectId)
            .ToDictionaryAsync(x => x.DocumentNo.ToLower(), x => x);

        var existingDocIds = existingDocs.Values.Select(x => x.DocumentId).ToList();

        var revisionCache = existingDocIds.Count == 0
            ? new Dictionary<(int, string), DocumentRevision>()
            : (await _context.DocumentRevisions
                .Where(r => existingDocIds.Contains(r.DocumentId))
                .ToListAsync())
                .ToDictionary(r => (r.DocumentId, r.RevisionNo), r => r);

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

        var mrCache = await _context.MaterialRequisitions
            .Where(x => x.ProjectId == batch.ProjectId)
            .ToDictionaryAsync(x => x.MRNumber.ToLower(), x => x);

        var mrIds = mrCache.Values.Select(m => m.MRId).ToList();
        var existingMrqNumbers = mrIds.Count == 0
            ? new HashSet<(int, string)>()
            : new HashSet<(int, string)>((await _context.MaterialRequisitionQuotations
                .Where(x => mrIds.Contains(x.MRId))
                .Select(x => new { x.MRId, x.MRQNumber })
                .ToListAsync())
                .Select(x => (x.MRId, x.MRQNumber.ToLower())));

        var fallbackDisciplineId = allDisciplines.FirstOrDefault()?.DisciplineId
            ?? await _context.Disciplines.Select(x => x.DisciplineId).FirstOrDefaultAsync();

        // بافر ردیف‌هایی که هنوز CreatedDocumentId‌شان (بعد از Flush بعدی) نوشته نشده
        var pendingRowDocPairs = new List<(MDRImportRow Row, EngineeringDocument Doc)>();
        var rowsSinceFlush = 0;

        foreach (var row in batch.Rows.Where(x => x.IncludeInCommit))
        {
            var payload = JsonSerializer.Deserialize<MDRImportRowPayload>(row.PayloadJson, JsonOptions);

            if (payload == null || string.IsNullOrWhiteSpace(payload.DocumentNumber))
            {
                skipped++;
                continue;
            }

            int? disciplineId = null;
            if (!string.IsNullOrWhiteSpace(payload.DisciplineCodeRaw))
            {
                var discipline = DisciplineMatcher.Match(allDisciplines, payload.DisciplineCodeRaw);
                disciplineId = discipline?.DisciplineId;
            }

            var docKey = payload.DocumentNumber.ToLower();
            EngineeringDocument doc;
            var isNewDoc = false;

            if (existingDocs.TryGetValue(docKey, out var existing))
            {
                doc = existing;

                // طبق Phase E-01 (رفع P0 ممیزی Freeze): قبلاً ایمپورت دسته‌ای می‌توانست متادیتای
                // مدرک منجمد را بی‌سروصدا رونویسی کند - دقیقاً همان محدودیتی که Freeze باید
                // جلویش را بگیرد. مدرک منجمد از این ردیف کاملاً رد می‌شود (نه فقط چند فیلد).
                if (doc.IsFrozen)
                {
                    frozenSkipped++;
                    continue;
                }

                doc.ModifiedDate = DateTime.Now;

                doc.Title = string.IsNullOrWhiteSpace(payload.Title) ? doc.Title : payload.Title!;
                doc.DocumentType = string.IsNullOrWhiteSpace(payload.DocumentTypeCode) ? doc.DocumentType : payload.DocumentTypeCode!;
                doc.DisciplineId = disciplineId ?? doc.DisciplineId;
                doc.Area = string.IsNullOrWhiteSpace(payload.Area) ? doc.Area : payload.Area;
                doc.Description = string.IsNullOrWhiteSpace(payload.Remark) ? doc.Description : payload.Remark;

                updated++;
            }
            else
            {
                doc = new EngineeringDocument
                {
                    ProjectId = batch.ProjectId,
                    CreatedDate = DateTime.Now,
                    IsActive = true,
                    DocumentNo = payload.DocumentNumber,
                    Title = string.IsNullOrWhiteSpace(payload.Title) ? payload.DocumentNumber : payload.Title!,
                    DocumentType = payload.DocumentTypeCode ?? "GEN",
                    DisciplineId = disciplineId,
                    Area = payload.Area,
                    Description = payload.Remark,
                    Status = "Draft"
                };

                _context.EngineeringDocuments.Add(doc);
                existingDocs[docKey] = doc;
                isNewDoc = true;
                created++;
            }

            var latestRevision = payload.Revisions.LastOrDefault();
            if (latestRevision != null)
            {
                doc.RevisionNo = latestRevision.RevisionNo ?? doc.RevisionNo;
                doc.IssuePurpose = latestRevision.PurposeOfIssueCode ?? doc.IssuePurpose;
                doc.CommentStatus = latestRevision.CommentStatus ?? doc.CommentStatus;
                doc.Status = ComputeStatusFromCommentStatus(latestRevision.CommentStatus, doc.Status);
                doc.ActualDate = latestRevision.TransmittalDate ?? doc.ActualDate;
            }

            if (payload.IsFrozen && !doc.IsFrozen)
            {
                doc.IsFrozen = true;
                doc.FrozenDate ??= DateTime.Now;
            }

            // ==============================================
            // ثبت تاریخچه رویژن‌ها و گردش‌کار - از طریق Navigation Property (Document = doc)
            // نه FK عددی مستقیم، تا نیازی به SaveChanges زودهنگام برای گرفتن DocumentId نباشد؛
            // EF Core موقع SaveChanges دسته‌ای در انتها، کلید واقعی را خودش حل می‌کند.
            // ==============================================
            DocumentRevision? previousRevision = null;

            // باگ واقعی کشف‌شده: در فایل‌های واقعی MDR، بیش از یک بلوک ستونی (مثلاً چرخه‌ی
            // اصلی "Doc. Rev." و چرخه‌ی جداگانه‌ی "Send To Site") می‌توانند مقدار متنی یکسانی
            // داشته باشند (مثلاً هر دو "0"). چون (DocumentId, RevisionNo) در دیتابیس Unique
            // است، تلاش برای درج دوباره‌ی همان شماره برای همان سند در همین ردیف باعث خطای
            // "Cannot insert duplicate key row" می‌شد - این چک قبلاً فقط برای اسناد از‌قبل‌موجود
            // اعمال می‌شد (revisionCache)، نه برای اسناد تازه‌ساخته‌شده‌ی همین Batch. بلوک اول
            // (که طبق ترتیب ستونی همیشه چرخه‌ی رسمی MDR است، نه چرخه‌های فرعی) نگه داشته می‌شود.
            var revisionNumbersThisRow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rev in payload.Revisions)
            {
                if (string.IsNullOrWhiteSpace(rev.RevisionNo)) continue;

                if (!revisionNumbersThisRow.Add(rev.RevisionNo))
                    continue; // همین شماره رویژن قبلاً در همین ردیف/همین سند پردازش شده

                DocumentRevision? existingRev = null;
                if (!isNewDoc && doc.DocumentId != 0)
                    revisionCache.TryGetValue((doc.DocumentId, rev.RevisionNo), out existingRev);

                if (existingRev == null)
                {
                    var computedStatus = ComputeStatusFromCommentStatus(rev.CommentStatus, "Draft");

                    var newRevision = new DocumentRevision
                    {
                        Document = doc,
                        RevisionNo = rev.RevisionNo,
                        PreviousRevisionNo = previousRevision?.RevisionNo,
                        Status = computedStatus,
                        IssuePurpose = rev.PurposeOfIssueCode,
                        Comment = BuildRevisionComment(rev),
                        RevisionDate = rev.TransmittalDate ?? DateTime.Now,
                        IssuedDate = rev.TransmittalDate,
                        CreatedDate = DateTime.Now,
                        IsActive = true
                    };

                    _context.DocumentRevisions.Add(newRevision);

                    if (previousRevision != null)
                        previousRevision.SupersededRevision = newRevision;

                    if (!isNewDoc && doc.DocumentId != 0)
                        revisionCache[(doc.DocumentId, rev.RevisionNo)] = newRevision;

                    previousRevision = newRevision;

                    if (!string.IsNullOrWhiteSpace(rev.TransmittalNo))
                    {
                        _context.DocumentWorkflows.Add(new DocumentWorkflow
                        {
                            Document = doc,
                            FromStatus = "Draft",
                            ToStatus = "Submitted",
                            ActionType = "Submit",
                            Comment = $"MDR Import: Submitted via Transmittal {rev.TransmittalNo}",
                            ActionDate = rev.TransmittalDate ?? DateTime.Now,
                            IsActive = true
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(rev.CommentStatus))
                    {
                        _context.DocumentWorkflows.Add(new DocumentWorkflow
                        {
                            Document = doc,
                            FromStatus = "Submitted",
                            ToStatus = computedStatus,
                            ActionType = "Review",
                            Comment = $"MDR Import: Client Status {rev.CommentStatus}. Comment Sheet: {rev.CommentNo ?? "N/A"}",
                            ActionDate = rev.CommentDate ?? rev.TransmittalDate ?? DateTime.Now,
                            IsActive = true
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(rev.ReplyStatus) || !string.IsNullOrWhiteSpace(rev.TransmittalReplyNo))
                    {
                        _context.DocumentWorkflows.Add(new DocumentWorkflow
                        {
                            Document = doc,
                            FromStatus = computedStatus,
                            ToStatus = computedStatus,
                            ActionType = "ReplyToClient",
                            Comment = $"MDR Import: Reply Transmittal {rev.TransmittalReplyNo ?? "N/A"}, Reply No {rev.ReplyNo ?? "N/A"}, Status {rev.ReplyStatus ?? "N/A"}",
                            ActionDate = rev.ReplyDate ?? rev.CommentDate ?? DateTime.Now,
                            IsActive = true
                        });
                    }
                }
                else
                {
                    previousRevision = existingRev;
                }
            }

            if (latestRevision != null && !string.IsNullOrWhiteSpace(latestRevision.TransmittalNo))
            {
                var transmittalKey = latestRevision.TransmittalNo!.ToLower();
                if (!transmittalCache.TryGetValue(transmittalKey, out var transmittal))
                {
                    transmittal = new Transmittal
                    {
                        TransmittalNo = latestRevision.TransmittalNo!,
                        ProjectId = batch.ProjectId,
                        From = "MDR Import",
                        To = "-",
                        Purpose = "For Information",
                        Status = "Sent",
                        TransmittalDate = DateTime.Now,
                        IsActive = true,
                        CreatedDate = DateTime.Now
                    };
                    _context.Transmittals.Add(transmittal);
                    transmittalCache[transmittalKey] = transmittal;
                }

                var alreadyLinked = transmittal.TransmittalId != 0 && doc.DocumentId != 0
                    && existingTransmittalLinks.Contains((transmittal.TransmittalId, doc.DocumentId));

                if (!alreadyLinked)
                {
                    _context.TransmittalItems.Add(new TransmittalItem
                    {
                        Transmittal = transmittal,
                        Document = doc,
                        RevisionNo = latestRevision.RevisionNo ?? "00"
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(payload.MRQNumber))
            {
                var mrKey = payload.MRQNumber!.Trim().ToLower();
                if (!mrCache.TryGetValue(mrKey, out var mr))
                {
                    mr = new MaterialRequisition
                    {
                        MRNumber = payload.MRQNumber!.Trim(),
                        ProjectId = batch.ProjectId,
                        DisciplineId = disciplineId ?? fallbackDisciplineId,
                        Description = $"Auto-created from MDR/VDR import (MR/MRQ No: {payload.MRQNumber!.Trim()})",
                        Status = "Draft",
                        IsActive = true,
                        CreatedDate = DateTime.Now
                    };
                    _context.MaterialRequisitions.Add(mr);
                    mrCache[mrKey] = mr;

                    _context.MaterialRequisitionQuotations.Add(new MaterialRequisitionQuotation
                    {
                        MRQNumber = payload.MRQNumber!.Trim(),
                        MR = mr,
                        VendorName = "Unknown (from import)",
                        QuotationStatus = "Not Sent",
                        CreatedDate = DateTime.Now,
                        IsActive = true
                    });
                }
                else if (mr.MRId != 0 && !existingMrqNumbers.Contains((mr.MRId, mrKey)))
                {
                    _context.MaterialRequisitionQuotations.Add(new MaterialRequisitionQuotation
                    {
                        MRQNumber = payload.MRQNumber!.Trim(),
                        MR = mr,
                        VendorName = "Unknown (from import)",
                        QuotationStatus = "Not Sent",
                        CreatedDate = DateTime.Now,
                        IsActive = true
                    });
                }

                doc.RelatedMR = mr;
            }

            pendingRowDocPairs.Add((row, doc));
            rowsSinceFlush++;

            // ==============================================================================
            // Flush دسته‌ای: به‌جای SaveChanges به ازای هر ردیف، هر ۲۰۰ ردیف یک‌بار. EF Core
            // خودش کلیدهای موقت (Document/Revision/Transmittal/MR جدید) را از طریق Navigation
            // Property حل می‌کند - نیازی به گرفتن ID زودهنگام نیست.
            // ==============================================================================
            if (rowsSinceFlush >= CommitFlushBatchSize)
            {
                await _context.SaveChangesAsync();

                foreach (var (pendingRow, pendingDoc) in pendingRowDocPairs)
                    pendingRow.CreatedDocumentId = pendingDoc.DocumentId;

                pendingRowDocPairs.Clear();
                rowsSinceFlush = 0;
            }
        }

        await _context.SaveChangesAsync();

        foreach (var (pendingRow, pendingDoc) in pendingRowDocPairs)
            pendingRow.CreatedDocumentId = pendingDoc.DocumentId;

        batch.Status = "Committed";
        batch.CommittedDate = DateTime.Now;
        await _context.SaveChangesAsync();

        if (frozenSkipped > 0)
        {
            _logger.LogWarning(
                "MDR Import Batch {BatchId}: {Count} ردیف به‌خاطر منجمد (Frozen) بودن مدرک مقصد نادیده گرفته شد.",
                batchId, frozenSkipped);
        }

        return (created, updated, skipped);
    }

    public async Task DiscardBatchAsync(int batchId)
    {
        var batch = await _context.MDRImportBatches
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.MDRImportBatchId == batchId);

        if (batch == null) return;

        _context.MDRImportRows.RemoveRange(batch.Rows);
        _context.MDRImportBatches.Remove(batch);
        await _context.SaveChangesAsync();
    }

    public async Task<int> RematchDisciplinesAsync(int? projectId = null)
    {
        var allDisciplines = await _context.Disciplines.Where(x => x.IsActive).ToListAsync();
        var rowsQuery = _context.MDRImportRows
            .Include(x => x.Batch)
            .Where(x => x.CreatedDocumentId.HasValue);

        if (projectId.HasValue)
            rowsQuery = rowsQuery.Where(x => x.Batch!.ProjectId == projectId.Value);

        var rows = await rowsQuery.ToListAsync();
        var fixedCount = 0;

        foreach (var row in rows)
        {
            var payload = JsonSerializer.Deserialize<MDRImportRowPayload>(row.PayloadJson, JsonOptions);
            if (payload == null || string.IsNullOrWhiteSpace(payload.DisciplineCodeRaw)) continue;

            var document = await _context.EngineeringDocuments
                .FirstOrDefaultAsync(x => x.DocumentId == row.CreatedDocumentId!.Value);

            if (document == null || document.DisciplineId.HasValue) continue;

            var match = DisciplineMatcher.Match(allDisciplines, payload.DisciplineCodeRaw);

            if (match != null)
            {
                document.DisciplineId = match.DisciplineId;
                fixedCount++;
            }
        }

        if (fixedCount > 0)
            await _context.SaveChangesAsync();

        return fixedCount;
    }

    public async Task<int> FixLegacyStatusValuesAsync(int? projectId = null)
    {
        var query = _context.EngineeringDocuments
            .Where(x => x.Status == "Revise & Resubmit" || x.Status == "Under Review");

        if (projectId.HasValue)
            query = query.Where(x => x.ProjectId == projectId.Value);

        var documents = await query.ToListAsync();

        foreach (var doc in documents)
        {
            doc.Status = doc.Status == "Revise & Resubmit" ? "Rejected" : "Submitted";
        }

        if (documents.Count > 0)
            await _context.SaveChangesAsync();

        return documents.Count;
    }

    // =========================
    // Helpers
    // =========================

    private static int FindHeaderRow(IXLWorksheet sheet, int maxRowsToCheck = 10)
    {
        var keyHeaders = new[] { "documentno", "documentnumber", "docno" };

        for (int r = 1; r <= maxRowsToCheck; r++)
        {
            for (int c = 1; c <= 30; c++)
            {
                var cellVal = NormalizeHeader(sheet.Cell(r, c).GetString());
                if (keyHeaders.Contains(cellVal))
                {
                    return r;
                }
            }
        }

        return 1;
    }

    private static Dictionary<string, List<int>> BuildHeaderMap(IXLWorksheet sheet, int headerRow, int lastCol)
    {
        var map = new Dictionary<string, List<int>>();

        for (int c = 1; c <= lastCol; c++)
        {
            var cell = sheet.Cell(headerRow, c);
            var h = NormalizeHeader(cell.GetString());

            if (string.IsNullOrEmpty(h))
            {
                for (int prevC = c - 1; prevC >= 1; prevC--)
                {
                    var prevVal = NormalizeHeader(sheet.Cell(headerRow, prevC).GetString());
                    if (!string.IsNullOrEmpty(prevVal))
                    {
                        h = prevVal;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(h))
            {
                if (!map.ContainsKey(h))
                    map[h] = new List<int>();

                map[h].Add(c);
            }
        }

        return map;
    }

    /// <summary>
    /// اولین ستون منطبق با یکی از نام‌های الیاس (بدون محدودیت بازه) - برای فیلدهای سطح
    /// سند مثل Document No که فقط یک بار در هر ردیف باید باشد.
    /// </summary>
    private static int? ResolveFirstAliasColumn(Dictionary<string, List<int>> map, string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (map.TryGetValue(alias, out var cols) && cols.Count > 0)
                return cols[0];
        }

        return null;
    }

    /// <summary>
    /// مقدار یک فیلد سطح سند را از هر یک از نام‌های الیاس ممکن می‌خواند (بدون محدودیت بازه
    /// ستونی). اگر چند ستون با یک هدر باشند، آخرین ستون غیرخالی برداشته می‌شود (رفتار قبلی
    /// GetByHeader حفظ شده است).
    /// </summary>
    private static string? GetByAliases(IXLWorksheet sheet, int row, Dictionary<string, List<int>> map, string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (map.TryGetValue(alias, out var columns))
            {
                for (int i = columns.Count - 1; i >= 0; i--)
                {
                    var v = sheet.Cell(row, columns[i]).GetString();

                    if (!string.IsNullOrWhiteSpace(v))
                        return v.Trim();
                }
            }
        }

        return null;
    }

    /// <summary>
    /// مقدار یک فیلد داخل یک بلوک رویژن مشخص را می‌خواند: فقط در بازه‌ی ستونی [left, right]
    /// جستجو می‌کند (تا بلوک‌های مجاور با هم قاطی نشوند) و اگر نزدیک‌ترین ستون به ستون
    /// Rev خالی بود، به ترتیب فاصله ستون‌های بعدی را هم امتحان می‌کند (fallback) قبل از
    /// اینکه null برگرداند.
    /// </summary>
    private static string? GetValueInRange(
        IXLWorksheet sheet, int row, Dictionary<string, List<int>> map,
        int left, int right, int anchorCol, string[] aliases)
    {
        var candidates = new List<int>();

        foreach (var alias in aliases)
        {
            if (map.TryGetValue(alias, out var cols))
                candidates.AddRange(cols.Where(c => c >= left && c <= right));
        }

        foreach (var col in candidates.Distinct().OrderBy(c => Math.Abs(c - anchorCol)))
        {
            var v = sheet.Cell(row, col).GetString();
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return null;
    }

    // مقادیری که در فایل‌های واقعی گاهی به اشتباه/به‌عنوان Placeholder داخل ستون Rev نوشته
    // می‌شوند (مثلاً "W.F.C" وقتی هنوز چرخه‌ی بعدی شروع نشده) و نباید به‌عنوان شماره
    // رویژن واقعی پذیرفته شوند.
    private static readonly HashSet<string> NonRevisionPlaceholders =
        new(StringComparer.OrdinalIgnoreCase) { "W.F.C", "WFC", "N/A", "NA", "NO", "YES", "-", "TBD" };

    /// <summary>
    /// استخراج تاریخچه رویژن‌ها از بلوک‌های تکراری اکسل. مرز هر بلوک با نقطه‌ی میانی بین
    /// دو ستون Rev متوالی محاسبه می‌شود (نه صرفاً "نزدیک‌ترین فاصله" بدون مرز) تا وقتی
    /// تعداد/فاصله بلوک‌ها در فایل‌های آینده تغییر کرد، داده‌ی یک بلوک به بلوک مجاور نشتی
    /// نکند.
    /// </summary>
    private static List<MDRRevisionBlock> ExtractRevisions(
        IXLWorksheet sheet, int row, Dictionary<string, List<int>> map, int lastCol)
    {
        var revisions = new List<MDRRevisionBlock>();

        // مهم: ستون‌های هر سه الیاس (rev/revision/docrev) با هم ادغام می‌شوند، نه اینکه
        // فقط یکی انتخاب شود. در فایل‌های واقعی دیده شد که یک شیت هم‌زمان از "Doc. Rev."
        // (چرخه‌ی Issued Document) و "Rev." (چرخه‌ی جداگانه‌ی Send To Site) استفاده می‌کند؛
        // انتخاب فقط یکی از این دو باعث گم‌شدن کامل بلوک‌های دیگری می‌شد - همان باگ اصلی
        // گزارش‌شده.
        var revColsRaw = new List<int>();
        foreach (var alias in FieldAliases["RevisionNo"])
        {
            if (map.TryGetValue(alias, out var cols))
                revColsRaw.AddRange(cols);
        }

        if (revColsRaw.Count == 0) return revisions;

        var sortedRevCols = revColsRaw.Distinct().OrderBy(c => c).ToList();

        for (int i = 0; i < sortedRevCols.Count; i++)
        {
            var col = sortedRevCols[i];
            var revNo = sheet.Cell(row, col).GetString().Trim();
            if (string.IsNullOrWhiteSpace(revNo) || NonRevisionPlaceholders.Contains(revNo)) continue;

            var leftBoundary = i == 0 ? 1 : (sortedRevCols[i - 1] + col) / 2 + 1;
            var rightBoundary = i == sortedRevCols.Count - 1 ? lastCol : (col + sortedRevCols[i + 1]) / 2;

            var block = new MDRRevisionBlock
            {
                RevisionNo = revNo,
                TransmittalNo = GetValueInRange(sheet, row, map, leftBoundary, rightBoundary, col, FieldAliases["TransmittalNo"]),
                TransmittalDate = ParseDate(GetValueInRange(sheet, row, map, leftBoundary, rightBoundary, col, FieldAliases["TransmittalDate"])),
                PurposeOfIssueCode = GetValueInRange(sheet, row, map, leftBoundary, rightBoundary, col, FieldAliases["POI"])?.Trim().ToUpper(),
                CommentNo = GetValueInRange(sheet, row, map, leftBoundary, rightBoundary, col, FieldAliases["CommentNo"]),
                CommentDate = ParseDate(GetValueInRange(sheet, row, map, leftBoundary, rightBoundary, col, FieldAliases["CommentDate"])),
                CommentStatus = NormalizeStatusCode(GetValueInRange(sheet, row, map, leftBoundary, rightBoundary, col, FieldAliases["CommentStatus"])),
                TransmittalReplyNo = GetValueInRange(sheet, row, map, leftBoundary, rightBoundary, col, FieldAliases["TransmittalReplyNo"]),
                ReplyDate = ParseDate(GetValueInRange(sheet, row, map, leftBoundary, rightBoundary, col, FieldAliases["ReplyDate"])),
                ReplyNo = GetValueInRange(sheet, row, map, leftBoundary, rightBoundary, col, FieldAliases["ReplyNo"]),
                ReplyStatus = GetValueInRange(sheet, row, map, leftBoundary, rightBoundary, col, FieldAliases["ReplyStatus"]),
                CommentStatusRevisionNo = GetValueInRange(sheet, row, map, leftBoundary, rightBoundary, col, FieldAliases["CommentStatusRevisionNo"]),
                DrawingSheetNo = GetValueInRange(sheet, row, map, leftBoundary, rightBoundary, col, FieldAliases["DrawingSheetNo"]),
                DrawingSize = GetValueInRange(sheet, row, map, leftBoundary, rightBoundary, col, FieldAliases["DrawingSize"])
            };

            revisions.Add(block);
        }

        return revisions;
    }

    private static string? BuildRevisionComment(MDRRevisionBlock rev)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(rev.CommentNo))
            parts.Add($"Comment Sheet: {rev.CommentNo}");

        if (!string.IsNullOrWhiteSpace(rev.TransmittalReplyNo))
            parts.Add($"Reply Transmittal: {rev.TransmittalReplyNo}");

        if (!string.IsNullOrWhiteSpace(rev.ReplyNo))
            parts.Add($"Reply No: {rev.ReplyNo}");

        if (!string.IsNullOrWhiteSpace(rev.ReplyStatus))
            parts.Add($"Reply Status: {rev.ReplyStatus}");

        return parts.Count > 0 ? string.Join(" | ", parts) : null;
    }

    private static List<string> DeserializeHeaderWarnings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string InferAreaFromSheetName(string sheetName)
    {
        var idx = sheetName.IndexOf("Docs", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? sheetName[(idx + 4)..].Trim() : sheetName.Trim();
    }

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
            .Replace("(", "")
            .Replace(")", "");
    }

    private static int? ParseInt(string? raw)
    {
        return int.TryParse(raw?.Trim(), out var v) ? v : null;
    }

    private static DateTime? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTime.TryParse(raw, out var d) ? d : null;
    }

    private static string? NormalizeStatusCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var t = raw.Trim().ToUpper().TrimEnd('*');

        return t switch
        {
            "AP" or "NC" or "AC" or "CO" or "RE" => t,
            "W.F.C" or "WFC" => "W.F.C",
            _ => t
        };
    }

    private static string ComputeStatusFromCommentStatus(string? commentStatus, string currentStatus)
    {
        return commentStatus switch
        {
            "AP" or "NC" => "Approved",
            "CO" or "AC" or "RE" => "Rejected",
            "W.F.C" => "Submitted",
            _ => currentStatus
        };
    }

    private record ValidationResult(string Status, string? Message);

    private static ValidationResult Validate(MDRImportRowPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.DocumentNumber))
            return new ValidationResult("Error", "شماره مدرک خالی است");

        if (string.IsNullOrWhiteSpace(payload.DisciplineCodeRaw))
            return new ValidationResult("Warning", "دیسیپلین مشخص نشده است");

        // بررسی سخت‌گیرانه: اگر وضعیت نهایی مدرک "ISSUED" است ولی هیچ رویژنی از اکسل
        // استخراج نشده، این دقیقاً همان الگوی باگی است که قبلاً باعث خالی ماندن
        // DocumentRevisions می‌شد - اینجا باید به‌صورت Warning قابل‌مشاهده باشد، نه گم شدن خاموش.
        var finalStatusNormalized = payload.FinalStatusRaw?.Replace(" ", "").ToUpperInvariant();
        if (payload.Revisions.Count == 0 && finalStatusNormalized == "ISSUED")
            return new ValidationResult("Warning", "وضعیت نهایی ISSUED است اما هیچ رویژنی از ستون‌های Rev استخراج نشد - هدر ستون‌های رویژن را بررسی کنید");

        return new ValidationResult("Valid", null);
    }
}
