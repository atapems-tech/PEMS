using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق CMMS Production Baseline v1 - CMMS-07 (Inspection): بازرسی دوره‌ای کمی تجهیز
/// (Condition Monitoring) - نقطه‌ی ورود دوم و مستقل به InspectionRecord، جدا از پذیرش ITP
/// که QCController مدیریت می‌کند.
/// </summary>
public interface IInspectionService
{
    Task<List<InspectionListItemDto>> GetFleetAsync();

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۶B - Wave A): اولویت‌بندی این Grid
    // (Alert/Overdue/DueSoon اول) از روی مقادیر محاسبه‌شده‌ی ConditionStatus/DueStatus است، نه
    // ستونی از DB - پس باید ابتدا کل مجموعه‌ی فعال مرتب شود و بعد صفحه‌بندی روی نتیجه انجام
    // شود (همون اصلی که در Permit.pendingMyApproval و FunctionalLocation.Breadcrumb رعایت شد).
    Task<PagedResult<InspectionListItemDto>> GetPagedFleetAsync(int page, int pageSize);

    Task<List<InspectionListItemDto>> GetForEquipmentAsync(int equipmentId);

    Task<(bool Success, string? Error, int? InspectionRecordId)> CreateAsync(
        int equipmentId, string inspectionType, DateTime inspectionDate, string? inspector,
        decimal? measurementValue, string? measurementUnit, decimal? lowerLimit, decimal? upperLimit,
        string? recommendation, DateTime? nextInspectionDate, string? description);
}
