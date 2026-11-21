using PEMS.Application.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-694: the review commands of EquipmentFunctionalLocationAssignmentController over IEquipmentFunctionalLocationAssignmentService.</summary>
public sealed class EquipmentFunctionalLocationAssignmentOperations(IEquipmentFunctionalLocationAssignmentService service) : IEquipmentFunctionalLocationAssignmentOperations
{
    public async Task<(bool Success, string? Error)> ToggleRowAsync(int rowId, bool include) { await service.SetRowIncludeAsync(rowId, include); return (true, null); }
    public async Task<(bool Success, string? Error)> ReplaceExistingAsync(int rowId, bool replace) { await service.SetReplaceExistingAssignmentAsync(rowId, replace); return (true, null); }
    public async Task<(bool Success, string? Error)> CommitAsync(int batchId) { await service.CommitBatchAsync(batchId); return (true, null); }
    public async Task<(bool Success, string? Error)> DiscardAsync(int batchId) { await service.DiscardBatchAsync(batchId); return (true, null); }
}
