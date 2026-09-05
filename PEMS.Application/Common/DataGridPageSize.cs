namespace PEMS.Application.Common;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» بند ۴: مجموعه‌ی مجاز اندازه‌ی صفحه در کل
/// برنامه دقیقاً {۲۰, ۵۰, ۱۰۰} است - هیچ مقدار دلخواه دیگری از سمت کلاینت نباید پذیرفته شود.
/// این یک نقطه‌ی مرکزی و قابل استفاده‌ی مجدد است تا هر Controller/Repository که PageSize را
/// از QueryString می‌خواند، همان قانون یکسان را اعمال کند - نه یک Clamp پراکنده و ناهم‌شکل
/// در هر فایل.
/// </summary>
public static class DataGridPageSize
{
    public static readonly int[] Allowed = { 20, 50, 100 };

    public const int Default = 50;

    /// <summary>
    /// مقدار درخواستی را فقط در صورتی می‌پذیرد که دقیقاً یکی از مقادیر مجاز باشد؛ در غیر این
    /// صورت (هر مقدار دیگر، صفر، منفی) بی‌صدا به مقدار پیش‌فرض ۵۰ بازمی‌گردد - طبق بند ۴:
    /// "If an invalid value is submitted, pageSize = 50 must be used as the safe fallback."
    /// </summary>
    public static int Normalize(int? requested) =>
        requested.HasValue && Array.IndexOf(Allowed, requested.Value) >= 0
            ? requested.Value
            : Default;
}
