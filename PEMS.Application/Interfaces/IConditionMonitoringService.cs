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
        int pointId, DateTime readingDate, decimal value, string source, string? notes, string? recordedBy,
        int? instrumentId = null);

    // طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۵
    Task<(bool Success, string? Error)> AssignEvaluationProfileAsync(int pointId, int? evaluationProfileId);

    // طبق فاز ۸: همیشه محدود به بازه‌ی زمانی + سقف تعداد - هرگز کل تاریخچه بدون محدودیت
    Task<ConditionMonitoringTrendDto?> GetTrendAsync(int pointId, DateTime fromDate, DateTime toDate);

    // طبق «PEMS — Equipment Passport Audit & CBM» فاز ۱ ممیزی §3.6: علامت‌گذاری دستی و صریح
    // یک قرائت برای بازبینی مهندسی - یک اقدام یک‌طرفه‌ی انسانی (دقیقاً مثل Finding.RaiseWorkRequest
    // که هرگز Un-raise نمی‌شود)، نه بازمحاسبه‌ی خاموش ارزیابی خودکار.
    Task<(bool Success, string? Error)> MarkRequiresEngineeringReviewAsync(int readingId, string? markedBy);

    // طبق «PEMS — Equipment Passport Audit & CBM» فاز ۱ ممیزی §3.8: داشبورد CBM ناوگانی - فقط
    // تجمیع روی داده‌ی موجود.
    Task<CbmDashboardDto> GetCbmDashboardAsync();
}
