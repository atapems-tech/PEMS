namespace PEMS.Application.Interfaces;

/// <summary>
/// هویت اقدام‌کننده برای عملیات حساس StoreRequisition: کارمند + نقش‌ها. EmployeeId=null یعنی حساب کاربری به کارمند متصل نیست و
/// هیچ عملیات حساسی پذیرفته نمی‌شود (هرگز به 0 یا هویت ساختگی تبدیل نمی‌شود).
/// </summary>
public sealed record StoreRequisitionActor(int? EmployeeId, IReadOnlyCollection<string> Roles);

/// <summary>عمل‌های ریزدانه‌ی ماژول مجوز «StoreRequisition» (RoleModuleAction).</summary>
public static class StoreRequisitionActions
{
    public const string Module = "StoreRequisition";
    public const string Submit = "Submit";
    public const string Approve = "Approve";
    public const string Reject = "Reject";
    public const string SetApprovedQuantity = "SetApprovedQuantity";
    public const string Reserve = "Reserve";
    public const string Issue = "Issue";
    public const string Close = "Close";
    public const string CloseShort = "CloseShort";
    public const string Cancel = "Cancel";

    public static readonly string[] Sensitive =
        { Submit, Approve, Reject, SetApprovedQuantity, Reserve, Issue, Close, CloseShort, Cancel };
}

/// <summary>
/// طبق سند Inventory بخش ۱۴: جابجایی وضعیت StoreRequisition (Draft→Submitted→Approved→Reserved→[PartiallyIssued]→Issued→Closed، +Rejected/Cancelled)
/// به همراه اثرات جانبی‌اش روی MaterialReservation/InventoryTransaction/MaterialStock. هر عملیات حساس در همین سرویس
/// (نه Controller) بررسی می‌کند: مجوز ریزدانه + هویت فعال کارمند + SoD + جدول گذار + ناورداهای مقدار.
/// requestKey (اختیاری) عملیات را در برابر تلاش مجدد (Timeout/دابل‌کلیک) Idempotent می‌کند.
/// </summary>
public interface IStoreRequisitionLifecycleService
{
    /// <summary>گذار وضعیت طبق جدول StoreRequisitionStatuses. nextStatus=Issued یعنی تحویل همه‌ی باقی‌مانده‌ی رزرو.</summary>
    Task<(bool Success, string? Error)> AdvanceStatusAsync(
        int storeRequisitionId, string nextStatus, string? reason, StoreRequisitionActor actor, string? requestKey = null);

    /// <summary>تعیین مقدار تأییدشده‌ی هر خط (فقط در وضعیت Submitted): 0 ≤ Approved ≤ Requested. پس از تأیید قابل تغییر نیست.</summary>
    Task<(bool Success, string? Error)> SetApprovedQuantitiesAsync(
        int storeRequisitionId, IReadOnlyDictionary<int, decimal> approvedByLineId, StoreRequisitionActor actor, string? requestKey = null);

    /// <summary>تحویل (کامل یا بخشی). quantitiesByLineId=null یعنی تمام باقی‌مانده‌ی رزرو هر خط.</summary>
    Task<(bool Success, string? Error)> IssueAsync(
        int storeRequisitionId, IReadOnlyDictionary<int, decimal>? quantitiesByLineId, string? reason, StoreRequisitionActor actor, string? requestKey = null);

    /// <summary>اجازه‌ی عمل برای رنگ/نمایش دکمه‌ها (UI فقط نمایش می‌دهد؛ اجرا همیشه دوباره در سرویس بررسی می‌شود).</summary>
    Task<bool> IsAllowedAsync(StoreRequisitionActor actor, string action);
}
