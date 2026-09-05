using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public interface IShiftHandoverService
{
    Task<List<ShiftHandoverListDto>> GetAllAsync(string? status);
    Task<ShiftHandoverDetailDto?> GetDetailsAsync(int id);

    Task<(bool Success, string? Error, int? Id)> CreateAsync(
        int outgoingEmployeeId, int incomingEmployeeId, string? shiftLabel, DateTime handoverDate, string? notes);

    Task<(bool Success, string? Error)> PrepareAsync(int id, int? actorEmployeeId);
    Task<(bool Success, string? Error)> AcknowledgeAsync(int id, int? actorEmployeeId);
    Task<(bool Success, string? Error)> CompleteAsync(int id, int? actorEmployeeId);
}
