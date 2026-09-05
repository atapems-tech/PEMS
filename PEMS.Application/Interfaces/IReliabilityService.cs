using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق CMMS Production Baseline v1 - CMMS-06 (Reliability): محاسبه‌ی MTBF/MTTR/Availability
/// از روی FailureRecord های واقعی هر تجهیز.
/// </summary>
public interface IReliabilityService
{
    Task<List<EquipmentReliabilityDto>> GetFleetReliabilityAsync();
    Task<EquipmentReliabilityDto?> GetEquipmentReliabilityAsync(int equipmentId);
}
