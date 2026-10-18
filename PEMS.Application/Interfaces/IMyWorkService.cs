using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// نمای واحد «کارهای منتظر اقدام من» روی کارتابل موتور گردش‌کار (بدون موتور/جدول Task جدید). اعلان‌ها (Notification) جدا هستند:
/// خواندن/Dismiss یک اعلان هرگز مرحله‌ای را انجام نمی‌دهد. هویت (employeeId/roles) باید از Principal سمت سرور بیاید، نه از ورودی کلاینت.
/// </summary>
public interface IMyWorkService
{
    Task<MyApprovalsSummaryDto> GetSummaryAsync(int employeeId, IReadOnlyList<string> roles);

    /// <summary>فهرست صفحه‌بندی‌شده؛ مرتب: عقب‌افتاده‌ها، سپس ارجاع‌های شخصی (قبل از صف نقش)، سپس نزدیک‌ترین سررسید، سپس قدیمی‌ترین.</summary>
    Task<MyApprovalsPageDto> GetApprovalsAsync(int employeeId, IReadOnlyList<string> roles, string? module, bool overdueOnly, bool personalOnly, int page, int pageSize);
}
