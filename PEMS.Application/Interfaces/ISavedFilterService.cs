using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// Saved Filters عمومی و قابل‌استفاده‌ی مجدد در هر صفحه‌ی فهرست‌دار (طبق بازبینی معماری).
/// </summary>
public interface ISavedFilterService
{
    Task<List<SavedFilterDto>> GetForPageAsync(int applicationUserId, string pageKey);

    [PEMS.Application.Common.PemsOperation("SavedFilter", "Save")]   // SCRUM-683
    Task<int> SaveAsync(int applicationUserId, string pageKey, string name, string queryString, bool isDefault);

    Task<SavedFilterDto?> GetDefaultAsync(int applicationUserId, string pageKey);

    [PEMS.Application.Common.PemsOperation("SavedFilter", "Delete")]   // SCRUM-683
    Task DeleteAsync(int savedFilterId, int applicationUserId);
}
