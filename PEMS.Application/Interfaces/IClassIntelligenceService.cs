using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 9» بند ۲۱: ترکیب سه سرویس موجود (IEquipmentClassCoverageService،
/// IReliabilityAnalyticsService، IRcmFmeaReadinessService) در یک نمای واحد - هیچ محاسبه‌ی
/// جدیدی این‌جا تکرار نمی‌شود.
/// </summary>
public interface IClassIntelligenceService
{
    Task<ClassIntelligenceProfileDto?> GetClassIntelligenceAsync(int equipmentClassId);
}
