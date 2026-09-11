using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۲/۱۳. Weibull از فاصله‌های واقعی متوالی بین FailureRecord.FailureDate
/// یک تجهیز برآورد می‌شود (نه از تاریخ راه‌اندازی - چون یک تاریخ راه‌اندازی معتبر برای کل جمعیت
/// تجهیز به‌صورت قابل‌اتکا موجود نیست). RAM روی IReliabilityService موجود سوار است.
/// </summary>
public class RamWeibullService : IRamWeibullService
{
    // طبق همان قرارداد MinimumReadySampleSize=5 که در ReliabilityIntelligenceService (فاز ۷)
    // تثبیت شده - با ۵ رکورد خرابی، ۴ فاصله‌ی واقعی برای برازش Weibull داریم.
    public const int MinimumFailureRecordsForWeibull = 5;
    private const int ReadyThresholdIntervals = 7; // >=8 خرابی => >=7 فاصله => READY؛ بین این دو => PARTIAL

    private readonly PEMSDbContext _context;
    private readonly IReliabilityService _reliabilityService;

    public RamWeibullService(PEMSDbContext context, IReliabilityService reliabilityService)
    {
        _context = context;
        _reliabilityService = reliabilityService;
    }

    public async Task<WeibullAnalysisDto> GetWeibullAnalysisAsync(int equipmentId)
    {
        var equipment = await _context.Equipments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EquipmentId == equipmentId);

        var dto = new WeibullAnalysisDto
        {
            EquipmentId = equipmentId,
            TagNumber = equipment?.TagNumber,
            RequiredMinimumSample = MinimumFailureRecordsForWeibull
        };

        var failureDates = await _context.FailureRecords.AsNoTracking()
            .Where(x => x.EquipmentId == equipmentId && x.IsActive)
            .OrderBy(x => x.FailureDate)
            .Select(x => x.FailureDate)
            .ToListAsync();

        if (failureDates.Count < MinimumFailureRecordsForWeibull)
        {
            dto.SampleSize = Math.Max(0, failureDates.Count - 1);
            dto.DataStatus = "INSUFFICIENT_DATA";
            dto.Reason = $"حداقل {MinimumFailureRecordsForWeibull} رکورد خرابی واقعی برای برازش Weibull لازم است (فعلاً {failureDates.Count} رکورد موجود است).";
            return dto;
        }

        var intervals = new List<double>();
        for (var i = 1; i < failureDates.Count; i++)
        {
            var hours = (failureDates[i] - failureDates[i - 1]).TotalHours;
            if (hours > 0) intervals.Add(hours);
        }

        if (intervals.Count < MinimumFailureRecordsForWeibull - 1)
        {
            dto.SampleSize = intervals.Count;
            dto.DataStatus = "INSUFFICIENT_DATA";
            dto.Reason = $"پس از حذف فاصله‌های نامعتبر (تاریخ تکراری/معکوس)، تعداد فاصله‌های معتبر ({intervals.Count}) کمتر از حداقل لازم است.";
            return dto;
        }

        dto.SampleSize = intervals.Count;

        var fit = FitWeibullMedianRankRegression(intervals);
        dto.Beta = fit.Beta;
        dto.Eta = fit.Eta;
        dto.RSquared = fit.RSquared;
        dto.BetaInterpretation = InterpretBeta(fit.Beta);
        dto.DataStatus = intervals.Count >= ReadyThresholdIntervals ? "READY" : "PARTIAL";
        dto.Reason = dto.DataStatus == "PARTIAL"
            ? $"نمونه کوچک است ({intervals.Count} فاصله) - پارامترها را فقط جهت‌نما در نظر بگیرید، نه قطعی."
            : null;

