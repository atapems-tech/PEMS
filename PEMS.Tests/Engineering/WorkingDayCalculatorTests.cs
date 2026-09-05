using PEMS.Domain.Common;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق بخش I/G ممیزی سخت‌سازی مهندسی: تعطیل هفتگی فقط جمعه است (طبق تأیید صریح کاربر).
/// </summary>
public class WorkingDayCalculatorTests
{
    [Fact]
    public void AddWorkingDays_SkipsFriday()
    {
        // 2026-08-13 پنج‌شنبه است؛ +1 روز کاری باید جمعه (2026-08-14) را رد کند و به
        // شنبه (2026-08-15) برسد.
        var thursday = new DateTime(2026, 8, 13);
        Assert.Equal(DayOfWeek.Thursday, thursday.DayOfWeek);

        var result = WorkingDayCalculator.AddWorkingDays(thursday, 1);

        Assert.Equal(new DateTime(2026, 8, 15), result);
        Assert.Equal(DayOfWeek.Saturday, result.DayOfWeek);
    }

    [Fact]
    public void AddWorkingDays_NeverLandsOnFriday()
    {
        var start = new DateTime(2026, 8, 13);

        for (var days = 1; days <= 20; days++)
        {
            var result = WorkingDayCalculator.AddWorkingDays(start, days);
            Assert.NotEqual(DayOfWeek.Friday, result.DayOfWeek);
        }
    }

    [Fact]
    public void AddWorkingDays_TenWorkingDays_MatchesManualCount()
    {
        // شمارش دستی: از 2026-08-13 (پنج‌شنبه) به بعد، ۱۰ روز کاری (بدون جمعه‌ها) باید
        // دقیقاً به 2026-08-25 برسد (دو جمعه‌ی 14 و 21 آگوست رد می‌شوند).
        var start = new DateTime(2026, 8, 13);
        var result = WorkingDayCalculator.AddWorkingDays(start, 10);

        Assert.Equal(new DateTime(2026, 8, 25), result);
    }
}
