using System.Linq.Expressions;
using PEMS.Domain.Entities;

namespace PEMS.Web.Models;

/// <summary>
/// D20: تعریف واحد KPI های مدارک مهندسی در داشبورد (تنها منبع؛ کارت‌ها و تست‌ها از همین استفاده می‌کنند).
/// ریشه‌ی اختلاف «۹۵۱ Rejected در برابر ۳۳ Pending Approval»: Import لیست MDR کدهای پاسخ کارفرما CO (Commented) و AC (Approved with comments) را هم به Status="Rejected"
/// نگاشت می‌کرد (MDRImportService.ComputeStatusFromCommentStatus)، پس «Rejected» عمدتاً مدارک «بازگشته با کامنت» بود نه ردشده‌ی واقعی؛ در حالی که «Pending» فقط
/// مدارکی است که همین الان در گردش‌کار داخلی (Submitted/Checked) منتظر اقدام‌اند. نگاشت Import برای داده‌ی تاریخی دست‌نخورده ماند؛ داشبورد این‌ها را جدا می‌شمارد.
/// </summary>
public static class DashboardDefinitions
{
    /// <summary>منتظر اقدام در گردش‌کار داخلی (Submitted یا Checked).</summary>
    public static readonly Expression<Func<EngineeringDocument, bool>> PendingApproval = x => x.Status == "Submitted" || x.Status == "Checked";

    /// <summary>ردشده‌ی واقعی: وضعیت Rejected و پاسخ کارفرما RE (یا بدون کد پاسخ، یعنی رد داخلی).</summary>
    public static readonly Expression<Func<EngineeringDocument, bool>> Rejected = x => x.Status == "Rejected" && (x.CommentStatus == null || x.CommentStatus == "" || x.CommentStatus == "RE");

    /// <summary>بازگشته با کامنت کارفرما (CO یا AC): نیاز به اصلاح و ارسال مجدد دارد ولی «رد» نیست.</summary>
    public static readonly Expression<Func<EngineeringDocument, bool>> ReturnedWithComments = x => x.Status == "Rejected" && (x.CommentStatus == "CO" || x.CommentStatus == "AC");

    /// <summary>قراردادهای در اجرا: Effective، یا Active میراثی (پیش از معماری گردش‌کار).</summary>
    public static readonly Expression<Func<Contract, bool>> ActiveContract = c => c.Status == "Effective" || c.Status == "Active";
}

public sealed record DisciplinePerformanceRow(string Discipline, int Total, int Approved, int Open, int PendingApproval)
{
    public int ApprovedPercent => Total == 0 ? 0 : (int)Math.Round(100.0 * Approved / Total);
}
