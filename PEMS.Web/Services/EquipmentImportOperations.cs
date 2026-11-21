using PEMS.Application.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-742: the toggle, commit and discard of EquipmentImportController over IEquipmentImportService.</summary>
public sealed class EquipmentImportOperations(IEquipmentImportService service) : IEquipmentImportOperations
{
    public async Task<(bool Success, string? Error)> ToggleRowAsync(int rowId, bool include) { await service.SetRowIncludeAsync(rowId, include); return (true, null); }
    public async Task<(bool Success, string? Error)> CommitAsync(int batchId) { await service.CommitBatchAsync(batchId); return (true, null); }
    public async Task<(bool Success, string? Error)> DiscardAsync(int batchId) { await service.DiscardBatchAsync(batchId); return (true, null); }
}
