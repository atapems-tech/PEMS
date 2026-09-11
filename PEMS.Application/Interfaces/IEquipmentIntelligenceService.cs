using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۹: نمای یکپارچه‌ی هوشمندی یک تجهیز - ترکیب سرویس‌های موجود،
/// نه یک موتور محاسباتی موازی.
/// </summary>
public interface IEquipmentIntelligenceService
{
    Task<EquipmentIntelligenceProfileDto?> GetProfileAsync(int equipmentId);
}
