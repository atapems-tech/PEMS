namespace PEMS.Domain.Entities;

/// <summary>
/// نوع تخصصی "Vendor Document" از Enterprise Document Core (Document) - همان VDR.
/// فیلدهای مشترک (DocumentNo, Title, ProjectId, DisciplineId, RevisionNo, Status, Area, ...)
/// از Document به ارث می‌رسند. فیلدهای زیر مخصوص گردش‌کار Vendor Document هستند و همان اسم‌های
/// قبلی را دارند تا سرویس/View های موجود VDR بدون تغییر کار کنند (DocumentNumber, WorkflowStatus, ...
/// همچنان جدا از فیلدهای پایه نگه داشته شده‌اند و در لایه‌ی سرویس با فیلدهای پایه همگام می‌شوند).
/// </summary>
public class VDRItem : Document
{
    public int SequenceNo { get; set; }

    // کد خام دیسیپلین از فایل ورودی (ME/EL/IN/PI/PM/HV/HS) - برای Audit نگه داشته می‌شود
    public string? DisciplineCodeRaw { get; set; }

    // آیا این مدرک چند‌دیسیپلینه است (کد PM) و باید به هماهنگ‌کننده ارجاع شود، نه مهندس یک دیسیپلین خاص
    public bool IsMultiDiscipline { get; set; } = false;

    public string? MRNumber { get; set; }

    public int? MRId { get; set; }
    public MaterialRequisition? MR { get; set; }

    public string? CommercialExpeditorName { get; set; }

    public string? EngineeringExpeditorName { get; set; }

    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    // ارتباط مستقیم با تجهیز (برای Traceability و Digital Equipment Passport)
    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public string? FolderNoOnFtp { get; set; }

    public int? PoId { get; set; }
    public PurchaseOrder? Po { get; set; }

    public string? PackageDescription { get; set; }

    // DWG, DSH, PRC, RPT, INX, MNL, DIA, ITP, CLN, PLN, SPC, MTO, FDB, CER, PID, CAT, RTP, IRC, JSC, ...
    // (نسخه‌ی خام Import شده؛ در سرویس با فیلد مشترک DocumentType هم‌گام می‌شود)
    public string DocumentTypeCode { get; set; } = string.Empty;

    public string DocumentNumber { get; set; } = string.Empty;

    public string? DocumentDescription { get; set; }

    // طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» بخش ۴.۳.۱ و Appendix 4: کد متریال (نمونه
    // سند: "0611"). تاکسونومی کامل Appendix 4 (صدها زیرکد) عمداً به‌صورت جدول مرجع بازسازی نشده
    // (منبع OCR با قطعیت پایین، ریسک بالا برای بازتولید اشتباه) - این‌جا فیلد آزاد دستی است.
    public string? MaterialCode { get; set; }

    // طبق بخش ۴.۳.۲ همان سند: کد تایپولوژی مدرک فروشنده (Appendix 3 - TT در فرمت
    // 3863-XX-VD-TT-UU-MMMM-NNNNNN-YYY)، مستقل از Discipline و DocumentTypeCode.
    public string? TypologyCode { get; set; }

    // شماره‌ی رسمی به فرمت SUB-CONTRACTOR/Vendor - جداگانه از DocumentNumber (که فرمت
    // CONTRACTOR را نگه می‌دارد)، چون سند هر دو را به‌صورت موازی برای یک مدرک تعریف کرده.
    public string? VendorFormatDocumentNumber { get; set; }

    // PJPC-Eng, QC, Procurement, Planning, JSC-Eng, JSC-QC, KSC ...
    public string? ResponsiblePartyCode { get; set; }

    // 1, 2, 3 - در صورت نامعتبر بودن مقدار خام، این فیلد خالی می‌ماند و مقدار اصلی در DocumentClassRaw ذخیره می‌شود
    public int? DocumentClass { get; set; }
    public string? DocumentClassRaw { get; set; }

    public string? WrapUpStatusImported { get; set; }

    public DateTime? PlannedFirstIssueDate { get; set; }

    // وضعیت زنده‌ی گردش کار VDR: Planned, Submitted, UnderReview, PendingResubmission, Closed
    // (در سرویس با فیلد مشترک Document.Status هم‌گام می‌شود)
    public string WorkflowStatus { get; set; } = "Planned";

    public ICollection<VDRSubmission> Submissions { get; set; } = new List<VDRSubmission>();
}
