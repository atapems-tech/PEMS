using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۲/۱۳: موتور RAM/Weibull - روی IReliabilityService موجود سوار است
/// (MTBF/MTTR را دوباره محاسبه نمی‌کند)، فقط پارامترهای Weibull و تفکیک Availability را
/// اضافه می‌کند. هیچ‌کدام از این متدها هرگز صفر/مقدار جعلی برنمی‌گردانند وقتی داده ناکافی است -
/// فقط DataStatus=INSUFFICIENT_DATA با Reason صریح.
/// </summary>
public interface IRamWeibullService
{
    Task<WeibullAnalysisDto> GetWeibullAnalysisAsync(int equipmentId);

    Task<RamAnalysisDto> GetRamAnalysisAsync(int equipmentId);
}
