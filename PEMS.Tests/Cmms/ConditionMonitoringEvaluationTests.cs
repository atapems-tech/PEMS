using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۵/۱۱:
/// این تست‌ها دقیقاً همان قوانین سختِ سند را ثابت می‌کنند - هیچ‌وقت به‌صورت خاموش Normal فرض
/// نشود (بند ۲۹)، نتیجه‌ی ارزیابی در لحظه‌ی ثبت منجمد شود و با نسخه‌ی بعدی پروفایل عوض نشود
/// (بند ۲۶)، و AlarmThresholdLow/High قدیمی (IsAlarm دو-سطحی) هرگز شکسته نشود.
/// </summary>
public class ConditionMonitoringEvaluationTests
{
    private static (ConditionMonitoringService Service, PEMS.Persistence.Context.PEMSDbContext Ctx) CreateService(string dbName)
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"CmEval_{dbName}");

        ctx.Equipments.Add(new Equipment
        {
            EquipmentId = 1,
            TagNumber = "PMP-001",
            Name = "Test Pump",
            EquipmentType = "Pump",
            IsActive = true
        });
        ctx.SaveChanges();

        return (new ConditionMonitoringService(ctx), ctx);
    }

    private static EvaluationProfile NewVibrationProfile(int id, int version = 1)
    {
        return new EvaluationProfile
        {
            EvaluationProfileId = id,
            Code = $"VIB-{id}",
            Name = "Vibration Profile",
            VariableType = "Vibration",
            Version = version,
            EffectiveFrom = DateTime.Now.AddDays(-30),
            IsActive = true,
            Limits = new List<EvaluationLimit>
            {
                new() { EvaluationLimitId = id * 10 + 1, EvaluationProfileId = id, Status = "Normal", LowValue = 0, HighValue = 2.8m, LowInclusive = true, HighInclusive = false, IsActive = true },
                new() { EvaluationLimitId = id * 10 + 2, EvaluationProfileId = id, Status = "Warning", LowValue = 2.8m, HighValue = 4.5m, LowInclusive = true, HighInclusive = false, IsActive = true },
                new() { EvaluationLimitId = id * 10 + 3, EvaluationProfileId = id, Status = "Alarm", LowValue = 4.5m, HighValue = 7.1m, LowInclusive = true, HighInclusive = false, IsActive = true },
                new() { EvaluationLimitId = id * 10 + 4, EvaluationProfileId = id, Status = "Critical", LowValue = 7.1m, LowInclusive = true, IsActive = true }
            }
        };
    }

    [Fact]
    public async Task RecordReadingAsync_NoProfileAssigned_StaysNotEvaluated_NeverGuessesNormal()
    {
        var (service, ctx) = CreateService(nameof(RecordReadingAsync_NoProfileAssigned_StaysNotEvaluated_NeverGuessesNormal));
        ctx.ConditionMonitoringPoints.Add(new ConditionMonitoringPoint { ConditionMonitoringPointId = 1, EquipmentId = 1, VariableType = "Vibration", Tag = "VIB-01", IsActive = true });
        await ctx.SaveChangesAsync();

        var (success, error) = await service.RecordReadingAsync(1, DateTime.Now, 1.5m, "Manual", null, "tester");

        Assert.True(success, error);
        var reading = ctx.ConditionReadings.Single();
        Assert.Equal("NotEvaluated", reading.EvaluationStatus);
        Assert.Null(reading.EvaluationProfileId);
        Assert.Null(reading.EvaluationProfileVersion);
    }

    [Theory]
    [InlineData(1.5, "Normal")]
    [InlineData(3.2, "Warning")]
    [InlineData(5.0, "Alarm")]
    [InlineData(8.0, "Critical")]
    public async Task RecordReadingAsync_WithProfile_EvaluatesCorrectBand(decimal value, string expectedStatus)
    {
        var (service, ctx) = CreateService($"{nameof(RecordReadingAsync_WithProfile_EvaluatesCorrectBand)}_{expectedStatus}");
        var profile = NewVibrationProfile(1);
        ctx.EvaluationProfiles.Add(profile);
        ctx.ConditionMonitoringPoints.Add(new ConditionMonitoringPoint
        {
            ConditionMonitoringPointId = 1, EquipmentId = 1, VariableType = "Vibration", Tag = "VIB-01",
            IsActive = true, EvaluationProfileId = 1
        });
        await ctx.SaveChangesAsync();

        var (success, error) = await service.RecordReadingAsync(1, DateTime.Now, value, "Manual", null, "tester");

        Assert.True(success, error);
        var reading = ctx.ConditionReadings.Single();
        Assert.Equal(expectedStatus, reading.EvaluationStatus);
        Assert.Equal(1, reading.EvaluationProfileId);
        Assert.Equal(1, reading.EvaluationProfileVersion);
    }

    [Fact]
    public async Task RecordReadingAsync_FreezesProfileVersionAtRecordTime_LaterProfileChangeDoesNotRewriteHistory()
    {
        var (service, ctx) = CreateService(nameof(RecordReadingAsync_FreezesProfileVersionAtRecordTime_LaterProfileChangeDoesNotRewriteHistory));
        var profile = NewVibrationProfile(1, version: 1);
        ctx.EvaluationProfiles.Add(profile);
        ctx.ConditionMonitoringPoints.Add(new ConditionMonitoringPoint
        {
            ConditionMonitoringPointId = 1, EquipmentId = 1, VariableType = "Vibration", Tag = "VIB-01",
            IsActive = true, EvaluationProfileId = 1
        });
        await ctx.SaveChangesAsync();

        await service.RecordReadingAsync(1, DateTime.Now, 5.0m, "Manual", null, "tester");
        var firstReading = ctx.ConditionReadings.Single();
        Assert.Equal("Alarm", firstReading.EvaluationStatus);
        Assert.Equal(1, firstReading.EvaluationProfileVersion);

        // طبق بند ۲۶: نسخه‌ی پروفایل تغییر می‌کند (مثلاً مهندس آستانه‌ها را بازبینی کرده) - اما
        // این نباید تفسیر قرائت قبلی را عوض کند، فقط روی قرائت‌های تازه اثر بگذارد.
        var trackedProfile = ctx.EvaluationProfiles.Single(x => x.EvaluationProfileId == 1);
        trackedProfile.Version = 2;
        ctx.EvaluationLimits.RemoveRange(ctx.EvaluationLimits.Where(x => x.EvaluationProfileId == 1));
        ctx.EvaluationLimits.Add(new EvaluationLimit { EvaluationLimitId = 999, EvaluationProfileId = 1, Status = "Normal", LowValue = 0, HighValue = 100, LowInclusive = true, HighInclusive = true, IsActive = true });
        await ctx.SaveChangesAsync();

        await service.RecordReadingAsync(1, DateTime.Now, 5.0m, "Manual", null, "tester");
        var readings = ctx.ConditionReadings.OrderBy(x => x.ConditionReadingId).ToList();

        Assert.Equal("Alarm", readings[0].EvaluationStatus); // منجمد - دست‌نخورده
        Assert.Equal(1, readings[0].EvaluationProfileVersion);
        Assert.Equal("Normal", readings[1].EvaluationStatus); // نسخه‌ی جدید
        Assert.Equal(2, readings[1].EvaluationProfileVersion);
    }

    [Fact]
    public async Task RecordReadingAsync_LegacyIsAlarm_StillWorksIndependentlyOfEvaluationProfile()
    {
        var (service, ctx) = CreateService(nameof(RecordReadingAsync_LegacyIsAlarm_StillWorksIndependentlyOfEvaluationProfile));
        ctx.ConditionMonitoringPoints.Add(new ConditionMonitoringPoint
        {
            ConditionMonitoringPointId = 1, EquipmentId = 1, VariableType = "Temperature", Tag = "TT-01",
            IsActive = true, AlarmThresholdLow = 10, AlarmThresholdHigh = 80
        });
        await ctx.SaveChangesAsync();

        await service.RecordReadingAsync(1, DateTime.Now, 95m, "Manual", null, "tester");

        var reading = ctx.ConditionReadings.Single();
        Assert.True(reading.IsAlarm);
        Assert.Equal("NotEvaluated", reading.EvaluationStatus); // بدون پروفایل - هرگز از IsAlarm نتیجه‌گیری نمی‌شود
    }

    [Fact]
    public async Task RecordReadingAsync_ValueWithNoMatchingLimitBand_StaysNotEvaluated()
    {
        // طبق بند ۲۵: اگر پروفایل فقط Alarm/Critical تعریف شده باشد (Normal/Warning پیکربندی
        // نشده)، مقداری که در هیچ بازه‌ای نمی‌افتد نباید حدس زده شود.
        var (service, ctx) = CreateService(nameof(RecordReadingAsync_ValueWithNoMatchingLimitBand_StaysNotEvaluated));
        var profile = new EvaluationProfile
        {
            EvaluationProfileId = 1, Code = "PARTIAL", Name = "Partial Profile", VariableType = "Vibration",
            Version = 1, EffectiveFrom = DateTime.Now.AddDays(-1), IsActive = true,
            Limits = new List<EvaluationLimit>
            {
                new() { EvaluationLimitId = 1, EvaluationProfileId = 1, Status = "Critical", LowValue = 10, LowInclusive = true, IsActive = true }
            }
        };
        ctx.EvaluationProfiles.Add(profile);
        ctx.ConditionMonitoringPoints.Add(new ConditionMonitoringPoint
        {
            ConditionMonitoringPointId = 1, EquipmentId = 1, VariableType = "Vibration", Tag = "VIB-01",
            IsActive = true, EvaluationProfileId = 1
        });
        await ctx.SaveChangesAsync();

        await service.RecordReadingAsync(1, DateTime.Now, 3.0m, "Manual", null, "tester");

        Assert.Equal("NotEvaluated", ctx.ConditionReadings.Single().EvaluationStatus);
    }

    [Fact]
    public async Task AssignEvaluationProfileAsync_InvalidProfileId_ReturnsError()
    {
        var (service, ctx) = CreateService(nameof(AssignEvaluationProfileAsync_InvalidProfileId_ReturnsError));
        ctx.ConditionMonitoringPoints.Add(new ConditionMonitoringPoint { ConditionMonitoringPointId = 1, EquipmentId = 1, VariableType = "Vibration", Tag = "VIB-01", IsActive = true });
        await ctx.SaveChangesAsync();

        var (success, error) = await service.AssignEvaluationProfileAsync(1, 999);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Null(ctx.ConditionMonitoringPoints.Single().EvaluationProfileId);
    }

    [Fact]
    public async Task AssignEvaluationProfileAsync_ValidProfile_UpdatesPoint()
    {
        var (service, ctx) = CreateService(nameof(AssignEvaluationProfileAsync_ValidProfile_UpdatesPoint));
        ctx.EvaluationProfiles.Add(NewVibrationProfile(1));
        ctx.ConditionMonitoringPoints.Add(new ConditionMonitoringPoint { ConditionMonitoringPointId = 1, EquipmentId = 1, VariableType = "Vibration", Tag = "VIB-01", IsActive = true });
        await ctx.SaveChangesAsync();

        var (success, error) = await service.AssignEvaluationProfileAsync(1, 1);

        Assert.True(success, error);
        Assert.Equal(1, ctx.ConditionMonitoringPoints.Single().EvaluationProfileId);
    }

    [Fact]
    public async Task GetTrendAsync_FiltersToDateRange_AndOrdersChronologically()
    {
        var (service, ctx) = CreateService(nameof(GetTrendAsync_FiltersToDateRange_AndOrdersChronologically));
        ctx.ConditionMonitoringPoints.Add(new ConditionMonitoringPoint { ConditionMonitoringPointId = 1, EquipmentId = 1, VariableType = "Temperature", Tag = "TT-01", IsActive = true });
        var now = DateTime.Now;
        ctx.ConditionReadings.AddRange(
            new ConditionReading { ConditionReadingId = 1, ConditionMonitoringPointId = 1, ReadingDate = now.AddDays(-45), Value = 10, EvaluationStatus = "NotEvaluated" },
            new ConditionReading { ConditionReadingId = 2, ConditionMonitoringPointId = 1, ReadingDate = now.AddDays(-5), Value = 20, EvaluationStatus = "NotEvaluated" },
            new ConditionReading { ConditionReadingId = 3, ConditionMonitoringPointId = 1, ReadingDate = now.AddDays(-1), Value = 30, EvaluationStatus = "NotEvaluated" }
        );
        await ctx.SaveChangesAsync();

        var trend = await service.GetTrendAsync(1, now.AddDays(-7), now);

        Assert.NotNull(trend);
        Assert.Equal(2, trend!.TotalInRange);
        Assert.Equal(2, trend.Points.Count);
        Assert.Equal(20, trend.Points[0].Value);
        Assert.Equal(30, trend.Points[1].Value);
        Assert.False(trend.Truncated);
    }

    [Fact]
    public async Task GetTrendAsync_UnknownPoint_ReturnsNull()
    {
        var (service, _) = CreateService(nameof(GetTrendAsync_UnknownPoint_ReturnsNull));

        var trend = await service.GetTrendAsync(999, DateTime.Now.AddDays(-7), DateTime.Now);

        Assert.Null(trend);
    }
}
