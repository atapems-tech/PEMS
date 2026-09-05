using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Persistence.Services;

/// <summary>
/// ==============================================================================
/// پیاده‌سازی سرویس مشترک Global Search. عمداً هیچ دانشی از هیچ موجودیت خاصی (Equipment،
/// VDR، Letter، ...) ندارد - فقط از روی EntityType، Provider مناسب را از میان تمام
/// ISearchableEntityProvider های ثبت‌شده در DI پیدا می‌کند و کار را به او واگذار می‌کند.
/// این دقیقاً یعنی "هیچ پیاده‌سازی تکراری" طبق درخواست اصلی.
/// ==============================================================================
/// </summary>
public class GlobalSearchService : IGlobalSearchService
{
    private readonly IEnumerable<ISearchableEntityProvider> _providers;

    public GlobalSearchService(IEnumerable<ISearchableEntityProvider> providers)
    {
        _providers = providers;
    }

    public async Task<GlobalSearchResponseDto> SearchAsync(
        string entityType, string query, int page = 1, int pageSize = 20, int? projectId = null)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var response = new GlobalSearchResponseDto
        {
            EntityType = entityType,
            Query = query ?? "",
            Page = page,
            PageSize = pageSize
        };

        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(query))
            return response;

        var provider = _providers.FirstOrDefault(p =>
            string.Equals(p.EntityType, entityType, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
        {
            // طبق اصل "بدون شکست خاموش": نوع موجودیت ناشناخته باید صریح مشخص باشد، نه فقط
            // یک نتیجه‌ی خالی که کاربر فکر کند جستجویش نتیجه‌ای نداشته
            throw new InvalidOperationException(
                $"نوع موجودیت «{entityType}» در Global Search Framework ثبت نشده است. Providerهای موجود: " +
                string.Join(", ", _providers.Select(p => p.EntityType)));
        }

        var (items, totalCount) = await provider.SearchAsync(query.Trim(), page, pageSize, projectId);

        response.Items = items;
        response.TotalCount = totalCount;
        response.HasMore = (page * pageSize) < totalCount;

        return response;
    }

    public IReadOnlyList<SearchEntityTypeDto> GetRegisteredEntityTypes()
    {
        return _providers
            .Select(p => new SearchEntityTypeDto { EntityType = p.EntityType, DisplayName = p.DisplayName })
            .OrderBy(x => x.DisplayName)
            .ToList();
    }
}
