namespace PEMS.Web.Helpers;

/// <summary>
/// طبق «PEMS — Engineering Documents UX Transformation»: چندین View (`badge-status-@item.Status`)
/// مستقیم Status را داخل نام کلاس CSS می‌گذاشتند - برای وضعیت‌های تک‌کلمه‌ای (Draft/Approved/...)
/// درست کار می‌کرد، اما برای وضعیت‌های چندکلمه‌ای واقعی ("IDC Review"، "Revise &amp; Resubmit"،
/// "Client Approved") HTML آن را به چند کلاس جدا از هم می‌شکند (مثلاً "badge-status-IDC" و
/// "Review" جدا) که به هیچ Rule ای در admin.css نمی‌خورد - نتیجه یک Badge بی‌رنگ/پیش‌فرض است،
/// نه باگ منطق کسب‌وکار. این متد فقط نگاشت نمایشی (کلاس CSS) را ایمن می‌کند؛ خودِ رشته‌ی
/// Status ذخیره‌شده در دیتابیس دست‌نخورده می‌ماند.
/// </summary>
public static class DocumentStatusBadge
{
    public static string CssClass(string? status) => status switch
    {
        "IDC Review" => "badge-status-IDCReview",
        "Revise & Resubmit" => "badge-status-ReviseResubmit",
        "Client Approved" => "badge-status-ClientApproved",
        null or "" => "badge-status-Draft",
        _ => "badge-status-" + status
    };

    // ---- workspace presentation (semantic tone + icon + text; the stored Status string is never changed) ----

    public static string Tone(string? status) => status switch
    {
        "Approved" or "Issued" or "Client Approved" => "vs-pill--success",
        "Rejected" => "vs-pill--danger",
        "Revise & Resubmit" => "vs-pill--warning",
        "Submitted" or "IDC Review" or "Checked" => "vs-pill--info",
        _ => "vs-pill--neutral"   // Draft, Archived, Superseded, unknown
    };

    public static string Icon(string? status) => status switch
    {
        "Draft" or null or "" => "bi-pencil",
        "Submitted" => "bi-send",
        "IDC Review" => "bi-diagram-3",
        "Checked" => "bi-patch-check",
        "Approved" => "bi-check-circle",
        "Issued" => "bi-send-check",
        "Rejected" => "bi-x-circle",
        "Revise & Resubmit" => "bi-arrow-repeat",
        "Client Approved" => "bi-building-check",
        "Archived" => "bi-archive",
        "Superseded" => "bi-layers",
        _ => "bi-circle"
    };

    /// <summary>کلید Localization وضعیت (بدون فاصله/&amp;): St_IDCReview، St_ReviseResubmit، ...</summary>
    public static string ResourceKey(string? status) => "St_" + status switch
    {
        "IDC Review" => "IDCReview",
        "Revise & Resubmit" => "ReviseResubmit",
        "Client Approved" => "ClientApproved",
        null or "" => "Draft",
        _ => status
    };
}
