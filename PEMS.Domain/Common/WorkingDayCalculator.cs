namespace PEMS.Domain.Common;

/// <summary>
/// طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» بخش ۵.۶ (بازبینی ۱۰/۱۵ روز کاری) و تأیید
/// کاربر: تعطیل هفتگی فقط جمعه است (نه پنج‌شنبه). تعطیلات رسمی در این نسخه لحاظ نشده - فقط
/// جمعه‌ها از شمارش کنار گذاشته می‌شوند.
/// </summary>
public static class WorkingDayCalculator
{
    public static DateTime AddWorkingDays(DateTime start, int workingDays)
    {
        var date = start;
        var added = 0;

        while (added < workingDays)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek != DayOfWeek.Friday)
                added++;
        }

        return date;
    }
}