        return dto;
    }

    public async Task<RamAnalysisDto> GetRamAnalysisAsync(int equipmentId)
    {
        var reliability = await _reliabilityService.GetEquipmentReliabilityAsync(equipmentId);
        var dto = new RamAnalysisDto { EquipmentId = equipmentId };

        if (reliability == null)
        {
            dto.ReliabilityDataStatus = "NOT_READY";
            dto.MaintainabilityDataStatus = "NOT_READY";
            dto.InherentAvailabilityDataStatus = "NOT_READY";
            dto.OperationalAvailabilityDataStatus = "NOT_READY";
            return dto;
        }

        dto.TagNumber = reliability.TagNumber;
        dto.FailureSampleSize = reliability.FailureCount;

        if (reliability.MTBFHours.HasValue)
        {
            dto.MTBFHours = reliability.MTBFHours;
            dto.FailureRateLambdaPerHour = reliability.MTBFHours.Value > 0 ? 1.0 / reliability.MTBFHours.Value : null;
            dto.ReliabilityDataStatus = "READY";
        }
        else
        {
            dto.ReliabilityDataStatus = reliability.FailureCount == 0 ? "NOT_READY" : "INSUFFICIENT_DATA";
        }

        if (reliability.MTTRHours.HasValue)
        {
            dto.MTTRHours = reliability.MTTRHours;
            dto.MaintainabilityDataStatus = "READY";
        }
        else
        {
            dto.MaintainabilityDataStatus = "INSUFFICIENT_DATA";
        }

        if (reliability.AvailabilityPercent.HasValue)
        {
            dto.InherentAvailabilityPercent = reliability.AvailabilityPercent;
            dto.InherentAvailabilityDataStatus = "READY";
        }
        else
        {
            dto.InherentAvailabilityDataStatus = "INSUFFICIENT_DATA";
        }

        // طبق بند ۱۲: Operational Availability فقط وقتی یک بازه‌ی تقویمی واقعی قابل تعریف است
        // (اولین خرابی ثبت‌شده تا اکنون) محاسبه می‌شود - هرگز از مدت‌زمان WorkOrder استنتاج نمی‌شود.
        if (reliability.FirstFailureDate.HasValue && reliability.TotalDowntimeHours > 0)
        {
            var windowHours = (DateTime.Now - reliability.FirstFailureDate.Value).TotalHours;
            if (windowHours > 0)
            {
                dto.CalendarWindowHoursConsidered = windowHours;
                dto.TotalDowntimeHoursConsidered = reliability.TotalDowntimeHours;
                dto.OperationalAvailabilityPercent = Math.Max(0, 100.0 * (1 - ((double)reliability.TotalDowntimeHours / windowHours)));
                dto.OperationalAvailabilityDataStatus = "READY";
            }
        }
        else
        {
            dto.OperationalAvailabilityDataStatus = reliability.FailureCount == 0 ? "NOT_READY" : "INSUFFICIENT_DATA";
        }

        return dto;
    }

    private static string InterpretBeta(double? beta)
    {
        if (!beta.HasValue) return "N/A";
        if (beta.Value < 0.9) return "Decreasing failure rate (infant mortality / early-life pattern)";
        if (beta.Value <= 1.1) return "Approximately constant failure rate (random failures)";
        return "Increasing failure rate (wear-out pattern)";
    }

    private static (double? Beta, double? Eta, double? RSquared) FitWeibullMedianRankRegression(List<double> intervals)
    {
        var sorted = intervals.OrderBy(x => x).ToList();
        var n = sorted.Count;

        var xs = new double[n];
        var ys = new double[n];

        for (var i = 0; i < n; i++)
        {
            // تقریب برنارد برای رتبه‌ی میانه
            var medianRank = (i + 1 - 0.3) / (n + 0.4);
            xs[i] = Math.Log(sorted[i]);
            ys[i] = Math.Log(-Math.Log(1 - medianRank));
        }

        var xMean = xs.Average();
        var yMean = ys.Average();

        var sxy = 0.0;
        var sxx = 0.0;
        var syy = 0.0;
        for (var i = 0; i < n; i++)
        {
            sxy += (xs[i] - xMean) * (ys[i] - yMean);
            sxx += (xs[i] - xMean) * (xs[i] - xMean);
            syy += (ys[i] - yMean) * (ys[i] - yMean);
        }

        if (sxx == 0) return (null, null, null);

        var beta = sxy / sxx;
        var intercept = yMean - beta * xMean;
        var eta = Math.Exp(-intercept / beta);
        var rSquared = syy == 0 ? (double?)null : (sxy * sxy) / (sxx * syy);

        return (beta, eta, rSquared);
    }
}
