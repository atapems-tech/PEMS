using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 3.5» بند ۱۵: ماتریس پوشش هوش نگهداری به‌ازای هر EquipmentClass - Query-Time،
/// فقط‌خواندنی، هیچ Mutation ای انجام نمی‌دهد و هرگز خلأ را خودکار پر نمی‌کند.
/// </summary>
public interface IEquipmentClassCoverageService
{
    Task<MaintenanceIntelligenceCoverageSummaryDto> GetCoverageMatrixAsync();
}
