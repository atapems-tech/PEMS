using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// Personalized Dashboard طبق بازبینی معماری (بند ۲): کاتالوگ Widget های موجود ثابت است؛
/// این سرویس فقط چیدمان (ترتیب/اندازه/نمایان‌بودن) هرکاربر را نگه‌داری و بازمی‌گرداند.
/// خودِ داده‌ی هر Widget از همان سرویس‌های موجود (VDR، Letter، Intake و ...) در Controller
/// خوانده می‌شود - این سرویس چیزی را تکراری نمی‌کند.
/// </summary>
public interface IUserDashboardService
{
    // کاتالوگ کامل Widget های قابل انتخاب، صرف‌نظر از این‌که کاربر فعلاً چه چیزی فعال دارد
    List<UserDashboardWidgetDto> GetCatalog();

    Task<List<UserDashboardWidgetDto>> GetLayoutAsync(int applicationUserId);

    Task SaveLayoutAsync(int applicationUserId, List<UserDashboardWidgetDto> widgets);

    Task ResetToDefaultAsync(int applicationUserId);
}
