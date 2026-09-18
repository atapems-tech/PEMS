namespace PEMS.Application.Interfaces;

public interface IDocumentWorkflowService
{
    /// <summary>
    /// idcDisciplineIds: دیسیپلین‌هایی که سازنده‌ی مدرک تشخیص داده باید بازبینی کنند (IDC).
    /// اگر null/خالی باشد، یعنی نیازی به IDC نیست و موتور خودش این مرحله را رد می‌کند.
    /// </summary>
    Task SubmitAsync(
        int documentId,
        string? comment,
        int? actionByEmployeeId,
        IReadOnlyList<string> actorRoles,
        IReadOnlyList<int>? idcDisciplineIds = null,
        string? ipAddress = null,
        string? userAgent = null);


    // طبق بررسی چرخه‌ی مدرک: مرحله‌ی Check مستقل قبل از Approve (Prepared/Checked/Approved
    // واقعی، نه یک تایید یک‌مرحله‌ای)
    Task CheckAsync(
        int documentId,
        string? comment,
        int? actionByEmployeeId,
        IReadOnlyList<string> actorRoles,
        string? ipAddress = null,
        string? userAgent = null);


    Task ApproveAsync(
        int documentId,
        string? comment,
        int? actionByEmployeeId,
        IReadOnlyList<string> actorRoles,
        string? ipAddress = null,
        string? userAgent = null);


