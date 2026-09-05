using System.Text;

namespace PEMS.Shared.Search;

/// <summary>
/// ==============================================================================
/// منطق مشترک امتیازدهی (Ranking by Relevance) و Highlight برای موتور جستجوی سراسری.
/// عمداً در PEMS.Shared قرار گرفته (نه Application/Persistence) چون این یک Utility کاملاً
/// مستقل از دیتابیس است و باید بدون وابستگی چرخه‌ای در همه‌جا (حتی احتمالاً Frontend/API
/// جداگانه در آینده) قابل‌استفاده باشد.
///
/// طبق تصمیم معماری: Fuzzy Search واقعی (تحمل خطای تایپی در سطح موتور دیتابیس) به فاز C
/// (SQL Full-Text Search) موکول شده. این‌جا فقط یک Fuzzy سبک و در-حافظه (Levenshtein روی
/// نتایج از‌قبل‌بازیابی‌شده با LIKE) پیاده‌سازی شده - برای تحمل ۱-۲ حرف اشتباه کافی است،
/// ولی جایگزین یک موتور جستجوی واقعی نیست.
/// ==============================================================================
/// </summary>
public static class SearchScoring
{
    public const int ScoreExactMatch = 100;
    public const int ScoreStartsWith = 80;
    public const int ScoreContains = 50;
    public const int ScoreFuzzy = 30;

    /// <summary>
    /// امتیاز تطبیق یک مقدار فیلد در برابر عبارت جستجو. هرچه بیشتر، مرتبط‌تر.
    /// ۰ یعنی هیچ تطبیقی (نه حتی Fuzzy) پیدا نشد.
    /// </summary>
    public static int ComputeFieldScore(string? fieldValue, string query)
    {
        if (string.IsNullOrWhiteSpace(fieldValue) || string.IsNullOrWhiteSpace(query))
            return 0;

        var value = fieldValue.Trim();
        var q = query.Trim();

        if (string.Equals(value, q, StringComparison.OrdinalIgnoreCase))
            return ScoreExactMatch;

        if (value.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            return ScoreStartsWith;

        if (value.Contains(q, StringComparison.OrdinalIgnoreCase))
            return ScoreContains;

        // Fuzzy: فقط برای عبارات کوتاه (زیر ۲۵ کاراکتر) و وقتی فاصله‌ی ویرایشی کوچک است -
        // وگرنه محاسبه‌ی Levenshtein روی رشته‌های بلند برای هر ردیف پرهزینه می‌شود
        if (q.Length is >= 2 and <= 25 && value.Length <= 60)
        {
            var maxAllowedDistance = q.Length <= 4 ? 1 : 2;
            if (LevenshteinDistance(value.ToLowerInvariant(), q.ToLowerInvariant()) <= maxAllowedDistance)
                return ScoreFuzzy;
        }

        return 0;
    }

    /// <summary>
    /// بهترین امتیاز وزن‌دار بین چند فیلد (مثلاً TagNumber وزن ۱.۰، Description وزن ۰.۶) -
    /// یعنی تطبیق روی فیلد مهم‌تر حتی با نوع تطبیق ضعیف‌تر، می‌تواند از تطبیق قوی روی فیلد
    /// کم‌اهمیت‌تر جلو بزند.
    /// </summary>
    public static int ComputeWeightedScore(string query, params (string? Value, double Weight)[] fields)
    {
        var best = 0.0;
        foreach (var (value, weight) in fields)
        {
            var raw = ComputeFieldScore(value, query);
            var weighted = raw * weight;
            if (weighted > best) best = weighted;
        }
        return (int)Math.Round(best);
    }

    /// <summary>
    /// عبارت جستجو را داخل متن با &lt;mark&gt; هایلایت می‌کند (Case Insensitive). اگر تطبیق
    /// پیدا نشد (مثلاً چون فقط Fuzzy بود)، متن اصلی بدون تغییر برگردانده می‌شود.
    /// </summary>
    public static string Highlight(string? text, string query)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        if (string.IsNullOrWhiteSpace(query)) return System.Net.WebUtility.HtmlEncode(text);

        var idx = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return System.Net.WebUtility.HtmlEncode(text);

        var sb = new StringBuilder();
        sb.Append(System.Net.WebUtility.HtmlEncode(text[..idx]));
        sb.Append("<mark>");
        sb.Append(System.Net.WebUtility.HtmlEncode(text.Substring(idx, query.Length)));
        sb.Append("</mark>");
        sb.Append(System.Net.WebUtility.HtmlEncode(text[(idx + query.Length)..]));

        return sb.ToString();
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var lenA = a.Length;
        var lenB = b.Length;
        var d = new int[lenA + 1, lenB + 1];

        for (var i = 0; i <= lenA; i++) d[i, 0] = i;
        for (var j = 0; j <= lenB; j++) d[0, j] = j;

        for (var i = 1; i <= lenA; i++)
        {
            for (var j = 1; j <= lenB; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[lenA, lenB];
    }
}
