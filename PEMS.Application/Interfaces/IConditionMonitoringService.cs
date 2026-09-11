using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۵: زیرساخت نظارت وضعیت - هرگز تله‌متری جعلی تولید نمی‌کند.
/// </summary>
public interface IConditionMonitoringService
{
    Task<ConditionMonitoringFoundationStatusDto> GetFoundationStatusAsync();

    Task<List<ConditionMonitoringPointDto>> GetPointsForEquipmentAsync(int equipmentId);

    Task<(bool Success, string? Error, int? PointId)> CreatePointAsync(
        int equipmentId, string variableType, string tag, string? unit,
        decimal? alarmLow, decimal? alarmHigh, string? description, string? createdBy);

    Task<(bool Success, string? Error)> RecordReadingAsync(
        int pointId, DateTime readingDate, decimal value, string source, string? notes, string? recordedBy);
}
