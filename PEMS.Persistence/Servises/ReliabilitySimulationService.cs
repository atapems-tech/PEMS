using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۴. روی IRamWeibullService موجود سوار است - پارامترهای Weibull/MTTR
/// را دوباره محاسبه نمی‌کند، فقط از آن‌ها به‌عنوان ورودی شبیه‌سازی استفاده می‌کند. Random با یک
/// Seed معین از equipmentId+iterations مقداردهی می‌شود تا نتیجه‌ی هر فراخوانی با ورودی یکسان
/// قابل بازتولید باشد (برای تست/بازبینی مهندسی)، نه صرفاً یک عدد تصادفی غیرقابل‌ردیابی.
/// </summary>
public class ReliabilitySimulationService : IReliabilitySimulationService
{
    private readonly IRamWeibullService _ramWeibullService;

    public ReliabilitySimulationService(IRamWeibullService ramWeibullService)
    {
        _ramWeibullService = ramWeibullService;
    }

    public async Task<ReliabilitySimulationDto> SimulateAvailabilityAsync(int equipmentId, int iterations = 1000, double horizonHours = 8760)
    {
        var dto = new ReliabilitySimulationDto { EquipmentId = equipmentId, Iterations = iterations, SimulatedHorizonHours = horizonHours };

        var weibull = await _ramWeibullService.GetWeibullAnalysisAsync(equipmentId);
        if (weibull.DataStatus == "INSUFFICIENT_DATA" || !weibull.Beta.HasValue || !weibull.Eta.HasValue)
        {
            dto.DataStatus = "NOT_READY";
            dto.Reason = "پارامترهای Weibull (β/η) هنوز از داده‌ی واقعی برازش نشده‌اند - " + (weibull.Reason ?? "داده‌ی خرابی کافی نیست.");
            return dto;
        }

        var ram = await _ramWeibullService.GetRamAnalysisAsync(equipmentId);
        if (!ram.MTTRHours.HasValue || ram.MTTRHours.Value <= 0)
        {
            dto.DataStatus = "NOT_READY";
            dto.Reason = "MTTR واقعی هنوز از داده‌ی کافی محاسبه نشده - شبیه‌سازی بدون آن معنادار نیست.";
            return dto;
        }

        dto.InputBeta = weibull.Beta;
        dto.InputEta = weibull.Eta;
        dto.InputMTTRHours = ram.MTTRHours;

        var seed = unchecked(equipmentId * 397 ^ iterations);
        var random = new Random(seed);

        var availabilities = new double[iterations];
        var downtimes = new double[iterations];
        var failureCounts = new double[iterations];

        for (var iter = 0; iter < iterations; iter++)
        {
            var clock = 0.0;
            var totalDowntime = 0.0;
            var failures = 0;

            while (clock < horizonHours)
            {
                var u = random.NextDouble();
                if (u <= 0) u = 1e-9;
                var timeToFailure = weibull.Eta.Value * Math.Pow(-Math.Log(1 - u), 1.0 / weibull.Beta.Value);
                clock += timeToFailure;
                if (clock >= horizonHours) break;

                failures++;
                // تعمیر با توزیع نمایی و میانگین MTTR واقعی نمونه‌برداری می‌شود (ساده‌سازی مستند)
                var repairU = random.NextDouble();
                if (repairU <= 0) repairU = 1e-9;
                var repairTime = -ram.MTTRHours.Value * Math.Log(1 - repairU);

                totalDowntime += repairTime;
                clock += repairTime;
            }

            totalDowntime = Math.Min(totalDowntime, horizonHours);
            availabilities[iter] = 100.0 * (1 - totalDowntime / horizonHours);
            downtimes[iter] = totalDowntime;
            failureCounts[iter] = failures;
        }

        Array.Sort(availabilities);

        dto.DataStatus = "READY";
        dto.MeanAvailabilityPercent = availabilities.Average();
        dto.P10AvailabilityPercent = Percentile(availabilities, 0.10);
        dto.P90AvailabilityPercent = Percentile(availabilities, 0.90);
        dto.MeanDowntimeHours = downtimes.Average();
        dto.MeanFailureCount = failureCounts.Average();

        return dto;
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0) return 0;
        var index = (int)Math.Clamp(Math.Round(percentile * (sortedValues.Length - 1)), 0, sortedValues.Length - 1);
        return sortedValues[index];
    }
}
