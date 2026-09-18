using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Help System — Phase 3»: مدیریت محتوای بصریِ راهنما - چه خودکار (از موتور
/// گردش‌کار واقعی، IHelpWorkflowDiagramService) چه دستی (Decision Tree/Step Guide/ERD/...).
/// </summary>
public interface IHelpVisualService
{
    Task<IReadOnlyList<HelpVisualRenderDto>> GetVisualsForArticleAsync(
        int helpArticleId, string languageCode, bool includeUnpublished = false);

    Task<IReadOnlyList<HelpVisualAdminEditDto>> GetAllForArticleAdminAsync(int helpArticleId);

    Task<HelpVisualAdminEditDto?> GetForEditAsync(int helpVisualId);

    Task<int> CreateAsync(HelpVisualAdminEditDto dto);

    Task UpdateAsync(int helpVisualId, HelpVisualAdminEditDto dto);

    Task SetPublishedAsync(int helpVisualId, bool isPublished);

    Task DeleteAsync(int helpVisualId);
}