    Task RejectAsync(
        int documentId,
        string? comment,
        int? actionByEmployeeId,
        IReadOnlyList<string> actorRoles,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// طبق Phase E-04: برخلاف Reject (رد قطعی)، این یعنی "بازبینی‌کننده می‌خواهد سازنده اصلاح
    /// و دوباره Submit کند" - دقیقاً همان الگوی PermitService.ReturnForReworkAsync (موتور
    /// گردش‌کار عمومی هیچ Action مجزای "Return" ندارد؛ از همان گذار واقعی "Reject" استفاده
    /// می‌شود، فقط وضعیت نهایی سند به‌جای "Rejected" برابر "Revise & Resubmit" ثبت می‌شود که
    /// SubmitAsync هم آن را به‌عنوان نقطه‌ی شروع مجاز برای Submit دوباره می‌پذیرد).
    /// </summary>
    Task ReviseAndResubmitAsync(
        int documentId,
        string? comment,
        int? actionByEmployeeId,
        IReadOnlyList<string> actorRoles,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// deliveryUnitType: "InternalDCC" (DCC داخلی خودمان) | "InternalDepartment" (واحد دیگر
    /// داخل شرکت) | "External" (نهاد بیرونی مثل DCC کارفرما/پیمانکار).
    /// برای External، نامه‌ی رسمی (Outgoing) اجباری است - letterNumber/letterSubject/
    /// externalReceiverName باید پر باشند. برای دو حالت داخلی، ثبت نامه اختیاری است.
    /// issuePurpose: طبق Phase E-04 - هدف واقعی همین صدور (IFC/IFA/IFI/...)، در لحظه‌ی صدور
    /// ثبت می‌شود؛ اگر خالی باشد، مقدار قبلی EngineeringDocument.IssuePurpose دست‌نخورده می‌ماند.
    /// </summary>
    Task IssueAsync(
        int documentId,
        string? comment,
        int? actionByEmployeeId,
        IReadOnlyList<string> actorRoles,
        string deliveryUnitType,
        int? deliveryDepartmentId = null,
        string? externalReceiverName = null,
        string? letterNumber = null,
        string? letterSubject = null,
        string? letterBody = null,
        string? issuePurpose = null,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// طبق تصمیم: پاسخ کارفرما دیگر متن آزاد نیست - باید از روی یک نامه‌ی وارده‌ی واقعی
    /// (ثبت‌شده در ماژول نامه‌نگاری، LetterType=Incoming) ثبت شود تا ردیابی رسمی داشته باشد.
    ///
    /// طبق «PEMS EDMS Phase 2 Lifecycle Design» بند ۲: revisionId اختیاری - اگر داده نشود
    /// (رفتار قبلی، دست‌نخورده برای فراخوانی‌های موجود)، آخرین رویژن مدرک انتخاب می‌شود -
    /// دقیقاً همان منطق تفکیک "آخرین رویژن" که ساخت رویژن جدید برای SupersededRevisionId
    /// استفاده می‌کند. پاسخ روی همان رویژن مشخص ثبت می‌شود (مرجع حقیقت)؛ فیلدهای هم‌نام روی
    /// Document فقط به‌عنوان Cache آخرین رویژن به‌روز می‌مانند.
    /// </summary>
    Task RecordClientResponseAsync(
        int documentId,
        string clientStatus,
        int incomingLetterId,
        string? clientComment,
        int? actionByEmployeeId,
        IReadOnlyList<string> actorRoles,
        string? ipAddress = null,
        string? userAgent = null,
        int? revisionId = null);


    /// <summary>
    /// پاسخ یک دیسیپلین به بازبینی IDC. decision باید Approve/Comment/Reject باشد.
    ///
    /// طبق «PEMS Engineering IDC Ownership Correction»: این پاسخ فقط نتیجه‌ی همان دیسیپلین را
    /// (به‌صورت دائمی روی DocumentIdcReferral) ثبت می‌کند - هرگز به‌تنهایی Document.Status را
    /// به "Rejected" نمی‌برد، حتی اگر decision برابر "Reject" باشد. وقتی همه‌ی دیسیپلین‌های
    /// ارجاع‌شده پاسخ دادند (صرف‌نظر از این‌که هرکدام چه پاسخی دادند)، مدرک به مرحله‌ی Approval
    /// می‌رود (Document.Status = "Checked") تا سازنده/تأییدکننده‌ی مدرک با دیدن مجموع نظرات
    /// IDC، تصمیم رسمی (Approve/Reject/Revise &amp; Resubmit) را خودش بگیرد.
    /// </summary>
    Task IdcRespondAsync(
        int documentId,
        string decision,
        string? comment,
        int actorEmployeeId,
        int actorDisciplineId,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// وضعیت لحظه‌ای IDC یک مدرک: کدام دیسیپلین‌ها هنوز پاسخ نداده‌اند و کدام‌ها چه پاسخی
    /// داده‌اند - برای نمایش گرافیکی «دقیقاً کجا گیر کرده».
    /// </summary>
    Task<IReadOnlyList<IdcStatusItemDto>> GetIdcStatusAsync(int documentId);

    /// <summary>
    /// طبق «PEMS Engineering Workflow History Read-Path Correction»: تاریخچه‌ی واقعیِ کامل
    /// گردش‌کار این مدرک - روی تمام Instance هایی که این مدرک تا امروز داشته (هر چرخه‌ی
    /// Submit یک Instance جدید می‌سازد)، از جدول واقعاً به‌روز نگه‌داشته‌شده WorkflowHistory؛
    /// نه از EngineeringDocument.Workflows قدیمی (که فقط یک‌بار، موقع Create نوشته می‌شود و
    /// هیچ‌کدام از Submit/Check/IDC/Approve/Reject/Issue را ثبت نمی‌کند).
    /// </summary>
    Task<IReadOnlyList<DocumentWorkflowHistoryItemDto>> GetWorkflowHistoryAsync(int documentId);
}


public class DocumentWorkflowHistoryItemDto
{
    public string ActionType { get; set; } = "";
    public string? Decision { get; set; }
    public string? FromStageName { get; set; }
    public string? ToStageName { get; set; }
    public string? Comment { get; set; }
    public string? ActorName { get; set; }
    public DateTime ActionDate { get; set; }
}


public class IdcStatusItemDto
{
    public int DisciplineId { get; set; }
    public string DisciplineName { get; set; } = "";
    public string Status { get; set; } = ""; // Pending | Approve | Comment | Reject

    // طبق «PEMS Engineering IDC Ownership Correction»: قبلاً این DTO از جداول گذرای موتور
    // گردش‌کار عمومی پر می‌شد و نظر/پاسخ‌دهنده/تاریخ اصلاً در دسترس نبود - حالا از
    // DocumentIdcReferral (رکورد دائمی) می‌آید تا سازنده‌ی مدرک بتواند واقعاً نظرات را
    // تجمیع/مرور کند (Originator Consolidation)
    public string? Comment { get; set; }
    public string? RespondedByName { get; set; }
    public DateTime? RespondedDate { get; set; }
}
