using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۷: لایه‌ی حاکمیت هوش‌مصنوعی روی قابلیت‌اطمینان. این سرویس فقط
/// می‌تواند ردیف‌های AIReliabilityInsight با ReviewStatus="Draft" بسازد - هیچ متدی در این
/// Interface نباید (و در پیاده‌سازی هم هرگز) مستقیماً RCMDecision/MaintenanceStrategy/
/// EquipmentCriticality/FailureRecord/WorkOrder را بنویسد. تایید/رد فقط با ReviewInsightAsync
/// و فقط توسط یک کاربر انسانی (از طریق کنترلر با بررسی مجوز) انجام می‌شود.
/// </summary>
public interface IAIReliabilityInsightService
{
    Task<GenerateInsightsResultDto> GenerateInsightsAsync(int? equipmentClassId, string? generatedBy);

    Task<List<AIReliabilityInsightDto>> GetInsightsAsync(string? reviewStatus);

    Task<(bool Success, string? Error)> ReviewInsightAsync(int insightId, bool approve, int? reviewedByEmployeeId, string? notes);
}
