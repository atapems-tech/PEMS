namespace PEMS.Application.Interfaces;

public interface IMOCService
{
    Task<int> RaiseAsync(
        string title, string description, string? reason, string changeType, string riskLevel,
        int projectId, int disciplineId, int? relatedDocumentId, int? relatedEquipmentId,
        DateTime? temporaryExpiryDate, bool trainingRequired,
        int? raisedByEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null);

    Task AssessRiskAsync(
        int mocId, string riskAssessmentText, int? actorEmployeeId, int actorDisciplineId,
        string? ipAddress = null, string? userAgent = null);

    // ==============================================================================
    // اگر ریسک High باشد، خودکار مرحله‌ی تایید ارشد (SeniorApproval) هم فعال می‌شود -
    // کاربر این را انتخاب نمی‌کند، از روی RiskLevel ثبت‌شده تعیین می‌شود.
    // ==============================================================================
    Task ApproveAsync(
        int mocId, int? actorEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null);

    Task RejectAsync(
        int mocId, string reason, int? actorEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null);

    Task RecordImplementationAsync(
        int mocId, string implementationPlan, int actorEmployeeId,
        string? ipAddress = null, string? userAgent = null);

    Task MarkDocumentsUpdatedAsync(int mocId, string? note, int actorEmployeeId);

    Task MarkTrainingCompletedAsync(int mocId, int actorEmployeeId);

    // طبق رویه‌ی استاندارد MOC: بستن نهایی فقط وقتی چک‌لیست (به‌روزرسانی مدارک + آموزش در
    // صورت نیاز) کامل باشد ممکن است - وگرنه خطای صریح می‌دهد
    Task CloseAsync(
        int mocId, int? actorEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null);
}
