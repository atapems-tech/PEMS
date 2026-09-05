using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// سرویس مشترک Global Search - طبق درخواست: "یک سرویس مشترک ایجاد شود تا همه ماژول‌ها از
/// همان سرویس استفاده کنند و هیچ پیاده‌سازی تکراری وجود نداشته باشد."
/// </summary>
public interface IGlobalSearchService
{
    Task<GlobalSearchResponseDto> SearchAsync(
        string entityType, string query, int page = 1, int pageSize = 20, int? projectId = null);

    // فهرست تمام موجودیت‌هایی که تا این لحظه در سیستم ثبت شده‌اند (برای UI/مستندسازی/تست)
    IReadOnlyList<SearchEntityTypeDto> GetRegisteredEntityTypes();
}
