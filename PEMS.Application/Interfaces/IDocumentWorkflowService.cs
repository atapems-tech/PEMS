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
    /// </summary>
    Task RecordClientResponseAsync(
        int documentId,
        string clientStatus,
        int incomingLetterId,
        string? clientComment,
        int? actionByEmployeeId,
        IReadOnlyList<string> actorRoles,
        string? ipAddress = null,
        string? userAgent = null);


    /// <summary>
    /// پاسخ یک دیسیپلین به بازبینی IDC. decision باید Approve/Comment/Reject باشد. فقط وقتی
    /// همه‌ی دیسیپلین‌های ارجاع‌شده Approve بدهند، مدرک به مرحله‌ی Approval می‌رود؛ اگر حتی
    /// یکی Comment یا Reject بدهد، مدرک رد می‌شود و باید دوباره Submit شود.
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
}


public class IdcStatusItemDto
{
    public int DisciplineId { get; set; }
    public string DisciplineName { get; set; } = "";
    public string Status { get; set; } = ""; // Pending | Approve | Comment | Reject
}
