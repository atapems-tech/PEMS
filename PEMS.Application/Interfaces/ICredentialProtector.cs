namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۱۲: هیچ رمز عبور/کلید خصوصی هرگز
/// به‌صورت متن‌خام در دیتابیس ذخیره نمی‌شود. پیاده‌سازی واقعی بر پایه‌ی
/// Microsoft.AspNetCore.DataProtection است (تنها مکانیزم رمزنگاری سمت سرور که هم‌اکنون در
/// خودِ فریم‌ورک ASP.NET Core موجود است؛ پیش از این هیچ الگوی رمزنگاری مشابهی در پروژه
/// استفاده نشده بود - این یک قابلیت واقعاً جدید است، نه بسط یک الگوی قبلی).
/// </summary>
public interface ICredentialProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
