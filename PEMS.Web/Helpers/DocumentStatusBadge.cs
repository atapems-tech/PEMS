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
}
