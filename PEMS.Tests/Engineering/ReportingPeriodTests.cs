using PEMS.Application.Common;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۱۵/۱۹: تست‌های پایه‌ی
/// ReportingPeriod - قطعیت مرزهای Day/Month/CustomRange، و اثبات این‌که Week بدون تایید
/// کسب‌وکاری هیچ پیش‌فرضی حدس نمی‌زند (Exception می‌دهد، نه یک مقدار ساکت اشتباه).
/// </summary>
public class ReportingPeriodTests
{
    [Fact]
    public void Resolve_Day_ReturnsCalendarDayBoundaries_MidnightToMidnight()
    {
        // طبق بخش ۱۷ سند فاز: هیچ «روز عملیاتی» تاییدشده‌ای وجود ندارد - Day عمداً روی روز
        // تقویمی ساده (نیمه‌شب تا نیمه‌شب) کار می‌کند، نه یک ساعت شروع حدسی.
        var anchor = new DateTime(2026, 3, 15, 14, 30, 0);

        var range = ReportingPeriod.Resolve(PeriodType.Day, anchor);

        Assert.Equal(new DateTime(2026, 3, 15, 0, 0, 0), range.Start);
        Assert.Equal(new DateTime(2026, 3, 16, 0, 0, 0), range.End);
    }

    [Fact]
    public void Resolve_Month_ReturnsFullGregorianMonth()
    {
        var anchor = new DateTime(2026, 2, 10);

        var range = ReportingPeriod.Resolve(PeriodType.Month, anchor);

        Assert.Equal(new DateTime(2026, 2, 1), range.Start);
        Assert.Equal(new DateTime(2026, 3, 1), range.End); // فوریه ۲۸ روزه در ۲۰۲۶
    }

    [Fact]
    public void Resolve_Month_LeapYear_HandlesFebruaryCorrectly()
    {
        var anchor = new DateTime(2028, 2, 10); // ۲۰۲۸ کبیسه است

        var range = ReportingPeriod.Resolve(PeriodType.Month, anchor);

        Assert.Equal(new DateTime(2028, 3, 1), range.End);
        Assert.Equal(29, (range.End - range.Start).Days);
    }

    [Fact]
    public void Resolve_Week_WithoutExplicitWeekStartDay_Throws()
    {
        // طبق بخش ۱۹ سند فاز: هیچ پیش‌فرضی برای WeekStartDay حدس زده نمی‌شود - این دقیقاً همون
        // «تصمیم کسب‌وکاری تاییدنشده» است که نباید در این کلاس ساکت فرض شود.
        var ex = Assert.Throws<InvalidOperationException>(() => ReportingPeriod.Resolve(PeriodType.Week, DateTime.Today));

        Assert.Contains("WeekStartDay", ex.Message);
    }

    [Fact]
    public void Resolve_Week_WithExplicitStartDay_ReturnsSevenDayRange()
    {
        // شنبه ۱۴۰۴/۱۲/... معادل یک شنبه‌ی میلادی فرضی - فقط برای اثبات محاسبه، نه یک قرارداد تایید‌شده
        var wednesday = new DateTime(2026, 3, 18); // چهارشنبه

        var range = ReportingPeriod.Resolve(PeriodType.Week, wednesday, weekStartDay: DayOfWeek.Saturday);

        Assert.Equal(DayOfWeek.Saturday, range.Start.DayOfWeek);
        Assert.Equal(7, (range.End - range.Start).Days);
        Assert.True(wednesday >= range.Start && wednesday < range.End);
    }

    [Fact]
    public void Resolve_CustomRange_IsInclusiveOfToDate()
    {
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 1, 5);

        var range = ReportingPeriod.Resolve(PeriodType.CustomRange, from, to);

        Assert.Equal(from, range.Start);
        Assert.Equal(new DateTime(2026, 1, 6), range.End); // انحصاری - شامل کل روز ۵ ام می‌شود
    }

    [Fact]
    public void Resolve_CustomRange_ToDateBeforeFromDate_Throws()
    {
        var from = new DateTime(2026, 1, 10);
        var to = new DateTime(2026, 1, 5);

        Assert.Throws<ArgumentException>(() => ReportingPeriod.Resolve(PeriodType.CustomRange, from, to));
    }

    [Fact]
    public void Resolve_CustomRange_MissingDates_Throws()
    {
        Assert.Throws<ArgumentException>(() => ReportingPeriod.Resolve(PeriodType.CustomRange, null, null));
    }
}
