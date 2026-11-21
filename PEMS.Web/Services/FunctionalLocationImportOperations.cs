using PEMS.Application.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-698: the toggle, commit and discard of FunctionalLocationImportController over IFunctionalLocationImportService.</summary>
public sealed class FunctionalLocationImportOperations(IFunctionalLocationImportService service) : IFunctionalLocationImportOperations
{
    public async Task<(bool Success, string? Error)> ToggleRowAsync(int rowId, bool include) { await service.SetRowIncludeAsync(rowId, include); return (true, null); }

    public async Task<(bool Success, string? Error)> CommitAsync(int batchId)
    {
        await service.CommitBatchAsync(batchId);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DiscardAsync(int batchId) { await service.DiscardBatchAsync(batchId); return (true, null); }
}
