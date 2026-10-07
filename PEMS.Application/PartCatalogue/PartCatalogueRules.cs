using System.Text.RegularExpressions;

namespace PEMS.Application.PartCatalogue;

/// <summary>
/// قواعد خالص (بدون DB) درخواست کاتالوگ قطعه - منبع: «دستورالعمل.docx» (۳۴ فیلد Add + ۴ فیلد Change).
/// در Application قرار دارد تا هم سرویس و هم تست‌های واحد مستقیماً از آن استفاده کنند؛ هیچ منطقی در Controller/JS نیست
/// (JS فقط همین قواعد را برای راحتی کاربر Mirror می‌کند؛ مرجع نهایی همیشه سرور است).
/// </summary>
public static class PartCatalogueRules
{
    /// <summary>وقتی Manufacturer = LOCAL است شماره‌ی قطعه‌ی سازنده باید دقیقاً این مقدار باشد (فیلد ۱۵).</summary>
    public const string NoPartNumber = "NO_PART_NR";

    public const string LocalManufacturerName = "LOCAL";

    /// <summary>پیش‌فرض آستانه‌ی Low Value Asset (ریال) - «کالاهای پایین‌تر از ۲۵۰,۰۰۰ ریال»؛ قابل‌تنظیم در appsettings.</summary>
    public const decimal DefaultLowValueThresholdIrr = 250_000m;

    public const string IrrCurrencyCode = "IRR";

