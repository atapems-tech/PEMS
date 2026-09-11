using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۴: زیرساخت Monte Carlo/RAM Simulation. فقط وقتی پارامترهای Weibull
/// و MTTR واقعی هر دو آماده باشند اجرا می‌کند - هرگز از توزیع فرضی/جعلی شبیه‌سازی نمی‌کند.
/// </summary>
public interface IReliabilitySimulationService
{
    Task<ReliabilitySimulationDto> SimulateAvailabilityAsync(int equipmentId, int iterations = 1000, double horizonHours = 8760);
}
