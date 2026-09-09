using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 5» بند ۹/۳۵: لایه‌ی کیفیت داده‌ی قابلیت‌اطمینان - فقط‌خواندنی، هرگز رکورد
/// منبع را تغییر نمی‌دهد.
/// </summary>
public interface IReliabilityDataQualityService
{
    Task<ReliabilityDataQualityDto> GetDataQualityAsync();
}
