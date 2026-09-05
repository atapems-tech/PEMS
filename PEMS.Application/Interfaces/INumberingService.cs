namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق CMMS Production Baseline v1 بند P0-04: سرویس شماره‌گذاری مرکزی، جایگزین الگوی ناامن
/// Count()+1 که در چند کنترلر مستقل (و ناهماهنگ با هم) تکرار شده بود. هیچ Controller/Service
/// دیگری نباید مستقیماً شماره تولید کند - همه از اینجا می‌گیرند.
/// </summary>
public interface INumberingService
{
    /// <summary>
    /// شماره بعدی را برای یک نوع سند (مثلاً "WorkOrder") به‌صورت اتمیک برمی‌گرداند.
    /// scopeKey دامنه‌ی شمارش را جدا می‌کند - مثلاً سال جاری برای WorkOrder، یا شماره
    /// تجهیز برای MaintenancePlan. اگر داده نشود، بسته به نوع سند پیش‌فرض محاسبه می‌شود.
    /// </summary>
    Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null);
}
