using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق CMMS-10c: LOTO/Isolation - چرخه‌ی وضعیت خطی و کنترل‌شده‌ی خودش را دارد (بدون نیاز به
/// IWorkflowRuntime - یک تاییدِ تک‌بازیگر است، نه زنجیره‌ی چندنقشی).
/// </summary>
public interface IIsolationService
{
    Task<List<IsolationPointListDto>> GetForPermitAsync(int permitId);

    Task<(bool Success, string? Error, int? Id)> CreateAsync(
        int permitId, string energyType, string location, string? normalState, string? requiredIsolationState,
        string? isolationMethod, int? equipmentId, int? assetId, int? functionalLocationId);

    Task<(bool Success, string? Error)> AdvanceStatusAsync(
        int isolationPointId, string nextStatus, string? reason, int? actorEmployeeId,
        string? lockNumber = null, string? tagNumber = null);
}
