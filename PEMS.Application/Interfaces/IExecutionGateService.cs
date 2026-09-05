using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface IExecutionGateService
{
    Task<ExecutionGateResultDto> EvaluateAsync(int permitId);
    Task<List<ExecutionGateDecisionDto>> GetDecisionsForPermitAsync(int permitId);

    Task<(bool Success, string? Error)> StartWorkAsync(int permitId, int? actorEmployeeId);

    Task<(bool Success, string? Error)> EmergencyOverrideAsync(
        int permitId, string reason, int? actorEmployeeId, IReadOnlyList<string> actorRoles);
}