    /// <summary>نام نرمال‌شده برای تشخیص تکرار: فقط حروف/ارقام، حروف بزرگ.</summary>
    public static string NormalizeName(string? s) =>
        new string((s ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    /// <summary>سازنده‌هایی که انتهای نامشان «(wrong)» است نباید انتخاب شوند (فیلد ۱۳).</summary>
    public static bool IsWrongName(string? name) =>
        (name ?? string.Empty).TrimEnd().EndsWith("(wrong)", StringComparison.OrdinalIgnoreCase);

    public static bool IsLocalName(string? name) =>
        string.Equals((name ?? string.Empty).Trim(), LocalManufacturerName, StringComparison.OrdinalIgnoreCase);

    /// <summary>فیلد ۳۰: روش A ⇒ Minimum Stock و Max Consumption Rate صفر؛ روش B (و بقیه) ⇒ مقدار کاربر.</summary>
    public static (decimal? Min, decimal? Max) ApplyPlanningMethod(string? methodCode, decimal? min, decimal? max) =>
        string.Equals(methodCode?.Trim(), "A", StringComparison.OrdinalIgnoreCase) ? (0m, 0m) : (min, max);

    public static bool IsPlanningMethodA(string? methodCode) =>
        string.Equals(methodCode?.Trim(), "A", StringComparison.OrdinalIgnoreCase);

    /// <summary>فیلد ۱۵: سازنده LOCAL ⇒ شماره‌ی قطعه NO_PART_NR (بدون توجه به ورودی).</summary>
    public static string? ApplyManufacturerPartNumber(bool manufacturerIsLocal, string? partNumber) =>
        manufacturerIsLocal ? NoPartNumber : (string.IsNullOrWhiteSpace(partNumber) ? null : partNumber.Trim());

    /// <summary>
    /// FREE FORMAT TEXT باید تک‌خطی باشد (IFS فقط سطر اول را نشان می‌دهد) و همیشه مقدار داشته باشد: اگر توضیحی نیست،
    /// «یک فاصله (Space)» درج می‌شود (دستورالعمل صفحه ۲). خط‌های جدید/Tab به فاصله تبدیل و فاصله‌های پشت‌سرهم یکی می‌شوند.
    /// </summary>
    public static string NormalizeFreeFormatText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return " ";

        var oneLine = Regex.Replace(text, @"[\r\n\t\v\f\u0085  ]+", " ");
        oneLine = Regex.Replace(oneLine, @"\s{2,}", " ").Trim();
        return oneLine.Length == 0 ? " " : oneLine;
    }

    public static bool ContainsLineBreak(string? text) =>
        !string.IsNullOrEmpty(text) && Regex.IsMatch(text, @"[\r\n\u0085  ]");

    /// <summary>
    /// Change-۴: وقتی نام سازنده/شماره‌ی قطعه‌ی سازنده عوض شود، مقدار قبلی در FREE FORMAT TEXT به‌صورت
    /// «VENDOR NAME: ... VENDOR CODE: ...» اضافه می‌شود (Idempotent؛ دوبار اضافه نمی‌شود).
    /// </summary>
    public static string AppendOldVendorInfo(string? freeFormatText, string? oldName, string? oldCode, bool nameChanged, bool codeChanged)
    {
        var text = NormalizeFreeFormatText(freeFormatText).Trim();
        var parts = new List<string>();

        if (nameChanged && !string.IsNullOrWhiteSpace(oldName)
            && !text.Contains("VENDOR NAME:", StringComparison.OrdinalIgnoreCase))
            parts.Add($"VENDOR NAME: {oldName.Trim()}");

        if (codeChanged && !string.IsNullOrWhiteSpace(oldCode)
            && !text.Contains("VENDOR CODE:", StringComparison.OrdinalIgnoreCase))
            parts.Add($"VENDOR CODE: {oldCode.Trim()}");

        if (parts.Count == 0) return NormalizeFreeFormatText(freeFormatText);

        return NormalizeFreeFormatText(string.Join(" ", new[] { text }.Concat(parts)));
    }

    /// <summary>
    /// «اگر شماره‌ی پارت سازنده موجود نباشد و در شرح کالا به شماره‌ی Pos یا Item اشاره شود، نقشه/مدرک پیوست الزامی است»
    /// (فیلد ۲۶). تشخیص: POS / ITEM به‌همراه یک عدد (مثلاً "POS NO:54"، "ITEM NO 6100-015A").
    /// </summary>
    public static bool ReferencesPosOrItem(string? description) =>
        !string.IsNullOrWhiteSpace(description)
        && Regex.IsMatch(description, @"\b(POS|ITEM)\b[\s.:#\-]*(NO|NR|NUMBER)?[\s.:#\-]*\d+", RegexOptions.IgnoreCase);

    /// <summary>نام سازنده‌ی جدید: دقیق، لاتین، (حروف بزرگ نرمال می‌شود) - فیلد ۱۴.</summary>
    public static bool IsLatinManufacturerName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && Regex.IsMatch(name.Trim(), @"^[A-Za-z0-9][A-Za-z0-9 .,&()/+\-_']*$");

    public static string NormalizeNewManufacturerName(string? name) =>
        Regex.Replace((name ?? string.Empty).Trim(), @"\s{2,}", " ").ToUpperInvariant();

    /// <summary>
    /// جلوگیری از ایجاد سازنده‌ی تکراری (فیلد ۱۴): هم‌نام دقیق (پس از نرمال‌سازی) = Exact؛ شباهت زیاد (شمول یا فاصله‌ی
    /// ویرایشی کم) = Similar. فقط فهرست‌های «نرمال‌شده‌ی» ورودی مقایسه می‌شوند.
    /// </summary>
    public static (List<string> Exact, List<string> Similar) FindManufacturerMatches(
        string newName, IEnumerable<(string Name, string Normalized)> existing)
    {
        var key = NormalizeName(newName);
        var exact = new List<string>();
        var similar = new List<string>();
        if (key.Length == 0) return (exact, similar);

        foreach (var (name, normalized) in existing)
        {
            if (normalized.Length == 0) continue;
            if (normalized == key) { exact.Add(name); continue; }

            var shorter = Math.Min(normalized.Length, key.Length);
            var contains = shorter >= 4 && (normalized.Contains(key) || key.Contains(normalized));
            var threshold = Math.Max(1, (int)Math.Floor(Math.Max(normalized.Length, key.Length) * 0.15));
            if (contains || (shorter >= 4 && Levenshtein(normalized, key) <= threshold))
                similar.Add(name);
        }

        return (exact, similar.Take(10).ToList());
    }

    public static int Levenshtein(string a, string b)
    {
        if (a == b) return 0;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }

    /// <summary>
    /// مشتق‌سازی Is Critical / Is Capital از Type Designation (فیلدهای ۲۰/۲۱ فقط‌خواندنی). فقط برای Seed اولیه‌ی جدول؛
    /// در زمان اجرا مقدار از ردیف جدول Type Designation (قابل‌ویرایش توسط مدیر) خوانده می‌شود.
    /// Critical: Critical Part یا توکن CR؛ Capital: Capital Part یا توکن CAP.
    /// </summary>
    public static (bool IsCritical, bool IsCapital) DeriveTypeFlags(string designationCode)
    {
        var code = (designationCode ?? string.Empty).Trim();
        var tokens = code.Split(new[] { '-', ' ', '*' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.ToUpperInvariant()).ToHashSet();

        var critical = tokens.Contains("CR") || string.Equals(code, "Critical Part", StringComparison.OrdinalIgnoreCase);
        var capital = tokens.Contains("CAP") || string.Equals(code, "Capital Part", StringComparison.OrdinalIgnoreCase);
        return (critical, capital);
    }

    /// <summary>فقط نام‌های ≥۴ حرف را در متن آزاد جستجو می‌کند (جلوگیری از هشدارهای بی‌ارزش مثل "S+C").</summary>
    public static bool MentionsAnyName(string? text, IEnumerable<string?> names)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var haystack = NormalizeName(text);
        foreach (var n in names)
        {
            var key = NormalizeName(n);
            if (key.Length >= 4 && !string.Equals(key, "LOCAL", StringComparison.Ordinal) && haystack.Contains(key))
                return true;
        }
        return false;
    }
}
