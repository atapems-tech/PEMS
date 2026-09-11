using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 9 — Enterprise Reliability, RAM &amp; Predictive Maintenance Intelligence»
/// بند ۱۲/۱۳: تست‌های موتور RAM/Weibull. قانون اصلی: هرگز پارامتر جعلی از داده‌ی ناکافی -
/// همیشه INSUFFICIENT_DATA با Reason صریح.
/// </summary>
public class RamWeibullServiceTests
{
    private static async Task<(RamWeibullService Service, PEMS.Persistence.Context.PEMSDbContext Ctx)> CreateServiceAsync(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"RamWeibull_{dbName}");
        ctx.Equipments.Add(new Equipment { EquipmentId = 1, TagNumber = "P-101", Name = "Pump", EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1, IsActive = true });
        await ctx.SaveChangesAsync();

        var reliabilityService = new ReliabilityService(ctx);
        return (new RamWeibullService(ctx, reliabilityService), ctx);
    }

    private static void AddFailure(PEMS.Persistence.Context.PEMSDbContext ctx, int id, DateTime date) =>
        ctx.FailureRecords.Add(new FailureRecord { FailureRecordId = id, EquipmentId = 1, FailureDate = date, FailureMode = "Wear", DowntimeHours = 1, IsActive = true, CreatedDate = DateTime.Now });

    [Fact]
    public async Task GetWeibullAnalysisAsync_FewerThanFiveFailures_ReturnsInsufficientData_NoParameters()
    {
        var (service, ctx) = await CreateServiceAsync(nameof(GetWeibullAnalysisAsync_FewerThanFiveFailures_ReturnsInsufficientData_NoParameters));
        AddFailure(ctx, 1, new DateTime(2026, 1, 1));
        AddFailure(ctx, 2, new DateTime(2026, 2, 1));
        AddFailure(ctx, 3, new DateTime(2026, 3, 1));
        await ctx.SaveChangesAsync();

        var result = await service.GetWeibullAnalysisAsync(1);

        Assert.Equal("INSUFFICIENT_DATA", result.DataStatus);
        Assert.Null(result.Beta);
        Assert.Null(result.Eta);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public async Task GetWeibullAnalysisAsync_NoFailures_ReturnsInsufficientData_ZeroSampleSize()
    {
        var (service, _) = await CreateServiceAsync(nameof(GetWeibullAnalysisAsync_NoFailures_ReturnsInsufficientData_ZeroSampleSize));

        var result = await service.GetWeibullAnalysisAsync(1);

        Assert.Equal("INSUFFICIENT_DATA", result.DataStatus);
        Assert.Equal(0, result.SampleSize);
        Assert.Equal(5, result.RequiredMinimumSample);
    }

    [Fact]
    public async Task GetWeibullAnalysisAsync_FiveFailuresIncreasingIntervals_ComputesIncreasingBeta()
    {
        var (service, ctx) = await CreateServiceAsync(nameof(GetWeibullAnalysisAsync_FiveFailuresIncreasingIntervals_ComputesIncreasingBeta));
        // فاصله‌های واقعی رو به افزایش (Wear-out pattern) - باید β > 1 برگرداند
        var start = new DateTime(2026, 1, 1);
        AddFailure(ctx, 1, start);
        AddFailure(ctx, 2, start.AddDays(100));
        AddFailure(ctx, 3, start.AddDays(250));
        AddFailure(ctx, 4, start.AddDays(450));
        AddFailure(ctx, 5, start.AddDays(700));
        await ctx.SaveChangesAsync();

        var result = await service.GetWeibullAnalysisAsync(1);

        Assert.True(result.DataStatus is "PARTIAL" or "READY");
        Assert.NotNull(result.Beta);
        Assert.NotNull(result.Eta);
        Assert.True(result.Eta > 0);
        Assert.Equal(4, result.SampleSize); // 5 خرابی => ۴ فاصله
        Assert.Contains("wear-out", result.BetaInterpretation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetWeibullAnalysisAsync_EightFailures_ReturnsReady_NotJustPartial()
    {
        var (service, ctx) = await CreateServiceAsync(nameof(GetWeibullAnalysisAsync_EightFailures_ReturnsReady_NotJustPartial));
        var start = new DateTime(2026, 1, 1);
        for (var i = 0; i < 8; i++)
            AddFailure(ctx, i + 1, start.AddDays(i * 60 + i * i * 5));
        await ctx.SaveChangesAsync();

        var result = await service.GetWeibullAnalysisAsync(1);

        Assert.Equal("READY", result.DataStatus);
        Assert.Equal(7, result.SampleSize);
    }

    [Fact]
    public async Task GetWeibullAnalysisAsync_DuplicateFailureDates_FiltersNonPositiveIntervals()
    {
        var (service, ctx) = await CreateServiceAsync(nameof(GetWeibullAnalysisAsync_DuplicateFailureDates_FiltersNonPositiveIntervals));
        var d = new DateTime(2026, 1, 1);
        AddFailure(ctx, 1, d);
        AddFailure(ctx, 2, d); // فاصله صفر - باید فیلتر شود
        AddFailure(ctx, 3, d.AddDays(10));
        AddFailure(ctx, 4, d.AddDays(20));
        AddFailure(ctx, 5, d.AddDays(30));
        await ctx.SaveChangesAsync();

        var result = await service.GetWeibullAnalysisAsync(1);

        // ۵ رکورد => ۴ فاصله خام، یکی صفر است => ۳ فاصله معتبر => کمتر از حداقل (۴) => INSUFFICIENT_DATA
        Assert.Equal("INSUFFICIENT_DATA", result.DataStatus);
        Assert.Equal(3, result.SampleSize);
    }

    [Fact]
    public async Task GetRamAnalysisAsync_NoFailureHistory_ReturnsNotReady()
    {
        var (service, _) = await CreateServiceAsync(nameof(GetRamAnalysisAsync_NoFailureHistory_ReturnsNotReady));

        var result = await service.GetRamAnalysisAsync(1);

        Assert.Equal("NOT_READY", result.ReliabilityDataStatus);
        Assert.Null(result.MTBFHours);
        Assert.Null(result.FailureRateLambdaPerHour);
    }

    [Fact]
    public async Task GetRamAnalysisAsync_TwoFailures_ComputesMTBFAndLambda()
    {
        var (service, ctx) = await CreateServiceAsync(nameof(GetRamAnalysisAsync_TwoFailures_ComputesMTBFAndLambda));
        AddFailure(ctx, 1, new DateTime(2026, 1, 1));
        AddFailure(ctx, 2, new DateTime(2026, 2, 1)); // 31 روز = 744 ساعت بعد
        await ctx.SaveChangesAsync();

        var result = await service.GetRamAnalysisAsync(1);

        Assert.Equal("READY", result.ReliabilityDataStatus);
        Assert.NotNull(result.MTBFHours);
        Assert.NotNull(result.FailureRateLambdaPerHour);
        Assert.Equal(1.0 / result.MTBFHours!.Value, result.FailureRateLambdaPerHour!.Value, precision: 10);
    }

    [Fact]
    public async Task GetRamAnalysisAsync_OneFailureOnly_MTBFInsufficientButNotNotReady()
    {
        var (service, ctx) = await CreateServiceAsync(nameof(GetRamAnalysisAsync_OneFailureOnly_MTBFInsufficientButNotNotReady));
        AddFailure(ctx, 1, new DateTime(2026, 1, 1));
        await ctx.SaveChangesAsync();

        var result = await service.GetRamAnalysisAsync(1);

        // یک خرابی: FailureCount=1 > 0، ولی MTBF هنوز Null (نیاز به ≥۲) => INSUFFICIENT_DATA نه NOT_READY
        Assert.Equal("INSUFFICIENT_DATA", result.ReliabilityDataStatus);
        Assert.Null(result.MTBFHours);
        Assert.Equal(1, result.FailureSampleSize);
    }
}
