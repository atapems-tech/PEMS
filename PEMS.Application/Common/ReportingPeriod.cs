namespace PEMS.Application.Common;

public enum PeriodType
{
    Day,
    Week,
    Month,
    CustomRange
}

/// <summary>
/// طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۱۹-۲۰: یک نقطه‌ی مشترک برای
/// محاسبه‌ی بازه‌ی گزارش‌گیری - به‌جای این‌که هر گزارش/Controller محاسبه‌ی هفته/ماه خودش را
/// جدا بنویسد (که طبق ممیزی، دقیقاً همون چیزی است که قبلاً هیچ‌جای PEMS وجود نداشت).
///
/// عمداً هیچ منطق تقویم جلالی/فارسی این‌جا نیست - طبق ممیزی، هیچ تبدیل جلالی واقعی در کدِ برنامه
/// وجود ندارد و این کلاس قرار نیست حدسی آن را اضافه کند؛ اگر روزی لازم شد، لایه‌ی نمایش/UI باید
/// آن را اضافه کند، نه این هسته‌ی محاسباتی (طبق دستور صریح «Do not store Jalali date strings as
/// authoritative»).
///
/// عمداً «روز عملیاتی» (ساعت شروع غیر از نیمه‌شب) پیاده‌سازی نشده - طبق ممیزی هیچ تعریف
/// کسب‌وکاری تاییدشده‌ای برای آن پیدا نشد؛ Day/Week/Month همه روی «روز تقویمی ساده» (۰۰:۰۰ تا
/// ۲۳:۵۹:۵۹.۹۹۹ به وقت سرور، بدون UTC/Timezone - هم‌خوان با DateTime.Now در کل پروژه) کار
/// می‌کنند. این محدودیت آگاهانه است، نه فراموشی - طبق بند ۱۷ سند فاز مستند شده.
///
/// عمداً هیچ مقدار پیش‌فرضی برای WeekStartDay وجود ندارد (پارامتر اجباری برای PeriodType.Week) -
/// طبق دستور صریح «It must not silently assume a business calendar rule that has not been
/// approved»: حدس زدن شنبه (پیش‌فرض منطقه‌ای رایج) به‌عنوان مقدار پیش‌فرض دقیقاً همون حدسِ
/// ممنوع می‌بود؛ هر مصرف‌کننده‌ی آینده باید آگاهانه این تصمیم را در محل فراخوانی خودش بدهد.
/// </summary>
public static class ReportingPeriod
{
    public readonly record struct Range(DateTime Start, DateTime End);

    /// <summary>
    /// بازه‌ی [Start, End) دقیق را برمی‌گرداند - End همیشه انحصاری است (شروع روز/بازه‌ی بعدی)،
    /// نه ۲۳:۵۹:۵۹.۹۹۹ (که با گرد کردن اعشار DateTime می‌تواند لبه‌ی بازه را از دست بدهد).
    /// Query هایی که از این بازه استفاده می‌کنند باید `>= Start &amp;&amp; &lt; End` بنویسند،
    /// نه `&lt;= End`.
    /// </summary>
    public static Range Resolve(PeriodType type, DateTime? anchorOrFrom = null, DateTime? to = null, DayOfWeek? weekStartDay = null)
    {
        switch (type)
        {
            case PeriodType.Day:
            {
                var day = (anchorOrFrom ?? DateTime.Now).Date;
                return new Range(day, day.AddDays(1));
            }

            case PeriodType.Month:
            {
                var anchor = (anchorOrFrom ?? DateTime.Now).Date;
                var start = new DateTime(anchor.Year, anchor.Month, 1);
                return new Range(start, start.AddMonths(1));
            }

            case PeriodType.Week:
            {
                if (weekStartDay is null)
                    throw new InvalidOperationException(
                        "شروع هفته (WeekStartDay) هنوز توسط کسب‌وکار تایید نشده - طبق «PEMS Operations Phase 2A» " +
                        "بخش ۱۵/۱۹، این متد هیچ پیش‌فرضی حدس نمی‌زند. مقدار را صریحاً به این متد بدهید.");

                var anchor = (anchorOrFrom ?? DateTime.Now).Date;
                var diff = ((int)anchor.DayOfWeek - (int)weekStartDay.Value + 7) % 7;
                var start = anchor.AddDays(-diff);
                return new Range(start, start.AddDays(7));
            }

            case PeriodType.CustomRange:
            {
                if (anchorOrFrom is null || to is null)
                    throw new ArgumentException("بازه‌ی سفارشی نیاز به هر دوی تاریخ شروع و پایان دارد.");
                if (to.Value.Date < anchorOrFrom.Value.Date)
                    throw new ArgumentException("تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد.");

                // طبق قرارداد End انحصاری بالا - تاریخ پایانِ کاربر شامل خودش می‌شود، پس یک روز
                // جلوتر می‌رویم تا کل آن روز را در بر بگیرد.
                return new Range(anchorOrFrom.Value.Date, to.Value.Date.AddDays(1));
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
