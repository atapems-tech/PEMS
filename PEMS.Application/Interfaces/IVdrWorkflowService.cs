namespace PEMS.Application.Interfaces;

/// <summary>
/// آداپتور گردش‌کار VDR روی موتور عمومی - همان دقتی که برای مدرک مهندسی اعمال شد: RBAC
/// واقعی (نه دستی)، بازبینی موازی دیسیپلین‌ها، تاریخچه‌ی تغییرناپذیر، و اتصال به نامه‌نگاری
/// برای بازگشت رسمی به وندور.
/// </summary>
public interface IVdrWorkflowService
{
    /// <summary>
    /// شروع یک چرخه‌ی بازبینی جدید - وندور مدرک را ارسال کرده. یک VDRSubmission جدید ساخته
    /// می‌شود و اگر disciplineIds داده شود، مرحله‌ی بازبینی موازی با همان دیسیپلین‌ها شروع
    /// می‌شود؛ در غیر این صورت مستقیم به تصمیم نهایی می‌رود.
    /// </summary>
    Task SubmitForReviewAsync(
        int vdrItemId, int revisionNo, string vendorTransmittalNo, DateTime? vendorActualIssuedDate,
        string? purposeOfIssueCode, IReadOnlyList<int>? disciplineIds,
        int? actionByEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null);

    /// <summary>
    /// پاسخ یک دیسیپلین به بازبینی موازی. decision: Approve/Comment/Reject.
    /// </summary>
    Task DisciplineRespondAsync(
        int vdrItemId, string decision, string? comment,
        int actorEmployeeId, int actorDisciplineId,
        string? ipAddress = null, string? userAgent = null);

    /// <summary>
    /// تصمیم نهایی بازبین ارشد - reviewCategory باید یکی از AP/NC/AC/CO/RE باشد.
    /// </summary>
    Task LeadReviewAsync(
        int vdrItemId, string reviewCategory, string? commentSheetNo, DateTime? commentDate,
        int? actionByEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null);

    /// <summary>
    /// بازگشت رسمی به وندور توسط DCC - طبق مقصد (داخلی/بیرونی) ممکن است نامه‌ی رسمی بسازد،
    /// دقیقاً مثل الگوی Issue مدرک مهندسی.
    /// </summary>
    Task ReturnToVendorAsync(
        int vdrItemId, string? replySheetNo, string? clientTransmittalNo,
        int? actionByEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null);

    Task<IReadOnlyList<VdrReviewStatusItemDto>> GetDisciplineReviewStatusAsync(int vdrItemId);
}


public class VdrReviewStatusItemDto
{
    public int DisciplineId { get; set; }
    public string DisciplineName { get; set; } = "";
    public string Status { get; set; } = "";
}
