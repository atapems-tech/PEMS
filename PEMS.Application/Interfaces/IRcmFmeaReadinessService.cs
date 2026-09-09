using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 4» بند ۲۶-۲۸/۴۵: ماتریس آماده‌بودی FMEA/RCM - Query-Time، فقط‌خواندنی،
/// هرگز خودکار خلأ را پر نمی‌کند.
/// </summary>
public interface IRcmFmeaReadinessService
{
    Task<RcmFmeaCoverageMatrixDto> GetCoverageMatrixAsync();
    Task<EquipmentClassRcmFmeaReadinessDto?> GetForEquipmentClassAsync(int equipmentClassId);
}
