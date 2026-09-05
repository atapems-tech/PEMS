namespace PEMS.Application.DTOs;

/// <summary>
/// یک ردیف پارس‌شده از اکسل MDR واقعی، پیش از ذخیره؛ به‌صورت JSON در MDRImportRow.PayloadJson
/// نگه‌داری می‌شود. ساختار آن از حالت تخت به ساختار تو در تو تغییر یافته تا بتواند 
/// تاریخچه کامل رویژن‌ها (Rev 0, Rev 1, ...) را از اکسل استخراج کند.
/// </summary>
public class MDRImportRowPayload
{
    public int SequenceNo { get; set; }
    public string? Area { get; set; }

    // مقدار خام ستون Scope (مثلاً TNA/JSC/FSP) - جدا از Area، چون در فایل‌های واقعی این دو
    // مفهوم متفاوتی دارند و هر دو باید حفظ شوند، حتی اگر فعلاً فقط برای نمایش/ممیزی استفاده شوند
    public string? Scope { get; set; }

    public string DisciplineCodeRaw { get; set; } = "";
    public string DocumentNumber { get; set; } = "";
    public string? Title { get; set; }
    public string? DocumentTypeCode { get; set; }

    // فیلدهای وضعیت کلی مدرک
    public bool IsFrozen { get; set; }
    public string? MRQNumber { get; set; }
    public string? ResponsibleCompanyCode { get; set; }

    // مقدار خام ستون FINAL STATUS (ISSUED / NOT ISSUED / Deleted / ...) - برای اعتبارسنجی
    // سخت‌گیرانه‌ی «اگر ISSUED است ولی رویژنی استخراج نشد، هشدار بده»
    public string? FinalStatusRaw { get; set; }

    // ==============================================
    // فیلدهای اضافی سطح ردیف که قبلاً «ستون شناسایی‌نشده» گزارش می‌شدند و بی‌صدا دور
    // ریخته می‌شدند. طبق بررسی سخت‌گیرانه، همه نگه داشته می‌شوند حتی اگر فعلاً فقط برای
    // ممیزی/گزارش استفاده شوند.
    // ==============================================
    public string? PlantNo { get; set; }
    public string? MaterialCode { get; set; }
    public string? TrainUnitNo { get; set; }
    public string? SerialNo { get; set; }
    public string? Remark { get; set; }
    public string? TransmittalLink { get; set; }
    public string? ExtraWorkRaw { get; set; }

    // ==============================================
    // لیست تاریخچه رویژن‌ها (جایگزین فیلدهای تخت قبلی)
    // این لیست توسط متد ExtractRevisions در سرویس پر می‌شود
    // ==============================================
    public List<MDRRevisionBlock> Revisions { get; set; } = new();
}

/// <summary>
/// یک بلوک از ستون‌های تکراری در اکسل MDR (معادل یک رویژن خاص مثل Rev 0 یا Rev 1)
/// فیلدهایی که قبلاً در بدنه اصلی بودند، به اینجا منتقل شده‌اند.
/// </summary>
public class MDRRevisionBlock
{
    public string? RevisionNo { get; set; }
    public string? PurposeOfIssueCode { get; set; }
    public string? TransmittalNo { get; set; }
    public DateTime? TransmittalDate { get; set; }
    public string? CommentNo { get; set; }
    public DateTime? CommentDate { get; set; }
    public string? CommentStatus { get; set; }
    public int? CommentDelayDays { get; set; }

    // ==============================================
    // بلوک "Reply To JAM" - قبلاً کلاً نادیده گرفته می‌شد (باگ تایید شده روی فایل واقعی)
    // ==============================================
    public string? TransmittalReplyNo { get; set; }
    public DateTime? ReplyDate { get; set; }
    public string? ReplyNo { get; set; }
    public string? ReplyStatus { get; set; }

    // ==============================================
    // فیلدهای بلوک "Send To Site" - یک چرخه‌ی کاملاً جداگانه که در فایل واقعی چندین بار
    // تکرار شده (شماره رویژن مربوط به کامنت، شماره شیت نقشه، سایز کاغذ)
    // ==============================================
    public string? CommentStatusRevisionNo { get; set; }
    public string? DrawingSheetNo { get; set; }
    public string? DrawingSize { get; set; }
}

// ==============================================
// کلاس‌های زیر بدون تغییر باقی می‌مانند
// ==============================================

public class MDRImportRowDto
{
    public int MDRImportRowId { get; set; }
    public int RowNumber { get; set; }
    public string DocumentNumber { get; set; } = "";
    public string? SheetName { get; set; }
    public string ValidationStatus { get; set; } = "Valid";
    public string? ValidationMessage { get; set; }
    public bool IsDuplicate { get; set; }
    public bool IncludeInCommit { get; set; }
    public MDRImportRowPayload? Payload { get; set; }
}

public class MDRImportBatchDto
{
    public int MDRImportBatchId { get; set; }
    public string FileName { get; set; } = "";
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public DateTime ImportDate { get; set; }
    public int RowsTotal { get; set; }
    public int RowsValid { get; set; }
    public int RowsWarning { get; set; }
    public int RowsError { get; set; }
    public int RowsDuplicate { get; set; }
    public string Status { get; set; } = "";
    public List<MDRImportRowDto> Rows { get; set; } = new();

    // ==============================================
    // گزارش پوشش هدر (Header Coverage Report): لیست ستون‌های اکسل که در هیچ شیت
    // شناسایی/نگاشت نشدند. برای اینکه وقتی مشاور بعدی ستونی را جابجا/تغییرنام دهد،
    // به‌جای باگ خاموش، همینجا دیده شود.
    // ==============================================
    public List<string> HeaderWarnings { get; set; } = new();
}