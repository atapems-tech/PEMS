using System.ComponentModel.DataAnnotations;
using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// چیدمان شخصی‌سازی‌شده‌ی Dashboard هر کاربر (طبق بازبینی معماری، بند ۲): کاربر می‌تواند
/// Widget اضافه/حذف کند، ترتیب و اندازه را تغییر دهد. کاتالوگ انواع Widget ثابت و در کد است
/// (IUserDashboardService.AvailableWidgets)؛ این جدول فقط ترجیحات کاربر را نگه می‌دارد.
/// </summary>
public class UserDashboardWidget : BaseEntity
{
    [Key]
    public int UserDashboardWidgetId { get; set; }

    public int ApplicationUserId { get; set; }

    // PendingDocuments, MyReferrals, OverdueLetters, RecentDocuments, VDRSummary, IntakeSummary, MyTasks
    public string WidgetKey { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    // Small, Medium, Large
    public string Size { get; set; } = "Medium";

    public bool IsVisible { get; set; } = true;
}
